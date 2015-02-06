using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
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

        private TResult ServiceFromAccountId<TResult> (int accountId, Func<ProtoControl, TResult> func)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            ProtoControl protoCtrl;
            if (!Services.TryGetValue (accountId, out protoCtrl)) {
                Log.Error (Log.LOG_SYS, "ServiceFromAccountId called with bad accountId {0}", accountId);
                return default (TResult);
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
            ServiceFromAccountId<bool> (accountId, (service) => {
                service.ForceStop ();
                return true;
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
            if (!Services.TryAdd (accountId, service)) {
                // Concurrency. Another thread has jumped in and done the add.
                Log.Info (Log.LOG_SYS, "Another thread has already called EstablishService for Account.Id {0}", accountId);
            }
        }

        // Service must be Stop()ed before calling RemoveService().
        private void RemoveService (int accountId)
        {
            ProtoControl service = null;
            if (Services.TryGetValue (accountId, out service)) {
                service.Remove ();
                if (!Services.TryRemove (accountId, out service)) {
                    Log.Error (Log.LOG_LIFECYCLE, "BackEnd.RemoveService({0}) could not remove service.", accountId);
                }
                var account = McAccount.QueryById<McAccount> (accountId);
                if (null != account) {
                    account.Delete ();
                } else {
                    Log.Warn (Log.LOG_LIFECYCLE, "BackEnd.RemoveService({0}) McAccount missing.", accountId);
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
                    return true;
                });
            }, "Start");
            Log.Info (Log.LOG_LIFECYCLE, "BackEnd.Start({0}) exited", accountId);
        }

        public void CertAskResp (int accountId, bool isOkay)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.CertAskResp (isOkay);
                    return true;
                });
            }, "CertAskResp");
        }

        public void ServerConfResp (int accountId, bool forceAutodiscovery)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.ServerConfResp (forceAutodiscovery);
                    return true;
                });
            }, "ServerConfResp");
        }

        public void CredResp (int accountId)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId, (service) => {
                    service.CredResp ();
                    return true;
                });
            }, "CredResp");
        }

        public void Cancel (int accountId, string token)
        {
            // Don't Task.Run.
            ServiceFromAccountId (accountId, (service) => {
                service.Cancel (token);
                return true;
            });
        }

        public void Prioritize (int accountId, string token)
        {
            // Don't Task.Run - must be super-fast return (no network).
            ServiceFromAccountId (accountId, (service) => {
                service.Prioritize (token);
                return true;
            });
        }

        // TODO - should these take Token?
        public void UnblockPendingCmd (int accountId, int pendingId)
        {
            ServiceFromAccountId (accountId, (service) => {
                service.UnblockPendingCmd (pendingId);
                return true;
            });
        }

        public void DeletePendingCmd (int accountId, int pendingId)
        {
            ServiceFromAccountId (accountId, (service) => {
                service.DeletePendingCmd (pendingId);
                return true;
            });
        }

        // Commands need to do Task.Run as appropriate in protocol controller.
        public string StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return ServiceFromAccountId (accountId, (service) => service.StartSearchContactsReq (prefix, maxResults));
        }

        public void SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            ServiceFromAccountId (accountId, (service) => {
                service.SearchContactsReq (prefix, maxResults, token);
                return true;
            });
        }

        public string SendEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.SendEmailCmd (emailMessageId));
        }

        public string SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.SendEmailCmd (emailMessageId, calId));
        }

        public string ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                                       int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId, (service) => service.ForwardEmailCmd (newEmailMessageId, forwardedEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

        public string ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                                     int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId, (service) => service.ReplyEmailCmd (newEmailMessageId, repliedToEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

        public string DeleteEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteEmailCmd (emailMessageId));
        }

        public string MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveEmailCmd (emailMessageId, destFolderId));
        }

        public string DnldAttCmd (int accountId, int attId, bool doNotDefer = false)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldAttCmd (attId, doNotDefer));
        }

        public string DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDefer = false)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDefer));
        }

        public string CreateCalCmd (int accountId, int calId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateCalCmd (calId, folderId));
        }

        public string UpdateCalCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateCalCmd (calId));
        }

        public string DeleteCalCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteCalCmd (calId));
        }

        public string MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveCalCmd (calId, destFolderId));
        }

        public string RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return ServiceFromAccountId (accountId, (service) => service.RespondEmailCmd (emailMessageId, response));
        }

        public string RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return ServiceFromAccountId (accountId, (service) => service.RespondCalCmd (calId, response, instance));
        }

        public string DnldCalBodyCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldCalBodyCmd (calId));
        }

        public string MarkEmailReadCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MarkEmailReadCmd (emailMessageId));
        }

        public string SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                                       DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return ServiceFromAccountId (accountId, (service) => service.SetEmailFlagCmd (emailMessageId, flagType, 
                start, utcStart, due, utcDue));
        }

        public string ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId, (service) => service.ClearEmailFlagCmd (emailMessageId));
        }

        public string MarkEmailFlagDone (int accountId, int emailMessageId,
                                         DateTime completeTime, DateTime dateCompleted)
        {
            return ServiceFromAccountId (accountId, (service) => service.MarkEmailFlagDone (emailMessageId,
                completeTime, dateCompleted));
        }

        public string CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateContactCmd (contactId, folderId));
        }

        public string UpdateContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateContactCmd (contactId));
        }

        public string DeleteContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteContactCmd (contactId));
        }

        public string MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveContactCmd (contactId, destFolderId));
        }

        public string DnldContactBodyCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldContactBodyCmd (contactId));
        }

        public string CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateTaskCmd (taskId, folderId));
        }

        public string UpdateTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.UpdateTaskCmd (taskId));
        }

        public string DeleteTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteTaskCmd (taskId));
        }

        public string MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveTaskCmd (taskId, destFolderId));
        }

        public string DnldTaskBodyCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DnldTaskBodyCmd (taskId));
        }

        public string CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateFolderCmd (destFolderId, displayName, folderType));
        }

        public string CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId, (service) => service.CreateFolderCmd (DisplayName, folderType));
        }

        public string DeleteFolderCmd (int accountId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.DeleteFolderCmd (folderId));
        }

        public string MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.MoveFolderCmd (folderId, destFolderId));
        }

        public string RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return ServiceFromAccountId (accountId, (service) => service.RenameFolderCmd (folderId, displayName));
        }

        public string SyncCmd (int accountId, int folderId)
        {
            return ServiceFromAccountId (accountId, (service) => service.SyncCmd (folderId));
        }

        public bool ValidateConfig (int accountId, McServer server, McCred cred)
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return false;
            }
            return ServiceFromAccountId (accountId, (service) => {
                service.ValidateConfig (server, cred);
                return true;
            });
        }

        public void CancelValidateConfig (int accountId)
        {
            ServiceFromAccountId (accountId, (service) => {
                service.CancelValidateConfig ();
                return true;
            });
        }

        public BackEndStateEnum BackEndState (int accountId)
        {
            return ServiceFromAccountId (accountId, (service) => service.BackEndState);
        }

        public AutoDInfoEnum AutoDInfo (int accountId)
        {
            return ServiceFromAccountId (accountId, (service) => service.AutoDInfo);
        }

        public X509Certificate2 ServerCertToBeExamined (int accountId)
        {
            return ServiceFromAccountId (accountId, (service) => service.ServerCertToBeExamined);
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

        public void ServConfReq (ProtoControl sender)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.ServConfReq (sender.AccountId);
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
    }
}
