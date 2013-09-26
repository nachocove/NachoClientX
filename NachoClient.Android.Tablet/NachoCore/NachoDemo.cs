using System;
using System.Linq;
using NachoCore;
using NachoCore.Model;

namespace NachoCore
{
	public class NachoDemo : IBackEndDelegate
	{
		private BackEnd Be { get; set;}
		public NachoDemo ()
		{

			// Register to receive DB update indications.
			NcEventable.DidWriteToDb += (BackEnd.Actors actor, NcEventable target, EventArgs e) => {
				if (BackEnd.Actors.Ui != actor) {
					Console.WriteLine("{0} written", target.ToString());
				}
			};
			NcEventable.WillDeleteFromDb += (BackEnd.Actors actor, NcEventable target, EventArgs e) => {
				if (BackEnd.Actors.Ui != actor) {
					Console.WriteLine("{0} deleted", target.ToString());
				}
			};
			// There is one back-end object covering all protocols and accounts. It does not go in the DB.
			// It manages everything while the app is running.
			Be = new BackEnd (this);
			if (0 == Be.Db.Table<NcAccount> ().Count ()) {
				EnterFullConfiguration ();
			}
			Be.Start ();
			TryDelete ();
		}

		private void EnterFullConfiguration () {
			// You will always need to supply user credentials (until certs, for sure).
			var cred = new NcCred () { Username = "jeffe@nachocove.com", Password = "D0ggie789" };
			Be.Db.Insert (BackEnd.Actors.Ui, cred);
			// Once autodiscover is viable, you will only need to supply this server info IFF you get a callback.
			var server = new NcServer () { Fqdn = "nco9.com" };
			Be.Db.Insert (BackEnd.Actors.Ui, server);
			// In the near future, you won't need to create this protocol state object.
			var protocolState = new NcProtocolState ();
			Be.Db.Insert (BackEnd.Actors.Ui, protocolState);
			// You will always need to supply the user's email address.
			var account = new NcAccount () { EmailAddr = "jeffe@nachocove.com" };
			// The account object is the "top", pointing to credential, server, and opaque protocol state.
			account.CredId = cred.Id;
			account.ServerId = server.Id;
			account.ProtocolStateId = protocolState.Id;
			Be.Db.Insert (BackEnd.Actors.Ui, account);
		}
		public void TryDelete () {
			if (0 != Be.Db.Table<NcEmailMessage> ().Count ()) {
				var dead = Be.Db.Table<NcEmailMessage> ().First ();
				Be.Db.Delete (BackEnd.Actors.Ui, dead);
			}
		}

		// Methods for IBackEndDelegate:
		public void CredReq(NcAccount account) {
		}
		public void ServConfReq (NcAccount account) {
			// Will change - needed for current autodiscover flow.
			Be.Db.Update (BackEnd.Actors.Ui, account);
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

