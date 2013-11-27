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
        public enum DbActors {Ui, Proto};
        public enum DbEvents {DidWrite, WillDelete};

        public SQLiteConnectionWithEvents Db { set; get; }
        public string AttachmentsDir { set; get; }

        private List<ProtoControl> Services;
        private IBackEndOwner Owner;
        private string DbFileName;

        private ProtoControl ServiceFromAccount (NcAccount account)
        {
            var query = Services.Where (ctrl => ctrl.Account.Id.Equals (account.Id));
            if (! Services.Any ()) {
                return null;
            }
            return query.Single ();
        }

        // For IBackEnd.

        public BackEnd (IBackEndOwner owner) {
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            AttachmentsDir = Path.Combine (documents, "attachments");
            Directory.CreateDirectory (Path.Combine (documents, AttachmentsDir));
            DbFileName = Path.Combine (documents, "db");
            Db = new SQLiteConnectionWithEvents(DbFileName);
            Db.CreateTable<NcAccount> ();
            Db.CreateTable<NcCred> ();
            Db.CreateTable<NcFolder> ();
            Db.CreateTable<NcEmailMessage> ();
            Db.CreateTable<NcAttachment> ();
            Db.CreateTable<NcContact> ();
            Db.CreateTable<NcPolicy> ();
            Db.CreateTable<NcProtocolState> ();
            Db.CreateTable<NcServer> ();
            Db.CreateTable<NcPendingUpdate> ();

            Services = new List<ProtoControl> ();

            Owner = owner;

            ServicePointManager.DefaultConnectionLimit = 8;
        }

        public void Start () {
            var accounts = Db.Table<NcAccount> ();
            foreach (var account in accounts) {
                Start (account);
            }
        }

        public void Start (NcAccount account) {
            var service = ServiceFromAccount (account);
            if (null == service) {
                /* NOTE: This code needs to be able to detect the account type and start the 
                 * appropriate control (not just AS).
                 */
                service = new AsProtoControl (this, account);
                Services.Add (service);
            }
            service.Execute ();
        }

        public void CertAskResp (NcAccount account, bool isOkay)
        {
            ServiceFromAccount (account).CertAskResp (isOkay);
        }

        public void ServerConfResp (NcAccount account)
        {
            ServiceFromAccount (account).ServerConfResp ();
        }

        public void CredResp (NcAccount account)
        {
            ServiceFromAccount (account).CredResp ();
        }

        // For IProtoControlOwner.

        public void CredReq (ProtoControl sender) {
            Owner.CredReq (sender.Account);
        }

        public void ServConfReq (ProtoControl sender) {
            Owner.ServConfReq (sender.Account);
        }

        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate) {
            Owner.CertAskReq (sender.Account, certificate);
        }

        public void HardFailInd (ProtoControl sender) {
            Owner.HardFailInd (sender.Account);
        }

        public void TempFailInd (ProtoControl sender) {
            Owner.HardFailInd (sender.Account);
        }

        public bool RetryPermissionReq (ProtoControl sender, uint delaySeconds) {
            return Owner.RetryPermissionReq (sender.Account, delaySeconds);
        }

        public void ServerOOSpaceInd (ProtoControl sender) {
            Owner.ServerOOSpaceInd (sender.Account);
        }
    }
}
