using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSyncCommand : AsCommand
	{

		public AsSyncCommand (IAsDataSource dataSource) : base(Xml.AirSync.Sync, dataSource) {}

		protected override XDocument ToXDocument () {
			XNamespace ns = Xml.AirSync.Ns;
			var collections = new XElement (ns+Xml.AirSync.Collections);
			var folders = m_dataSource.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id && true == x.AsSyncRequired && "Mail:DEFAULT" == x.ServerId);
			foreach (var folder in folders) {
				var collection = new XElement (ns + Xml.AirSync.Collection,
				                              new XElement (ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
				                              new XElement (ns + Xml.AirSync.CollectionId, folder.ServerId));
				if (Xml.AirSync.SyncKey_Initial != folder.AsSyncKey) {
					collection.Add (new XElement (ns + Xml.AirSync.GetChanges));
				}
				collections.Add (collection);
			}
			var sync = new XElement (ns+Xml.AirSync.Sync, collections);
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sync);
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			XNamespace ns = Xml.AirSync.Ns;
			XNamespace baseNs = Xml.AirSyncBase.Ns;
			// FIXME - handle status.
			var collections = doc.Root.Element (ns + Xml.AirSync.Collections).Elements ();
			foreach (var collection in collections) {
				var serverId = collection.Element (ns + Xml.AirSync.CollectionId).Value;
				var folder = m_dataSource.Db.Table<NcFolder> ().Where (rec => rec.ServerId == serverId).First ();
				var oldSyncKey = folder.AsSyncKey;
				// FIXME - make SyncKey update contingent on transaction success.
				// FIXME - make this thing a DB transaction.
				folder.AsSyncKey = collection.Element (ns + Xml.AirSync.SyncKey).Value;
				folder.AsSyncRequired = (Xml.AirSync.SyncKey_Initial == oldSyncKey);
				var commandsNode = collection.Element (ns + Xml.AirSync.Commands);
				if (null != commandsNode) {
					var commands = commandsNode.Elements ();
					foreach (var command in commands) {
						switch (command.Name.LocalName) {
						case Xml.AirSync.Add:
							var body = command.Element (ns + Xml.AirSync.ApplicationData).Element (baseNs + Xml.AirSyncBase.Body);
							var emailMessage = new NcMessageEmail () {
								AccountId = m_dataSource.Account.Id,
								FolderId = folder.Id,
								ServerId = command.Element(ns + Xml.AirSync.ServerId).Value,
								Encoding = body.Element(baseNs + Xml.AirSyncBase.Type).Value,
								Body = body.Element (baseNs + Xml.AirSyncBase.Data).Value
							};
							m_dataSource.Db.Insert (BackEnd.Actors.Proto, emailMessage);
							break;
						}
					}
				}
				m_dataSource.Db.Update (BackEnd.Actors.Proto, folder);
			}
			var folders = m_dataSource.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id && true == x.AsSyncRequired && "Mail:DEFAULT" == x.ServerId);
			return (folders.Any ()) ? (uint)AsProtoControl.Lev.ReSync : (uint)Ev.Success;
		}
	}
}

