using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
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

        private ProtoControl ServiceFromAccount (McAccount account)
        {
            var query = Services.Where (ctrl => ctrl.Account.Id.Equals (account.Id));
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
            Db.CreateTable<McPendingUpdate> ();
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
            var accounts = Db.Table<McAccount> ();
            foreach (var account in accounts) {
                Start (account);
            }
        }

        public void Start (McAccount account)
        {
            var service = ServiceFromAccount (account);
            if (null == service) {
                /* NOTE: This code needs to be able to detect the account type and start the 
                 * appropriate control (not just AS).
                 */
                service = new AsProtoControl (this, account);
                Services.Add (service);
                // Create client owned objects as needed.
                if (null == GetOutbox (account.Id)) {
                    var outbox = McFolder.CreateClientOwned (account);
                    outbox.IsHidden = false;
                    outbox.ParentId = "0";
                    outbox.ServerId = ClientOwned_Outbox;
                    outbox.DisplayName = ClientOwned_Outbox;
                    outbox.Type = (uint)Xml.FolderHierarchy.TypeCode.UserCreatedMail;
                    BackEnd.Instance.Db.Insert (outbox);
                }
                if (null == GetGalCache (account.Id)) {
                    var galCache = McFolder.CreateClientOwned (account);
                    galCache.IsHidden = true;
                    galCache.ParentId = "0";
                    galCache.ServerId = ClientOwned_GalCache;
                    galCache.Type = (uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts;
                    BackEnd.Instance.Db.Insert (galCache);
                }
                if (null == GetGleaned (account.Id)) {
                    var gleaned = McFolder.CreateClientOwned (account);
                    gleaned.IsHidden = true;
                    gleaned.ParentId = "0";
                    gleaned.ServerId = ClientOwned_Gleaned;
                    gleaned.Type = (uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts;
                    BackEnd.Instance.Db.Insert (gleaned);
                }
            }
            NcCommStatus.Instance.Reset (account.ServerId);
            service.Execute ();
        }

        public void CertAskResp (McAccount account, bool isOkay)
        {
            ServiceFromAccount (account).CertAskResp (isOkay);
        }

        public void ServerConfResp (McAccount account)
        {
            ServiceFromAccount (account).ServerConfResp ();
        }

        public void CredResp (McAccount account)
        {
            ServiceFromAccount (account).CredResp ();
        }

        public bool Cancel (McAccount account, string token)
        {
            return ServiceFromAccount (account).Cancel (token);
        }

        public string StartSearchContactsReq (McAccount account, string prefix, uint? maxResults)
        {
            return ServiceFromAccount (account).StartSearchContactsReq (prefix, maxResults);
        }

        public void SearchContactsReq (McAccount account, string prefix, uint? maxResults, string token)
        {
            ServiceFromAccount (account).SearchContactsReq (prefix, maxResults, token);
        }

        public string SendEmailCmd (McAccount account, int emailMessageId)
        {
            return ServiceFromAccount (account).SendEmailCmd (emailMessageId);
        }

        public string DeleteEmailCmd (McAccount account, int emailMessageId)
        {
            return ServiceFromAccount (account).DeleteEmailCmd (emailMessageId);
        }

        public string MoveItemCmd (McAccount account, int emailMessageId, int destFolderId)
        {
            return ServiceFromAccount (account).MoveItemCmd (emailMessageId, destFolderId);
        }

        public string DnldAttCmd (McAccount account, int attId)
        {
            return ServiceFromAccount (account).DnldAttCmd (attId);
        }

        public string MarkEmailReadCmd (McAccount account, int emailMessageId)
        {
            return ServiceFromAccount (account).MarkEmailReadCmd (emailMessageId);
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
                Owner.StatusInd (sender.Account, status);
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
                Owner.StatusInd (sender.Account, status, tokens);
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
                Owner.CredReq (sender.Account);
            });
        }

        public void ServConfReq (ProtoControl sender)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.ServConfReq (sender.Account);
            });
        }

        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CertAskReq (sender.Account, certificate);
            });
        }

        public void SearchContactsResp (ProtoControl sender, string prefix, string token)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SearchContactsResp (sender.Account, prefix, token);
            });
        }
    }
}
