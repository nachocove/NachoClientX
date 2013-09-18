using System;
using System.Collections.Generic;
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
	public class BackEnd : IAsOwner
	{
		public SQLiteConnectionWithEvents Db { set; get; }

		private List<AsControl> services;
		private IBackEndDelegate m_dele;

		public BackEnd (IBackEndDelegate dele) {
			Db = new SQLiteConnectionWithEvents("db");
			Db.CreateTable<NcAccount> ();
			Db.CreateTable<NcCred> ();
			Db.CreateTable<NcFolder> ();
			Db.CreateTable<NcMessageEmail> ();
			Db.CreateTable<NcProtocolState> ();
			Db.CreateTable<NcServer> ();
			services = new List<AsControl> ();

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
			var service = new AsControl (this, account);
			services.Add (service);
			service.Execute ();
		}
		public void CredRequest (AsControl sender) {
			m_dele.CredRequest (sender.Account);
		}
		public void ServConfRequest (AsControl sender) {
			m_dele.ServConfRequest (sender.Account);
		}
		public void HardFailureIndication (AsControl sender) {
			m_dele.HardFailureIndication (sender.Account);
		}
		public void SoftFailureIndication (AsControl sender) {
			m_dele.HardFailureIndication (sender.Account);
		}
	}
}
