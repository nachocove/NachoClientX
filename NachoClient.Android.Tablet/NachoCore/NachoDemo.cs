using System;
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
			NcEventable.DidWriteToDb += (BackEnd.Actors actor, int accountId, Type klass, int id, EventArgs e) => {
				if (BackEnd.Actors.Ui != actor) {
					Console.WriteLine("{0} written {1}", klass.Name, id);
				}
			};
			NcEventable.WillDeleteFromDb += (BackEnd.Actors actor, int accountId, Type klass, int id, EventArgs e) => {
				if (BackEnd.Actors.Ui != actor) {
					Console.WriteLine("{0} deleted {1}", klass.Name, id);
				}
			};
			// There is one back-end object covering all protocols and accounts. It does not go in the DB.
			// It manages everything while the app is running.
			Be = new BackEnd (this);
			if (0 == Be.Db.Table<NcAccount> ().Count ()) {
				EnterFullConfiguration ();
			}
			Be.Start ();
		}

		private void EnterFullConfiguration () {
			// You will always need to supply user credentials (until certs, for sure).
			var cred = new NcCred () { Username = "jeffe@nachocove.com", Password = "D0ggie789" };
			Be.Db.Insert (BackEnd.Actors.Ui, cred);
			// Once autodiscover is viable, you will only need to supply this server info IFF you get a callback.
			var server = new NcServer () { Fqdn = "nco9.com", Port = 443, Scheme = "https"};
			Be.Db.Insert (BackEnd.Actors.Ui, server);
			// In the near future, you won't need to create this protocol state object.
			var protocolState = new NcProtocolState () { AsProtocolVersion = "12.0", AsPolicyKey = "0" };
			Be.Db.Insert (BackEnd.Actors.Ui, protocolState);
			// You will always need to supply the user's email address.
			var account = new NcAccount () { EmailAddr = "jeffe@nachocove.com" };
			// The account object is the "top", pointing to credential, server, and opaque protocol state.
			account.CredId = cred.Id;
			account.ServerId = server.Id;
			account.ProtocolStateId = protocolState.Id;
			Be.Db.Insert (BackEnd.Actors.Ui, account);
		}

		public void CredRequest(NcAccount account) {
		}
		public void ServConfRequest (NcAccount account) {
			// Will change - needed for current autodiscover flow.
			Be.Db.Update (BackEnd.Actors.Ui, account);
		}
		public void HardFailureIndication (NcAccount account) {
		}
		public void SoftFailureIndication (NcAccount account) {
		}
	}
}

