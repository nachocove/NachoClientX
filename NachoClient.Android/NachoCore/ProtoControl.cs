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

        public NcAccount Account {
            get {
                return Owner.Db.Table<NcAccount> ().Where (acc => acc.Id == AccountId).Single ();
            }
        }

        public NcCred Cred {
            get {
                // Note the lack of join :-(.
                var account = Account;
                return Owner.Db.Table<NcCred> ().Where (crd => crd.Id == Account.CredId).Single ();
            }
        }

        public NcServer Server { 
            get {
                var account = Account;
                return Owner.Db.Table<NcServer> ().Where (srv => srv.Id == Account.ServerId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ServerId;
                Owner.Db.Update (BackEnd.DbActors.Proto, update);
            }
        }

        public NcProtocolState ProtocolState { 
            get {
                var account = Account;
                return Owner.Db.Table<NcProtocolState> ().Where (pcs => pcs.Id == Account.ProtocolStateId).Single ();
            }
            set {
                var update = value;
                update.Id = Account.ProtocolStateId;
                Owner.Db.Update (BackEnd.DbActors.Proto, update);
            }
        }

        public StateMachine Sm { set; get; }

        public abstract void Execute ();
        public abstract void CertAskResp (bool isOkay);
        public abstract void ServerConfResp ();
        public abstract void CredResp ();
    }
}
