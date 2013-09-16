using System;
using System.Collections.Generic;
using SQLite;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
	public class BackEnd : IAsOwner
	{
		public SQLiteConnectionWithEvents Db { set; get; }
		private List<AsControl> services;

		public BackEnd () {
			Db = new SQLiteConnectionWithEvents("db");
			Db.CreateTable<NcAccount> ();
			Db.CreateTable<NcCred> ();
			Db.CreateTable<NcFolder> ();
			Db.CreateTable<NcMessageEmail> ();
			Db.CreateTable<NcProtocolState> ();
			Db.CreateTable<NcServer> ();
			services = new List<AsControl> ();
		}
		// for each account, fire up an EAS control.
		public void Start () {
			var accounts = Db.Table<NcAccount> ();
			foreach (var account in accounts) {
				StartAccount (account);
			}
		}
		// FIXME need a new/changed account callback to (re)kick-off.
		public void StartAccount (NcAccount account) {
			var service = new AsControl (this, account);
			services.Add (service);
			service.Execute ();
		}
		// FIXME - this crap needs to go to the UI, not the BE. Also need to indicate the account.
		public void CredRequest (AsControl sender) {
			sender.CredResponse ();
		}
		public void ServConfRequest (AsControl sender) {
			sender.ServConfResponse ();
		}
		public void HardFailureIndication (AsControl sender) {
		}
	}
}
