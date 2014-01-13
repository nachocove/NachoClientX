using System;
using System.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public abstract class ProtoControl
    {
        protected int AccountId;
        public IProtoControlOwner Owner { get; set; }

        public McAccount Account {
            get {
                return Owner.Db.Table<McAccount> ().Where (acc => acc.Id == AccountId).Single ();
            }
        }

        public McCred Cred {
            get {
                // Note the lack of join :-(.
                var account = Account;
                return Owner.Db.Table<McCred> ().Where (crd => crd.Id == Account.CredId).Single ();
            }
        }

        public McServer Server { 
            get {
                var account = Account;
                return Owner.Db.Table<McServer> ().Where (srv => srv.Id == Account.ServerId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ServerId;
                Owner.Db.Update (update);
            }
        }

        public McProtocolState ProtocolState { 
            get {
                var account = Account;
                return Owner.Db.Table<McProtocolState> ().Where (pcs => pcs.Id == Account.ProtocolStateId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ProtocolStateId;
                Owner.Db.Update (update);
            }
        }

        public StateMachine Sm { set; get; }

        // Interface to owner.
        public abstract void Execute ();
        public abstract void CertAskResp (bool isOkay);
        public abstract void ServerConfResp ();
        public abstract void CredResp ();
        public abstract bool Cancel (string token);
        public abstract string StartSearchContactsReq (string prefix, uint? maxResults);
        public abstract void SearchContactsReq (string prefix, uint? maxResults, string token);
        public abstract string SendEmailCmd (int emailMessageId);
        public abstract string DeleteEmailCmd (int emailMessageId);
        // Interface to controllers.
        public abstract void StatusInd (NcResult status);
        public abstract void StatusInd (NcResult status, string[] tokens);
    }
}
