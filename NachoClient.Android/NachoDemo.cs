using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
	public class NachoDemo : IBackEndOwner
	{
		private BackEnd Be { get; set;}
		private NcAccount Account { get; set; }

		public NachoDemo ()
		{
			// Register to receive DB update indications.
			NcEventable.DbEvent += (BackEnd.DbActors dbActor, BackEnd.DbEvents dbEvent, NcEventable target, EventArgs e) => {
				if (BackEnd.DbActors.Ui != dbActor) {
					Console.WriteLine("DB Event {1} on {0}", target.ToString(), dbEvent.ToString());
				}
			};

			// There is one back-end object covering all protocols and accounts. It does not go in the DB.
			// It manages everything while the app is running.
			Be = new BackEnd (this);
			if (0 == Be.Db.Table<NcAccount> ().Count ()) {
				EnterFullConfiguration ();
			} else {
				Account = Be.Db.Table<NcAccount> ().First ();
			}
			Be.Start ();
			TrySend ();
			//TryDelete ();
		}

		private void EnterFullConfiguration () {
			// You will always need to supply user credentials (until certs, for sure).
			var cred = new NcCred () { Username = "jeffe@nachocove.com", Password = "D0ggie789" };
			Be.Db.Insert (BackEnd.DbActors.Ui, cred);
			// In the near future, you won't need to create this protocol state object.
			var protocolState = new NcProtocolState ();
			Be.Db.Insert (BackEnd.DbActors.Ui, protocolState);
			// You will always need to supply the user's email address.
			Account = new NcAccount () { EmailAddr = "jeffe@nachocove.com" };
			// The account object is the "top", pointing to credential, server, and opaque protocol state.
			Account.CredId = cred.Id;
			Account.ProtocolStateId = protocolState.Id;
			Be.Db.Insert (BackEnd.DbActors.Ui, Account);
            /* */
            var server = new NcServer () { Fqdn = "m.google.com" };
            Be.Db.Insert (BackEnd.DbActors.Ui, server);
            Account.ServerId = server.Id;
            Be.Db.Update (BackEnd.DbActors.Ui, Account); 
		}
		public void TryDelete () {
			if (0 != Be.Db.Table<NcEmailMessage> ().Count ()) {
				var dead = Be.Db.Table<NcEmailMessage> ().First ();
				Be.Db.Delete (BackEnd.DbActors.Ui, dead);
			}
		}

		public void TrySend () {
			var email = new NcEmailMessage () {
				AccountId = Account.Id,
				To = "jeff.enderwick@gmail.com",
				From = "jeffe@nachocove.com",
				Subject = "test",
				Body = "this is a simple test.",
				IsAwatingSend = true
			};
			Be.Db.Insert(BackEnd.DbActors.Ui, email);
		}
		// Methods for IBackEndDelegate:
		public void CredReq(NcAccount account) {
		}
		public void ServConfReq (NcAccount account) {
			// Will change - needed for current autodiscover flow.
            var server = new NcServer () { Fqdn = "nco9.com" };
            Be.Db.Insert (BackEnd.DbActors.Ui, server);
            account.ServerId = server.Id;
			Be.Db.Update (BackEnd.DbActors.Ui, account);
            Be.ServerConfResp (account);
		}
        public void CertAskReq (NcAccount account, X509Certificate2 certificate)
        {
            Be.CertAskResp (account, true);
        }

        public void CertAskReq (NcAccount account) {
        }
		public void HardFailInd (NcAccount account) {
		}
		public void SoftFailInd (NcAccount account) {
		}
		public bool RetryPermissionReq (NcAccount account, uint delaySeconds) {
			return true;
		}
		public void ServerOOSpaceInd (NcAccount account) {
		}
	}
}

