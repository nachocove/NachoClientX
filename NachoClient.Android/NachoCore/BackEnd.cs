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
    public class BackEnd : IBackEnd, IProtoControlOwner
    {

        private static readonly BackEnd instance = new BackEnd();

        public static BackEnd Instance
        {
            get 
            {
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

        public SQLiteConnectionWithEvents Db { set; get; }

        public string AttachmentsDir { set; get; }

        private List<ProtoControl> Services;
        private string DbFileName;

        public IBackEndOwner Owner { set; private get; }

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
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            AttachmentsDir = Path.Combine (documents, "attachments");
            Directory.CreateDirectory (Path.Combine (documents, AttachmentsDir));
            DbFileName = Path.Combine (documents, "db");
            Db = new SQLiteConnectionWithEvents (DbFileName, storeDateTimeAsTicks: true);
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
 
            Services = new List<ProtoControl> ();

            ServicePointManager.DefaultConnectionLimit = 8;
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

        //
        // For IProtoControlOwner.
        //
        public void StatusInd (ProtoControl sender, NcResult status)
        {
            Owner.StatusInd (sender.Account, status);
        }

        public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
        {
            Owner.StatusInd (sender.Account, status, tokens);
        }

        public void CredReq (ProtoControl sender)
        {
            Owner.CredReq (sender.Account);
        }

        public void ServConfReq (ProtoControl sender)
        {
            Owner.ServConfReq (sender.Account);
        }

        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate)
        {
            Owner.CertAskReq (sender.Account, certificate);
        }

        public void SearchContactsResp (ProtoControl sender, string prefix, string token)
        {
            Owner.SearchContactsResp (sender.Account, prefix, token);
        }
    }
}
