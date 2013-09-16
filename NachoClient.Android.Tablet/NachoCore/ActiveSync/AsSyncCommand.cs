using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSyncCommand : AsCommand
	{
		public const string SyncKeyInitial = "0";

		public AsSyncCommand (IAsDataSource dataSource) : base(Xml.AirSync.Ns, dataSource) {}

		protected override XDocument ToXDocument () {
			XNamespace ns = Xml.AirSync.Ns;
			var collections = new XElement (ns+Xml.AirSync.Collections);
			var folders = m_dataSource.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id);
			foreach (var folder in folders) {
				var collection = new XElement (ns + Xml.AirSync.Collection,
				                              new XElement (ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
				                              new XElement (ns + Xml.AirSync.CollectionId, folder.ServerId));
				if (SyncKeyInitial != folder.AsSyncKey) {
					collection.Add (new XElement (ns + Xml.AirSync.GetChanges));
				}
			}
			var sync = new XElement (ns+Xml.AirSync.Sync, collections);
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sync);
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			return (uint)Ev.Success; // FIXME
		}
	}
}

