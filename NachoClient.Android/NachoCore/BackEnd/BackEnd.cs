using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SQLite;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.IMAP;
using NachoCore.SMTP;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

/* Back-End:
 * The BE manages all protocol interaction with all servers (will be
 * extended beyond EAS). The BE communicates with the UI through APIs
 * and through the DB:
 * - The UI can call a BE API,
 * - The UI can modify the DB, and the BE detects it,
 * - The BE can modify the DB, and the UI detects it,
 * - The BE can invoke a callback API () on the UI.
 * There is only one BE object in the app. The BE is responsible for the
 * setup of the DB, and the UI gets access to the DB though the BE's
 * Db property.
 * 
 * The UI Must have started all accounts before modding the DB records associated
 * with those accounts - otherwise mod events will get dropped and not end up on the server.
 * */
namespace NachoCore
{
    public sealed class BackEnd : IBackEnd, INcProtoControlOwner
    {
        private static volatile BackEnd instance;
        private static object syncRoot = new Object ();

        public static BackEnd Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new BackEnd ();
                        }
                    }
                }
                return instance; 
            }
        }

        public enum DbActors
        {
            Ui,
            Proto,
        };

        public enum DbEvents
        {
            DidWrite,
            WillDelete,
        };

        private ConcurrentDictionary<int, ConcurrentQueue<NcProtoControl>> Services;
        private NcTimer PendingOnTimeTimer = null;
        private Dictionary<int, bool> CredReqActive;

        public IBackEndOwner Owner { set; private get; }

        private bool AccountHasServices (int accountId)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            return Services.ContainsKey (accountId);
        }

        private NcResult ApplyToService (int accountId, McAccount.AccountCapabilityEnum capability, Func<NcProtoControl, NcResult> func)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            ConcurrentQueue<NcProtoControl> services;
            if (!Services.TryGetValue (accountId, out services)) {
                Log.Error (Log.LOG_BACKEND, "ServiceFromAccountId called with bad accountId {0} @ {1}", accountId, new StackTrace ());
                return NcResult.Error (NcResult.SubKindEnum.Error_AccountDoesNotExist);
            }
            var protoControl = services.FirstOrDefault (x => capability == (x.Capabilities & capability));
            if (null == protoControl) {
                Log.Error (Log.LOG_BACKEND, "ServiceFromAccountId: can't find controller with desired capability {0}", capability);
                return NcResult.Error (NcResult.SubKindEnum.Error_NoCapableService);
            }
            return func (protoControl);
        }

        private NcResult ApplyAcrossServices (int accountId, string name, Func<NcProtoControl, NcResult> func)
        {
            var result = NcResult.OK ();
            NcResult iterResult = null;
            ConcurrentQueue<NcProtoControl> services = null;
            if (Services.TryGetValue (accountId, out services)) {
                foreach (var service in services) {
                    iterResult = func (service);
                    if (iterResult.isError ()) {
                        result = iterResult;
                    }
                }
                if (result.isOK ()) {
                    Log.Info (Log.LOG_BACKEND, "{0}({1})", name, accountId);
                } else {
                    Log.Warn (Log.LOG_BACKEND, "BackEnd.ApplyAcrossServices {0}({1}):{2}.", name, accountId, result.Message);
                }
            } else {
                Log.Warn (Log.LOG_BACKEND, "BackEnd.ApplyAcrossServices {0}({1}) could not find services.", name, accountId);
            }
            return result;
        }

        private void ApplyAcrossAccounts (string name, Action<int> func)
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                func (account.Id);
            }
        }

        // For IBackEnd.
        private BackEnd ()
        {
            // Adjust system settings.
            ServicePointManager.DefaultConnectionLimit = 25;

            Services = new ConcurrentDictionary<int, ConcurrentQueue<NcProtoControl>> ();
            CredReqActive = new Dictionary<int, bool> ();
        }

        public void CreateServices ()
        {
            ApplyAcrossAccounts ("CreateServices", (accountId) => {
                if (!AccountHasServices (accountId)) {
                    CreateServices (accountId);
                }
            });
        }

        public void Start ()
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start() called");
            // The callee does Task.Run.
            ApplyAcrossAccounts ("Start", (accountId) => {
                Start (accountId);
            });
        }

        // DON'T PUT Stop in a Task.Run. We want to execute as much as possible immediately.
        // Under iOS, there is a deadline. The ProtoControl's ForceStop must stop everything and
        // return without waiting.
        public void Stop ()
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Stop() called");
            if (null != PendingOnTimeTimer) {
                PendingOnTimeTimer.Dispose ();
                PendingOnTimeTimer = null;
            }
            ApplyAcrossAccounts ("Stop", (accountId) => {
                Stop (accountId);
            });
        }

        public void Stop (int accountId)
        {
            if (!AccountHasServices (accountId)) {
                CreateServices (accountId);
            }
            ApplyAcrossServices (accountId, "Stop", (service) => {
                service.ForceStop ();
                return NcResult.OK ();
            });
        }

        public void Remove (int accountId)
        {
            Stop (accountId);
            RemoveService (accountId);
        }

        public void CreateServices (int accountId)
        {
            var services = new ConcurrentQueue<NcProtoControl> ();
            var account = McAccount.QueryById<McAccount> (accountId);
            switch (account.AccountType) {
            case McAccount.AccountTypeEnum.Device:
                services.Enqueue (new DeviceProtoControl (this, accountId));
                break;

            case McAccount.AccountTypeEnum.Exchange:
                services.Enqueue (new AsProtoControl (this, accountId));
                break;

            case McAccount.AccountTypeEnum.IMAP_SMTP:
                services.Enqueue (new SmtpProtoControl (this, accountId));
                services.Enqueue (new ImapProtoControl (this, accountId));
                break;

            default:
                NcAssert.True (false);
                break;
            }
            Log.Info (Log.LOG_BACKEND, "CreateServices {0}", accountId);
            if (!Services.TryAdd (accountId, services)) {
                // Concurrency. Another thread has jumped in and done the add.
                Log.Info (Log.LOG_BACKEND, "Another thread has already called CreateServices for Account.Id {0}", accountId);
            }
        }

        // Service must be Stop()ed before calling RemoveService().
        public void RemoveService (int accountId)
        {
            ApplyAcrossServices (accountId, "RemoveService", (service) => {
                service.Remove ();
                return NcResult.OK ();
            });
        }

        public void Start (int accountId)
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start({0}) called", accountId);
            NcCommStatus.Instance.Refresh ();
            if (!AccountHasServices (accountId)) {
                CreateServices (accountId);
            }
            if (null == PendingOnTimeTimer) {
                PendingOnTimeTimer = new NcTimer ("BackEnd:PendingOnTimeTimer", state => {
                    McPending.MakeEligibleOnTime ();
                }, null, 1000, 2000);
                PendingOnTimeTimer.Stfu = true;
            }
            NcTask.Run (() => {
                ApplyAcrossServices (accountId, "Start", (service) => {
                    service.Execute ();
                    return NcResult.OK ();
                });
            }, "Start");
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start({0}) exited", accountId);
        }

        public void CertAskResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool isOkay)
        {
            NcTask.Run (delegate {
                ApplyToService (accountId, capabilities, (service) => {
                    service.CertAskResp (isOkay);
                    return NcResult.OK ();
                });
            }, "CertAskResp");
        }

        // FIXME add capabilities.
        public void ServerConfResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool forceAutodiscovery)
        {
            NcTask.Run (delegate {
                ApplyToService (accountId, capabilities, (service) => {
                    service.ServerConfResp (forceAutodiscovery);
                    return NcResult.OK ();
                });
            }, "ServerConfResp");
        }

        public void CredResp (int accountId)
        {
            NcTask.Run (() => {
                // Let every service know about the new creds.
                ApplyAcrossServices (accountId, "CredResp", (service) => {
                    service.CredResp ();
                    return NcResult.OK ();
                });
                lock (CredReqActive) {
                    CredReqActive.Remove (accountId);
                }
            }, "CredResp");
        }

       private NcResult CmdInDoNotDelayContext (int accountId, McAccount.AccountCapabilityEnum capability, Func<NcProtoControl, NcResult> cmd)
        {
            return ApplyToService (accountId, capability, (service) => {
                if (NcCommStatus.Instance.Status == NetStatusStatusEnum.Down) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
                }
                if (NcCommStatus.Instance.Quality (service.Server.Id) == NcCommStatus.CommQualityEnum.Unusable) {
                    return NcResult.Error (NcResult.SubKindEnum.Info_ServiceUnavailable);
                }
                return cmd (service);
            });
        }

        // Commands need to do Task.Run as appropriate in protocol controller.
        public NcResult StartSearchEmailReq (int accountId, string keywords, uint? maxResults)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.StartSearchEmailReq (keywords, maxResults));
        }

        public NcResult SearchEmailReq (int accountId, string keywords, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SearchEmailReq (keywords, maxResults, token));
        }

        public NcResult StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.StartSearchContactsReq (prefix, maxResults));
        }

        public NcResult SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.SearchContactsReq (prefix, maxResults, token));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.SendEmailCmd (emailMessageId));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SendEmailCmd (emailMessageId, calId));
        }

        public NcResult ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                                         int folderId, bool originalEmailIsEmbedded)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.ForwardEmailCmd (newEmailMessageId, forwardedEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

        public NcResult ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                                       int folderId, bool originalEmailIsEmbedded)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.ReplyEmailCmd (newEmailMessageId, repliedToEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

       
        private List<NcResult> DeleteMultiCmd (int accountId, McAccount.AccountCapabilityEnum capability, List<int> Ids,
            Func<NcProtoControl, int, bool, NcResult> deleter)
        {
            var outer = ApplyToService (accountId, capability, (service) => {
                var retval = new List<NcResult> ();
                for (var iter = 0; iter < Ids.Count; ++iter) {
                    if (Ids.Count - 1 == iter) {
                        retval.Add (deleter (service, Ids[iter], true));
                        } else {
                        retval.Add (deleter (service, Ids[iter], false));
                    }
                }
                return NcResult.OK (retval);
            });
            return (List<NcResult>)outer.Value;
        }

        private List<NcResult> MoveMultiCmd (int accountId, McAccount.AccountCapabilityEnum capability, List<int> Ids, int destFolderId,
            Func<NcProtoControl, int, int, bool, NcResult> mover)
        {
            var outer = ApplyToService (accountId, capability, (service) => {
                var retval = new List<NcResult> ();
                for (var iter = 0; iter < Ids.Count; ++iter) {
                    if (Ids.Count - 1 == iter) {
                        retval.Add (mover (service, Ids[iter], destFolderId, true));
                    } else {
                        retval.Add (mover (service, Ids[iter], destFolderId, false));
                    }
                }
                return NcResult.OK (retval);
            });
            return (List<NcResult>)outer.Value;
        }

        public List<NcResult> DeleteEmailsCmd (int accountId, List<int> emailMessageIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, emailMessageIds, (service, id, lastInSeq) => {
                return service.DeleteEmailCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteEmailCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DeleteEmailCmd (emailMessageId));
        }

        public List<NcResult> MoveEmailsCmd (int accountId, List<int> emailMessageIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, emailMessageIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveEmailCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MoveEmailCmd (emailMessageId, destFolderId));
        }

        public NcResult DnldAttCmd (int accountId, int attId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldAttCmd (attId, doNotDelay));
            } else {
                return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldAttCmd (attId, doNotDelay));
            }
        }

        public NcResult DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            } else {
                return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            }
        }

        public NcResult CreateCalCmd (int accountId, int calId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.CreateCalCmd (calId, folderId));
        }

        public NcResult UpdateCalCmd (int accountId, int calId, bool sendBody)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.UpdateCalCmd (calId, sendBody));
        }

        public List<NcResult> DeleteCalsCmd (int accountId, List<int> calIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.CalWriter, calIds, (service, id, lastInSeq) => {
                return service.DeleteCalCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteCalCmd (int accountId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.DeleteCalCmd (calId));
        }

        public NcResult MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.MoveCalCmd (calId, destFolderId));
        }

        public List<NcResult> MoveCalsCmd (int accountId, List<int> calIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.CalWriter, calIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveCalCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.RespondEmailCmd (emailMessageId, response));
        }

        public NcResult RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.RespondCalCmd (calId, response, instance));
        }

        public NcResult DnldCalBodyCmd (int accountId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalReader, (service) => service.DnldCalBodyCmd (calId));
        }

        public NcResult ForwardCalCmd (int accountId, int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.ForwardCalCmd (newEmailMessageId, forwardedCalId, folderId));
        }

        public NcResult MarkEmailReadCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MarkEmailReadCmd (emailMessageId));
        }

        public NcResult SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                                         DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SetEmailFlagCmd (emailMessageId, flagType, 
                start, utcStart, due, utcDue));
        }

        public NcResult ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.ClearEmailFlagCmd (emailMessageId));
        }

        public NcResult MarkEmailFlagDone (int accountId, int emailMessageId,
                                           DateTime completeTime, DateTime dateCompleted)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MarkEmailFlagDone (emailMessageId,
                completeTime, dateCompleted));
        }

        public NcResult CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.CreateContactCmd (contactId, folderId));
        }

        public NcResult UpdateContactCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.UpdateContactCmd (contactId));
        }

        public List<NcResult> DeleteContactsCmd (int accountId, List<int> contactIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.ContactWriter, contactIds, (service, id, lastInSeq) => {
                return service.DeleteContactCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteContactCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.DeleteContactCmd (contactId));
        }

        public List<NcResult> MoveContactsCmd (int accountId, List<int> contactIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.ContactWriter, contactIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveContactCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.MoveContactCmd (contactId, destFolderId));
        }

        public NcResult DnldContactBodyCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.DnldContactBodyCmd (contactId));
        }

        public NcResult CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.CreateTaskCmd (taskId, folderId));
        }

        public NcResult UpdateTaskCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.UpdateTaskCmd (taskId));
        }

        public List<NcResult> DeleteTasksCmd (int accountId, List<int> taskIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.TaskWriter, taskIds, (service, id, lastInSeq) => {
                return service.DeleteTaskCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteTaskCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.DeleteTaskCmd (taskId));
        }

        public List<NcResult> MoveTasksCmd (int accountId, List<int> taskIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.TaskWriter, taskIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveTaskCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.MoveTaskCmd (taskId, destFolderId));
        }

        public NcResult DnldTaskBodyCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskReader, (service) => service.DnldTaskBodyCmd (taskId));
        }

        // TODO it is likely that we will need to use folderId to help us find the right service someday.
        // Think of the folder tree being "mounted" on the service/NcProtoControl.
        public NcResult CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.CreateFolderCmd (destFolderId, displayName, folderType));
        }

        public NcResult CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.CreateFolderCmd (DisplayName, folderType));
        }

        public NcResult DeleteFolderCmd (int accountId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DeleteFolderCmd (folderId));
        }

        public NcResult MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MoveFolderCmd (folderId, destFolderId));
        }

        public NcResult RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.RenameFolderCmd (folderId, displayName));
        }

        public NcResult SyncCmd (int accountId, int folderId)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SyncCmd (folderId));
        }

        public NcResult ValidateConfig (int accountId, McServer server, McCred cred)
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
            }
            return ApplyToService (accountId, server.Capabilities, (service) => {
                service.ValidateConfig (server, cred);
                return NcResult.OK ();
            });
        }

        public void CancelValidateConfig (int accountId)
        {
            ApplyAcrossServices (accountId, "CancelValidateConfig", (service) => {
                service.CancelValidateConfig ();
                return NcResult.OK ();
            });
        }

        public BackEndStateEnum BackEndState (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var result = ApplyToService (accountId, capabilities,
                (service) => NcResult.OK (service.BackEndState));
            return result.isOK () ? result.GetValue<BackEndStateEnum> () : BackEndStateEnum.NotYetStarted;
        }

        public AutoDInfoEnum AutoDInfo (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var result = ApplyToService (accountId, capabilities,
                (service) => NcResult.OK (service.AutoDInfo));
            return result.isOK () ? result.GetValue<AutoDInfoEnum> () : AutoDInfoEnum.Unknown;
        }

        public X509Certificate2 ServerCertToBeExamined (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return ApplyToService (accountId, capabilities, 
                (service) => NcResult.OK (service.ServerCertToBeExamined)).GetValue<X509Certificate2> ();
        }

        //
        // For IProtoControlOwner.
        //
        private void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            NcApplication.Instance.InvokeStatusIndEvent (e);
        }

        public void StatusInd (NcProtoControl sender, NcResult status)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Account = sender.Account,
                Status = status,
            });
        }

        public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = sender.Account,
                Status = status,
                Tokens = tokens,
            });
        }

        public void CredReq (NcProtoControl sender)
        {
            // If we don't already have a request from this account, record it and send it up.
            lock (CredReqActive) {
                if (CredReqActive.ContainsKey (sender.Account.Id)) {
                    return;
                }
                CredReqActive.Add (sender.Account.Id, true);
            }
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CredReq (sender.AccountId);
            });
        }

        public void ServConfReq (NcProtoControl sender, object arg)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.ServConfReq (sender.AccountId, sender.Capabilities, arg);
            });
        }

        public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CertAskReq (sender.AccountId, sender.Capabilities, certificate);
            });
        }

        public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SearchContactsResp (sender.AccountId, prefix, token);
            });
        }

        public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SendEmailResp (sender.AccountId, emailMessageId, didSend);
            });
        }
    }
}
