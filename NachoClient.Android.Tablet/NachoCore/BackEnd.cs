using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
 * */
namespace NachoCore
{
	public class BackEnd : IProtoControlOwner
	{
		public enum Actors {Ui, Proto};
		public SQLiteConnectionWithEvents Db { set; get; }

		private List<ProtoControl> services;
		private IBackEndDelegate m_dele;

		public BackEnd (IBackEndDelegate dele) {
			var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
			var filename = Path.Combine (documents, "db");
			Db = new SQLiteConnectionWithEvents(filename);
			Db.CreateTable<NcAccount> ();
			Db.CreateTable<NcCred> ();
			Db.CreateTable<NcFolder> ();
			Db.CreateTable<NcMessageEmail> ();
			Db.CreateTable<NcProtocolState> ();
			Db.CreateTable<NcServer> ();
			Db.CreateTable<NcPendingUpdate> ();

			services = new List<ProtoControl> ();

			m_dele = dele;
		}
		// for each account, fire up an EAS control.
		public void Start () {
			var accounts = Db.Table<NcAccount> ();
			foreach (var account in accounts) {
				Start (account);
			}
		}
		public void Start (NcAccount account) {
			// FIXME. This code needs to be able to detect the account type and start the appropriate control.
			var service = new AsProtoControl (this, account);
			services.Add (service);
			service.Execute ();
		}
		public bool SendEMail(NcAccount account, Dictionary<string,string> message) {
			var service = services.Single (svc => svc.Account.Id == account.Id);
			AsProtoControl asService = (AsProtoControl)service;
			return asService.SendEMail (message); // FIXME - cast sucks.
		}
		// For IProtoControlOwner.
		public void CredRequest (ProtoControl sender) {
			m_dele.CredRequest (sender.Account);
		}
		public void ServConfRequest (ProtoControl sender) {
			m_dele.ServConfRequest (sender.Account);
		}
		public void HardFailureIndication (ProtoControl sender) {
			m_dele.HardFailureIndication (sender.Account);
		}
		public void SoftFailureIndication (ProtoControl sender) {
			m_dele.HardFailureIndication (sender.Account);
		}
	}
}
