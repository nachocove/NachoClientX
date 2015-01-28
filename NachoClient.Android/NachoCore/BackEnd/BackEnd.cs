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

        private ProtoControl ServiceFromAccountId (int accountId)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            ProtoControl protoCtrl;
            if (!Services.TryGetValue (accountId, out protoCtrl)) {
                return null;
            }
            return protoCtrl;
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
            var service = ServiceFromAccountId (accountId);
            service.ForceStop ();
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
            NcTask.Run (delegate {
                NcCommStatus.Instance.Refresh ();
                if (!HasServiceFromAccountId (accountId)) {
                    EstablishService (accountId);
                }
                ServiceFromAccountId (accountId).Execute ();
            }, "Start");
        }

        public void CertAskResp (int accountId, bool isOkay)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId).CertAskResp (isOkay);
            }, "CertAskResp");
        }

        public void ServerConfResp (int accountId, bool forceAutodiscovery)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId).ServerConfResp (forceAutodiscovery);
            }, "ServerConfResp");
        }

        public void CredResp (int accountId)
        {
            NcTask.Run (delegate {
                ServiceFromAccountId (accountId).CredResp ();
            }, "CredResp");
        }

        public void Cancel (int accountId, string token)
        {
            // Don't Task.Run.
            ServiceFromAccountId (accountId).Cancel (token);
        }

        public void Prioritize (int accountId, string token)
        {
            // Don't Task.Run - must be super-fast return (no network).
            ServiceFromAccountId (accountId).Prioritize (token);
        }

        // TODO - should these take Token?
        public void UnblockPendingCmd (int accountId, int pendingId)
        {
            ServiceFromAccountId (accountId).UnblockPendingCmd (pendingId);
        }

        public void DeletePendingCmd (int accountId, int pendingId)
        {
            ServiceFromAccountId (accountId).DeletePendingCmd (pendingId);
        }

        // Commands need to do Task.Run as appropriate in protocol controller.
        public string StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return ServiceFromAccountId (accountId).StartSearchContactsReq (prefix, maxResults);
        }

        public void SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            ServiceFromAccountId (accountId).SearchContactsReq (prefix, maxResults, token);
        }

        public string SendEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId).SendEmailCmd (emailMessageId);
        }

        public string SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return ServiceFromAccountId (accountId).SendEmailCmd (emailMessageId, calId);
        }

        public string ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                                       int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId).ForwardEmailCmd (newEmailMessageId, forwardedEmailMessageId,
                folderId, originalEmailIsEmbedded);
        }

        public string ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                                     int folderId, bool originalEmailIsEmbedded)
        {
            return ServiceFromAccountId (accountId).ReplyEmailCmd (newEmailMessageId, repliedToEmailMessageId,
                folderId, originalEmailIsEmbedded);
        }

        public string DeleteEmailCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId).DeleteEmailCmd (emailMessageId);
        }

        public string MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveEmailCmd (emailMessageId, destFolderId);
        }

        public string DnldAttCmd (int accountId, int attId, bool doNotDefer = false)
        {
            return ServiceFromAccountId (accountId).DnldAttCmd (attId, doNotDefer);
        }

        public string DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDefer = false)
        {
            return ServiceFromAccountId (accountId).DnldEmailBodyCmd (emailMessageId, doNotDefer);
        }

        public string CreateCalCmd (int accountId, int calId, int folderId)
        {
            return ServiceFromAccountId (accountId).CreateCalCmd (calId, folderId);
        }

        public string UpdateCalCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId).UpdateCalCmd (calId);
        }

        public string DeleteCalCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId).DeleteCalCmd (calId);
        }

        public string MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveCalCmd (calId, destFolderId);
        }

        public string RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return ServiceFromAccountId (accountId).RespondEmailCmd (emailMessageId, response);
        }

        public string RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return ServiceFromAccountId (accountId).RespondCalCmd (calId, response, instance);
        }

        public string DnldCalBodyCmd (int accountId, int calId)
        {
            return ServiceFromAccountId (accountId).DnldCalBodyCmd (calId);
        }

        public string MarkEmailReadCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId).MarkEmailReadCmd (emailMessageId);
        }

        public string SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                                       DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return ServiceFromAccountId (accountId).SetEmailFlagCmd (emailMessageId, flagType, 
                start, utcStart, due, utcDue);
        }

        public string ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return ServiceFromAccountId (accountId).ClearEmailFlagCmd (emailMessageId);
        }

        public string MarkEmailFlagDone (int accountId, int emailMessageId,
                                         DateTime completeTime, DateTime dateCompleted)
        {
            return ServiceFromAccountId (accountId).MarkEmailFlagDone (emailMessageId,
                completeTime, dateCompleted);
        }

        public string CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return ServiceFromAccountId (accountId).CreateContactCmd (contactId, folderId);
        }

        public string UpdateContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId).UpdateContactCmd (contactId);
        }

        public string DeleteContactCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId).DeleteContactCmd (contactId);
        }

        public string MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveContactCmd (contactId, destFolderId);
        }

        public string DnldContactBodyCmd (int accountId, int contactId)
        {
            return ServiceFromAccountId (accountId).DnldContactBodyCmd (contactId);
        }

        public string CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return ServiceFromAccountId (accountId).CreateTaskCmd (taskId, folderId);
        }

        public string UpdateTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId).UpdateTaskCmd (taskId);
        }

        public string DeleteTaskCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId).DeleteTaskCmd (taskId);
        }

        public string MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveTaskCmd (taskId, destFolderId);
        }

        public string DnldTaskBodyCmd (int accountId, int taskId)
        {
            return ServiceFromAccountId (accountId).DnldTaskBodyCmd (taskId);
        }

        public string CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId).CreateFolderCmd (destFolderId, displayName, folderType);
        }

        public string CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ServiceFromAccountId (accountId).CreateFolderCmd (DisplayName, folderType);
        }

        public string DeleteFolderCmd (int accountId, int folderId)
        {
            return ServiceFromAccountId (accountId).DeleteFolderCmd (folderId);
        }

        public string MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveFolderCmd (folderId, destFolderId);
        }

        public string RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return ServiceFromAccountId (accountId).RenameFolderCmd (folderId, displayName);
        }

        public string SyncCmd (int accountId, int folderId)
        {
            return ServiceFromAccountId (accountId).SyncCmd (folderId);
        }

        public bool ValidateConfig (int accountId, McServer server, McCred cred)
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return false;
            }
            ServiceFromAccountId (accountId).ValidateConfig (server, cred);
            return true;
        }

        public void CancelValidateConfig (int accountId)
        {
            ServiceFromAccountId (accountId).CancelValidateConfig ();
        }

        public BackEndStateEnum BackEndState (int accountId)
        {
            return ServiceFromAccountId (accountId).BackEndState;
        }

        public AutoDInfoEnum AutoDInfo (int accountId)
        {
            return ServiceFromAccountId (accountId).AutoDInfo;
        }

        public X509Certificate2 ServerCertToBeExamined (int accountId)
        {
            return ServiceFromAccountId (accountId).ServerCertToBeExamined;
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
