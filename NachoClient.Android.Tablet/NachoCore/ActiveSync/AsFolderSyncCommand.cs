using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsFolderSyncCommand : AsCommand
	{

		public AsFolderSyncCommand (IAsDataSource dataSource) : base(Xml.FolderHierarchy.FolderSync, dataSource) {}

		protected override XDocument ToXDocument () {
			XNamespace ns = Xml.FolderHierarchy.Ns;
			var folderSync = new XElement (ns+Xml.FolderHierarchy.FolderSync, 
			                               new XElement (ns+Xml.FolderHierarchy.SyncKey, m_dataSource.ProtocolState.AsSyncKey));
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (folderSync);
			return doc;
		}

		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			XNamespace ns = Xml.FolderHierarchy.Ns;
			switch ((Xml.FolderHierarchy.StatusCode)Convert.ToUInt32 (doc.Root.Element (ns+Xml.FolderHierarchy.Status).Value)) {
			case Xml.FolderHierarchy.StatusCode.Success:
				m_dataSource.ProtocolState.AsSyncKey = doc.Root.Element (ns+Xml.FolderHierarchy.SyncKey).Value;
				var changes = doc.Root.Element (ns+Xml.FolderHierarchy.Changes).Elements ();
				if (null != changes) {
					foreach (var change in changes) {
						switch (change.Name.LocalName) {
						case Xml.FolderHierarchy.Add:
							var folder = new NcFolder () {
								AccountId = m_dataSource.Account.Id,
								ServerId = change.Element (ns+Xml.FolderHierarchy.ServerId).Value,
								ParentId = change.Element (ns+Xml.FolderHierarchy.ParentId).Value,
								DisplayName = change.Element (ns+Xml.FolderHierarchy.DisplayName).Value,
								Type = change.Element (ns+Xml.FolderHierarchy.Type).Value,
								AsSyncKey = Xml.AirSync.SyncKey_Initial,
								AsSyncRequired = true
							};
							m_dataSource.Owner.Db.Insert (BackEnd.Actors.Proto, folder);
							break;
						case Xml.FolderHierarchy.Update:
							var serverId = change.Element (ns+Xml.FolderHierarchy.ServerId).Value;
							folder = m_dataSource.Owner.Db.Table<NcFolder> ().Where (rec => rec.ServerId == serverId).First ();
							folder.ParentId = change.Element (ns+Xml.FolderHierarchy.ParentId).Value;
							folder.DisplayName = change.Element (ns+Xml.FolderHierarchy.DisplayName).Value;
							folder.Type = change.Element (ns+Xml.FolderHierarchy.Type).Value;
							m_dataSource.Owner.Db.Update (BackEnd.Actors.Proto, folder);
							break;
						case Xml.FolderHierarchy.Delete:
							serverId = change.Element (ns+Xml.FolderHierarchy.ServerId).Value;
							folder = m_dataSource.Owner.Db.Table<NcFolder> ().Where (rec => rec.ServerId == serverId).First ();
							m_dataSource.Owner.Db.Delete (BackEnd.Actors.Proto, folder);
							break;
						}
					}
				}
				return (uint)Ev.Success;
			default:
				return (uint)Ev.HardFail;
			}
		}
	}
}

