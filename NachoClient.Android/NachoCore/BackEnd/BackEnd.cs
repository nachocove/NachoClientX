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
using NachoCore.ActiveSync;
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
    public sealed class BackEnd : IBackEnd, IProtoControlOwner
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

        private ConcurrentDictionary<int,ProtoControl> Services;

        public IBackEndOwner Owner { set; private get; }

        private bool HasServiceFromAccountId (int accountId)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            return Services.ContainsKey (accountId);
        }

        private NcResult ServiceFromAccountId (int accountId, Func<ProtoControl, NcResult> func)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            ProtoControl protoCtrl;
            if (!Services.TryGetValue (accountId, out protoCtrl)) {
                Log.Error (Log.LOG_SYS, "ServiceFromAccountId called with bad accountId {0} @ {1}", accountId, new StackTrace ());
                return NcResult.Error (NcResult.SubKindEnum.Error_AccountDoesNotExist);
            }
            return func (protoCtrl);
        }
        // For IBackEnd.
        private BackEnd ()
        {
            // Adjust system settings.
            ServicePointManager.DefaultConnectionLimit = 25;

            Services = new ConcurrentDictionary<int, ProtoControl> ();
        }

        public void EstablishService ()
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                if (!HasServiceFromAccountId (account.Id)) {
                    EstablishService (account.Id);
                }
            }
        }

        public void Start ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "BackEnd.Start() called");
            // The callee does Task.Run.
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                Start (account.Id);
            }
        }

        // DON'T PUT Stop in a Task.Run. We want to execute as much as possible immediately.
        // Under iOS, there is a deadline. The ProtoControl's ForceStop must stop everything and
        // return without waiting.
        public void Stop ()
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                Stop (account.Id);
            }
        }

        public void Stop (int accountId)
        {
            if (!HasServiceFromAccountId (accountId)) {
                EstablishService (accountId);
            }
            ServiceFromAccountId (accountId, (service) => {
                service.ForceStop ();
                return NcResult.OK ();
            });
        }

        public void Remove (int accountId)
        {
            Stop (accountId);
            RemoveService (accountId);
        }

        public void EstablishService (int accountId)
        {
            ProtoControl service = null;
            var account = McAccount.QueryById<McAccount> (accountId);
            switch (account.AccountType) {
            case McAccount.AccountTypeEnum.Device:
                service = new ProtoControl (this, accountId);
                break;

            case McAccount.AccountTypeEnum.Exchange:
                service = new AsProtoControl (this, accountId);
                break;

            default:
                NcAssert.True (false);
                break;
            }
            Log.Info (Log.LOG_LIFECYCLE, "EstablishService {0}", accountId);
            if (!Services.TryAdd (accountId, service)) {
                // Concurrency. Another thread has jumped in and done the add.
                Log.Info (Log.LOG_LIFECYCLE, "Another thread has already called EstablishService for Account.Id {0}", accountId);
            }
        }

        // Service must be Stop()ed before calling RemoveService().
        public void RemoveService (int accountId)
        {
            ProtoControl service = null;
            if (Services.TryGetValue (accountId, out service)) {
                service.Remove ();
                Log.Info (Log.LOG_LIFECYCLE, "RemoveService {0}", accountId);
                if (!Services.TryRemove (accountId, out service)) {
                    Log.Error (Log.LOG_LIFECYCLE, "BackEnd.RemoveService({0}) could not remove service.", accountId);
                }
            } else {
                Log.Warn (Log.LOG_LIFECYCLE, "BackEnd.RemoveService({0}) could not find service.", accountId);
            }
        }

        public void Start (int accountId)
        {
            Log.Info (Log.LOG_LIFECYCLE, "BackEnd.Start({0}) called", accountId);
            NcCommStatus.Instance.Refresh ();
            if (!HasServiceFromAccountId (accountId)) {
                EstablishService (accountId);
            }
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.Execute ();
                    return NcResult.OK ();
                });
            }, "Start");
            Log.Info (Log.LOG_LIFECYCLE, "BackEnd.Start({0}) exited", accountId);
        }

        public void CertAskResp (int accountId, bool isOkay)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.CertAskResp (isOkay);
                    return NcResult.OK ();
                });
            }, "CertAskResp");
        }

        public void ServerConfResp (int accountId, bool forceAutodiscovery)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.ServerConfResp (forceAutodiscovery);
                    return NcResult.OK ();
                });
            }, "ServerConfResp");
        }

        public void CredResp (int accountId)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.CredResp ();
                    return NcResult.OK ();
                });
            }, "CredResp");
        }

        public void Cancel (int accountId, string token)
        {
            // Don't Task.Run.
            ServiceFromAccountId (accountId, (service) => {
                service.Cancel (token);
                return NcResult.OK ();
            });
        }

        public void Prioritize (int accountId, string token)
        {
            // Don't Task.Run - must be super-fast return (no network).
            ServiceFromAccountId (accountId, (service) => {
                service.Prioritize (token);
                return NcResult.OK ();
            });
        }

        // TODO - should these take Token?
        public McPending UnblockPendingCmd (int accountId, int pendingId)
        {
            McPending retval = null;
            ServiceFromAccountId (accountId, (service) => {
                retval = service.UnblockPendingCmd (pendingId);
                return NcResult.OK ();
            });
            return retval;
        }

        public McPending DeletePendingCmd (int accountId, int pendingId)
        {
            McPending retval = null;
            ServiceFromAccountId (accountId, (service) => {
                retval = service.DeletePendingCmd (pendingId);
                return NcResult.OK ();
            });
            return retval;
        }

        private NcResult CmdInDoNotDelayContext (int accountId, Func<ProtoControl, NcResult> cmd)
        {
            return ServiceFromAccountId (accountId, (service) => {
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
            return CmdInDoNotDelayContext (accountId, (service) => service.StartSearchEmailReq (keywords, maxResults));
        }

        public NcResult SearchEmailReq (int accountId, string keywords, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, (service) => service.SearchEmailReq (keywords, maxResults, token));
        }

        public NcResult StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return CmdInDoNotDelayContext (accountId, (service) => service.StartSearchContactsReq (prefix, maxResults));
        }

        public NcResult SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, (service) => service.SearchContactsReq (prefix, maxResults, token));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.SendEmailCmd (emailMessageId));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.SendEmailCmd (emailMessageId, calId));
        }

        public NcResult ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                                         int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId, (service) => service.ForwardEmailCmd (newEmailMessageId, forwardedEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

        public NcResult ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                                       int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId, (service) => service.ReplyEmailCmd (newEmailMessageId, repliedToEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

       
        private List<NcResult> DeleteMultiCmd (int accountId, List<int> Ids,
            Func<ProtoControl, int, bool, NcResult> deleter)
        {
            var outer = ServiceFromAccountId (accountId, (service) => {
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

        private List<NcResult> MoveMultiCmd (int accountId, List<int> Ids, int destFolderId,
            Func<ProtoControl, int, int, bool, NcResult> mover)
        {
            var outer = ServiceFromAccountId (accountId, (service) => {
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
            return DeleteMultiCmd (accountId, emailMessageIds, (service, id, lastInSeq) => {
                return service.DeleteEmailCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteEmailCmd (emailMessageId));
        }

        public List<NcResult> MoveEmailsCmd (int accountId, List<int> emailMessageIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, emailMessageIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveEmailCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveEmailCmd (emailMessageId, destFolderId));
        }

        public NcResult DnldAttCmd (int accountId, int attId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, (service) => service.DnldAttCmd (attId, doNotDelay));
            } else {
                return ServiceFromAccountId (accountId, (service) => service.DnldAttCmd (attId, doNotDelay));
            }
        }

        public NcResult DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            } else {
                return ServiceFromAccountId (accountId, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            }
        }

        public NcResult CreateCalCmd (int accountId, int calId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateCalCmd (calId, folderId));
        }

        public NcResult UpdateCalCmd (int accountId, int calId, bool sendBody)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateCalCmd (calId, sendBody));
        }

        public List<NcResult> DeleteCalsCmd (int accountId, List<int> calIds)
        {
            return DeleteMultiCmd (accountId, calIds, (service, id, lastInSeq) => {
                return service.DeleteCalCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteCalCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteCalCmd (calId));
        }

        public NcResult MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveCalCmd (calId, destFolderId));
        }

        public List<NcResult> MoveCalsCmd (int accountId, List<int> calIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, calIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveCalCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return ServiceFromAccountId (accountId, (service) => service.RespondEmailCmd (emailMessageId, response));
        }

        public NcResult RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return ServiceFromAccountId (accountId, (service) => service.RespondCalCmd (calId, response, instance));
        }

        public NcResult DnldCalBodyCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldCalBodyCmd (calId));
        }

        public NcResult ForwardCalCmd (int accountId, int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.ForwardCalCmd (newEmailMessageId, forwardedCalId, folderId));
        }

        public NcResult MarkEmailReadCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MarkEmailReadCmd (emailMessageId));
        }

        public NcResult SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                                         DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return ServiceFromAccountId (accountId, (service) => service.SetEmailFlagCmd (emailMessageId, flagType, 
                start, utcStart, due, utcDue));
        }

        public NcResult ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.ClearEmailFlagCmd (emailMessageId));
        }

        public NcResult MarkEmailFlagDone (int accountId, int emailMessageId,
                                           DateTime completeTime, DateTime dateCompleted)
        {
            return ServiceFromAccountId (accountId, (service) => service.MarkEmailFlagDone (emailMessageId,
                completeTime, dateCompleted));
        }

        public NcResult CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateContactCmd (contactId, folderId));
        }

        public NcResult UpdateContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateContactCmd (contactId));
        }

        public List<NcResult> DeleteContactsCmd (int accountId, List<int> contactIds)
        {
            return DeleteMultiCmd (accountId, contactIds, (service, id, lastInSeq) => {
                return service.DeleteContactCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteContactCmd (contactId));
        }

        public List<NcResult> MoveContactsCmd (int accountId, List<int> contactIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, contactIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveContactCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveContactCmd (contactId, destFolderId));
        }

        public NcResult DnldContactBodyCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldContactBodyCmd (contactId));
        }

        public NcResult CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateTaskCmd (taskId, folderId));
        }

        public NcResult UpdateTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateTaskCmd (taskId));
        }

        public List<NcResult> DeleteTasksCmd (int accountId, List<int> taskIds)
        {
            return DeleteMultiCmd (accountId, taskIds, (service, id, lastInSeq) => {
                return service.DeleteTaskCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteTaskCmd (taskId));
        }

        public List<NcResult> MoveTasksCmd (int accountId, List<int> taskIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, taskIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveTaskCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveTaskCmd (taskId, destFolderId));
        }

        public NcResult DnldTaskBodyCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldTaskBodyCmd (taskId));
        }

        public NcResult CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateFolderCmd (destFolderId, displayName, folderType));
        }

        public NcResult CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateFolderCmd (DisplayName, folderType));
        }

        public NcResult DeleteFolderCmd (int accountId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteFolderCmd (folderId));
        }

        public NcResult MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveFolderCmd (folderId, destFolderId));
        }

        public NcResult RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return ServiceFromAccountId (accountId, (service) => service.RenameFolderCmd (folderId, displayName));
        }

        public NcResult SyncCmd (int accountId, int folderId)
        {
            return CmdInDoNotDelayContext (accountId, (service) => service.SyncCmd (folderId));
        }

        public NcResult ValidateConfig (int accountId, McServer server, McCred cred)
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
            }
            return ServiceFromAccountId (accountId, (service) => {
                service.ValidateConfig (server, cred);
                return NcResult.OK ();
            });
        }

        public void CancelValidateConfig (int accountId)
        {
            ServiceFromAccountId (accountId, (service) => {
                service.CancelValidateConfig ();
                return NcResult.OK ();
            });
        }

        public BackEndStateEnum BackEndState (int accountId)
        {
            var result = ServiceFromAccountId (accountId, (service) => NcResult.OK (service.BackEndState));
            return result.isOK () ? result.GetValue<BackEndStateEnum> () : BackEndStateEnum.NotYetStarted;
        }

        public AutoDInfoEnum AutoDInfo (int accountId)
        {
            var result = ServiceFromAccountId (accountId, (service) => NcResult.OK (service.AutoDInfo));
            return result.isOK () ? result.GetValue<AutoDInfoEnum> () : AutoDInfoEnum.Unknown;
        }

        public X509Certificate2 ServerCertToBeExamined (int accountId)
        {
            return ServiceFromAccountId (accountId, (service) => NcResult.OK (service.ServerCertToBeExamined)).GetValue<X509Certificate2> ();
        }

        //
        // For IProtoControlOwner.
        //
        private void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            NcApplication.Instance.InvokeStatusIndEvent (e);
        }

        public void StatusInd (ProtoControl sender, NcResult status)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Account = sender.Account,
                Status = status,
            });
        }

        public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = sender.Account,
                Status = status,
                Tokens = tokens,
            });
        }

        public void CredReq (ProtoControl sender)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CredReq (sender.AccountId);
            });
        }

        public void ServConfReq (ProtoControl sender, object arg)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.ServConfReq (sender.AccountId, arg);
            });
        }

        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CertAskReq (sender.AccountId, certificate);
            });
        }

        public void SearchContactsResp (ProtoControl sender, string prefix, string token)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SearchContactsResp (sender.AccountId, prefix, token);
            });
        }

        public void SendEmailResp (ProtoControl sender, int emailMessageId, bool didSend)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SendEmailResp (sender.AccountId, emailMessageId, didSend);
            });
        }
    }
}
