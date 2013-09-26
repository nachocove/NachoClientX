using System;
using System.Collections.Generic;
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
			// FIXME - only syncing down Mail:DEFAULT.
			var folders = m_dataSource.Owner.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id && true == x.AsSyncRequired && "Mail:DEFAULT" == x.ServerId);
			foreach (var folder in folders) {
				var collection = new XElement (ns + Xml.AirSync.Collection,
				                              new XElement (ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
				                              new XElement (ns + Xml.AirSync.CollectionId, folder.ServerId));
				if (Xml.AirSync.SyncKey_Initial != folder.AsSyncKey) {
					collection.Add (new XElement (ns + Xml.AirSync.GetChanges));
					if (m_dataSource.Staged.EmailMessageDeletes.ContainsKey(folder.Id)) {
						var deles = m_dataSource.Staged.EmailMessageDeletes [folder.Id];
						//collection.Add (new XElement (ns + Xml.AirSync.DeleteAsMoves));
						var commands = new XElement (ns + Xml.AirSync.Commands);
						foreach (var change in deles) {
							commands.Add (new XElement (ns + Xml.AirSync.Delete,
							                            new XElement (ns + Xml.AirSync.ServerId, change.Update.ServerId)));
							change.IsDispatched = true;
						}
						collection.Add (commands);
					}
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
			var collections = doc.Root.Element (ns + Xml.AirSync.Collections).Elements ();
			foreach (var collection in collections) {
				var serverId = collection.Element (ns + Xml.AirSync.CollectionId).Value;
				var folder = m_dataSource.Owner.Db.Table<NcFolder> ().Single (rec => rec.ServerId == serverId);
				var oldSyncKey = folder.AsSyncKey;
				folder.AsSyncKey = collection.Element (ns + Xml.AirSync.SyncKey).Value;
				folder.AsSyncRequired = (Xml.AirSync.SyncKey_Initial == oldSyncKey);
				switch (uint.Parse(collection.Element (ns + Xml.AirSync.Status).Value)) {
				case (uint)Xml.AirSync.StatusCode.Success:
					if (m_dataSource.Staged.EmailMessageDeletes.ContainsKey (folder.Id)) {
						foreach (StagedChange change in m_dataSource.Staged.EmailMessageDeletes [folder.Id].
						         Where(elem => true == elem.IsDispatched).ToList ()) {
							m_dataSource.Staged.EmailMessageDeletes [folder.Id].Remove (change);
							m_dataSource.Owner.Db.Delete (BackEnd.Actors.Proto, change.Update);
						}
					}
					break;
					// FIXME - other status code values.
				}
				var commandsNode = collection.Element (ns + Xml.AirSync.Commands);
				if (null != commandsNode) {
					var commands = commandsNode.Elements ();
					foreach (var command in commands) {
						switch (command.Name.LocalName) {
						case Xml.AirSync.Add:
							var emailMessage = new NcEmailMessage () {
								AccountId = m_dataSource.Account.Id,
								FolderId = folder.Id,
								ServerId = command.Element(ns + Xml.AirSync.ServerId).Value
							};
							var appData = command.Element (ns + Xml.AirSync.ApplicationData);
							foreach (var child in appData.Elements()) {
								switch (child.Name.LocalName) {
								case Xml.AirSyncBase.Body:
									emailMessage.Encoding = child.Element (baseNs + Xml.AirSyncBase.Type).Value;
									emailMessage.Body = child.Element (baseNs + Xml.AirSyncBase.Data).Value;
									break;
								case Xml.Email.To:
									emailMessage.To = child.Value;
									break;
								case Xml.Email.From:
									emailMessage.From = child.Value;
									break;
								case Xml.Email.ReplyTo:
									emailMessage.ReplyTo = child.Value;
									break;
								case Xml.Email.Subject:
									emailMessage.Subject = child.Value;
									break;
								case Xml.Email.DateReceived:
									try {
										emailMessage.DateReceived = DateTime.Parse (child.Value);
									} catch {
										// FIXME - just log it.
									}
									break;
								case Xml.Email.DisplayTo:
									emailMessage.DisplayTo = child.Value;
									break;
								case Xml.Email.Importance:
									try {
										emailMessage.Importance = UInt32.Parse (child.Value);
									} catch {
										// FIXME - just log it.
									}
									break;
								case Xml.Email.Read:
									if ("1" == child.Value) {
										emailMessage.Read = true;
									} else {
										emailMessage.Read = false;
									}
									break;
								case Xml.Email.MessageClass:
									emailMessage.MessageClass = child.Value;
									break;
								}
							}
							m_dataSource.Owner.Db.Insert (BackEnd.Actors.Proto, emailMessage);
							break;
						}
					}
				}
				m_dataSource.Owner.Db.Update (BackEnd.Actors.Proto, folder);
			}
			var folders = m_dataSource.Owner.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id && true == x.AsSyncRequired && "Mail:DEFAULT" == x.ServerId);
			return (folders.Any ()) ? (uint)AsProtoControl.Lev.ReSync : (uint)Ev.Success;
		}
	}
}

