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
        public AsFolderSyncCommand (IAsDataSource dataSource) : base(Xml.FolderHierarchy.FolderSync, Xml.FolderHierarchy.Ns, dataSource) {}

        public override XDocument ToXDocument (AsHttpOperation Sender) {
            var folderSync = new XElement (m_ns+Xml.FolderHierarchy.FolderSync, 
                                           new XElement (m_ns+Xml.FolderHierarchy.SyncKey, DataSource.ProtocolState.AsSyncKey));
            var doc = AsCommand.ToEmptyXDocument();
            doc.Add (folderSync);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            switch ((Xml.FolderHierarchy.StatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns+Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.StatusCode.Success:
                DataSource.ProtocolState.AsSyncKey = doc.Root.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                var changes = doc.Root.Element (m_ns + Xml.FolderHierarchy.Changes).Elements ();
                if (null != changes) {
                    foreach (var change in changes) {
                        switch (change.Name.LocalName) {
                        case Xml.FolderHierarchy.Add:
                            var folder = new NcFolder () {
                                AccountId = DataSource.Account.Id,
                                ServerId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value,
                                ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                Type = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                                AsSyncKey = Xml.AirSync.SyncKey_Initial,
                                AsSyncRequired = true
                            };
                            DataSource.Owner.Db.Insert (BackEnd.DbActors.Proto, folder);
                            break;
                        case Xml.FolderHierarchy.Update:
                            var serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            folder = DataSource.Owner.Db.Table<NcFolder> ().Where (rec => rec.ServerId == serverId).First ();
                            folder.ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value;
                            folder.DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value;
                            folder.Type = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value);
                            DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, folder);
                            break;
                        case Xml.FolderHierarchy.Delete:
                            serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            folder = DataSource.Owner.Db.Table<NcFolder> ().Where (rec => rec.ServerId == serverId).First ();
                            DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, folder);
                            break;
                        }
                    }
                }
                return Event.Create ((uint)SmEvt.E.Success);
            default:
                return Event.Create ((uint)SmEvt.E.HardFail);
            }
        }
    }
}

