using System;
using System.Collections.Generic;
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
                        if (instance == null)
                            instance = new BackEnd ();
                    }
                }
                return instance; 
            }
        }

        public enum DbActors
        {
            Ui,
            Proto}
        ;

        public enum DbEvents
        {
            DidWrite,
            WillDelete}
        ;

        public event EventHandler StatusIndEvent;

        public SQLiteConnection Db { set; get; }

        public string AttachmentsDir { set; get; }

        private List<ProtoControl> Services;
        private string DbFileName;

        public IBackEndOwner Owner { set; private get; }

        private const string ClientOwned_Outbox = "Outbox2";
        private const string ClientOwned_GalCache = "GAL";
        private const string ClientOwned_Gleaned = "GLEANED";

        private ProtoControl ServiceFromAccountId (int accountId)
        {
            var query = Services.Where (ctrl => ctrl.Account.Id.Equals (accountId));
            if (!Services.Any ()) {
                return null;
            }
            return query.Single ();
        }
        // For IBackEnd.
        private BackEnd ()
        {
            // Make sure DB is setup.
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            AttachmentsDir = Path.Combine (documents, "attachments");
            Directory.CreateDirectory (Path.Combine (documents, AttachmentsDir));
            DbFileName = Path.Combine (documents, "db");
            Db = new SQLiteConnection (DbFileName, 
                SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex, 
                storeDateTimeAsTicks: true);
            Db.CreateTable<McAccount> ();
            Db.CreateTable<McCred> ();
            Db.CreateTable<McMapFolderItem> ();
            Db.CreateTable<McFolder> ();
            Db.CreateTable<McEmailMessage> ();
            Db.CreateTable<McAttachment> ();
            Db.CreateTable<McContact> ();
            Db.CreateTable<McContactDateAttribute> ();
            Db.CreateTable<McContactStringAttribute> ();
            Db.CreateTable<McContactAddressAttribute> ();
            Db.CreateTable<McPolicy> ();
            Db.CreateTable<McProtocolState> ();
            Db.CreateTable<McServer> ();
            Db.CreateTable<McPending> ();
            Db.CreateTable<McCalendar> ();
            Db.CreateTable<McException> ();
            Db.CreateTable<McAttendee> ();
            Db.CreateTable<McCalendarCategory> ();
            Db.CreateTable<McRecurrence> ();
            Db.CreateTable<McTimeZone> ();
            Db.CreateTable<McBody> ();
 
            // Adjust system settings.
            ServicePointManager.DefaultConnectionLimit = 8;

            Services = new List<ProtoControl> ();
        }

        public void Start ()
        {
            // The callee does Task.Run.
            var accounts = Db.Table<McAccount> ();
            foreach (var account in accounts) {
                Start (account.Id);
            }
        }

        public void Stop ()
        {
            // Don't Task.Run.
            var accounts = Db.Table<McAccount> ();
            foreach (var account in accounts) {
                Stop (account.Id);
            }
        }

        public void Stop (int accountId)
        {
            // Don't Task.Run.
            var service = ServiceFromAccountId (accountId);
            service.ForceStop ();
        }

        public void Start (int accountId)
        {
            Task.Run (delegate {
                NcCommStatus.Instance.Refresh ();
                var service = ServiceFromAccountId (accountId);
                if (null == service) {
                    /* NOTE: This code needs to be able to detect the account type and start the 
                 * appropriate control (not just AS).
                 */
                    service = new AsProtoControl (this, accountId);
                    Services.Add (service);
                    // Create client owned objects as needed.
                    if (null == GetOutbox (accountId)) {
                        McFolder.Create (accountId, true, false, "0",
                            ClientOwned_Outbox, ClientOwned_Outbox,
                            (uint)Xml.FolderHierarchy.TypeCode.UserCreatedMail);
                    }
                    if (null == GetGalCache (accountId)) {
                        McFolder.Create (accountId, true, true, "0",
                            ClientOwned_GalCache, string.Empty,
                            (uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts);
                    }
                    if (null == GetGleaned (accountId)) {
                        McFolder.Create (accountId, true, true, "0",
                            ClientOwned_Gleaned, string.Empty,
                            (uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts);
                    }
                }
                service.Execute ();
            });
        }

        public void ForceSync ()
        {
            // The callee does Task.Run.
            var accounts = Db.Table<McAccount> ();
            foreach (var account in accounts) {
                ForceSync (account.Id);
            }
        }

        public void ForceSync (int accountId)
        {
            Task.Run (delegate {
                NcCommStatus.Instance.Refresh ();
                ServiceFromAccountId (accountId).ForceSync ();
            });
        }

        public void CertAskResp (int accountId, bool isOkay)
        {
            Task.Run (delegate {
                ServiceFromAccountId (accountId).CertAskResp (isOkay);
            });
        }

        public void ServerConfResp (int accountId, bool forceAutodiscovery)
        {
            Task.Run (delegate {
                ServiceFromAccountId (accountId).ServerConfResp (forceAutodiscovery);
            });
        }

        public void CredResp (int accountId)
        {
            Task.Run (delegate {
                ServiceFromAccountId (accountId).CredResp ();
            });
        }

        public bool Cancel (int accountId, string token)
        {
            // Don't Task.Run.
            return ServiceFromAccountId (accountId).Cancel (token);
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

        public string MoveItemCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ServiceFromAccountId (accountId).MoveItemCmd (emailMessageId, destFolderId);
        }

        public string DnldAttCmd (int accountId, int attId)
        {
            return ServiceFromAccountId (accountId).DnldAttCmd (attId);
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

        public string CreateFolderCmd (int accountId, int destFolderId, string displayName, uint folderType,
                                 bool IsClientOwned, bool isHidden)
        {
            return ServiceFromAccountId (accountId).CreateFolderCmd (destFolderId, displayName, folderType,
                IsClientOwned, isHidden);
        }

        public string CreateFolderCmd (int accountId, string DisplayName, uint folderType,
                                 bool IsClientOwned, bool isHidden)
        {
            return ServiceFromAccountId (accountId).CreateFolderCmd (DisplayName, folderType,
                IsClientOwned, isHidden);
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

        private McFolder GetClientOwned (int accountId, string serverId)
        {
            return BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => 
                accountId == x.AccountId &&
            serverId == x.ServerId &&
            true == x.IsClientOwned);
        }

        public McFolder GetOutbox (int accountId)
        {
            return GetClientOwned (accountId, ClientOwned_Outbox);
        }

        public McFolder GetGalCache (int accountId)
        {
            return GetClientOwned (accountId, ClientOwned_GalCache);
        }

        public McFolder GetGleaned (int accountId)
        {
            return GetClientOwned (accountId, ClientOwned_Gleaned);
        }
        //
        // For IProtoControlOwner.
        //
        private void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            if (null != StatusIndEvent) {
                InvokeOnUIThread.Instance.Invoke (delegate() {
                    StatusIndEvent.Invoke (this, e);
                });
            }
        }

        public void StatusInd (ProtoControl sender, NcResult status)
        {
            try {
                InvokeOnUIThread.Instance.Invoke (delegate() {
                    Owner.StatusInd (sender.AccountId, status);
                });
                InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Account = sender.Account,
                    Status = status,
                });
            } catch (Exception e) {
                Log.Error (Log.LOG_AS, "Exception in status recipient: {0}", e.ToString ());
            }
        }

        public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
        {
            try {
                InvokeOnUIThread.Instance.Invoke (delegate() {
                    Owner.StatusInd (sender.AccountId, status, tokens);
                });
                InvokeStatusIndEvent (new StatusIndEventArgs () {
                    Account = sender.Account,
                    Status = status,
                    Tokens = tokens,
                });
            } catch (Exception e) {
                Log.Error (Log.LOG_AS, "Exception in status recipient: {0}", e.ToString ());
            }
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
