using System;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsFolderSyncCommand : AsCommand
    {
        private bool HadFolderChanges;

        public AsFolderSyncCommand (IAsDataSource dataSource) :
            base (Xml.FolderHierarchy.FolderSync, Xml.FolderHierarchy.Ns, dataSource)
        {
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var syncKey = DataSource.ProtocolState.AsSyncKey;
            Log.Info ("AsFolderSyncCommand: AsSyncKey=" + syncKey);
            var folderSync = new XElement (m_ns + Xml.FolderHierarchy.FolderSync, new XElement (m_ns + Xml.FolderHierarchy.SyncKey, syncKey));
            var doc = AsCommand.ToEmptyXDocument();
            doc.Add (folderSync);
            Log.Info (Log.LOG_SYNC, "AsFolderSyncCommand:\n{0}", doc);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var status = (Xml.FolderHierarchy.FolderSyncStatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.FolderHierarchy.Status).Value);

            switch (status) {
            case Xml.FolderHierarchy.FolderSyncStatusCode.Success:
                var protocolState = DataSource.ProtocolState;
                var syncKey = doc.Root.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                Log.Info ("AsFolderSyncCommand process response: SyncKey=" + syncKey);
                Log.Info (Log.LOG_SYNC, "AsFolderSyncCommand response:\n{0}", doc);
                protocolState.AsSyncKey = syncKey;
                DataSource.Owner.Db.Update (protocolState);
                var changes = doc.Root.Element (m_ns + Xml.FolderHierarchy.Changes).Elements ();
                if (null != changes) {
                    HadFolderChanges = true;
                    foreach (var change in changes) {
                        switch (change.Name.LocalName) {
                        case Xml.FolderHierarchy.Add:
                            var folder = new McFolder () {
                                AccountId = DataSource.Account.Id,
                                ServerId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value,
                                ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value,
                                DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value,
                                Type = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value),
                                AsSyncKey = Xml.AirSync.SyncKey_Initial,
                                AsSyncRequired = true
                            };
                            Log.Info ("foldersync - add - " + folder);
                            DataSource.Owner.Db.Insert (folder);
                            break;
                        case Xml.FolderHierarchy.Update:
                            var serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            folder = DataSource.Owner.Db.Table<McFolder> ().Where (rec => rec.ServerId == serverId).First ();
                            folder.ParentId = change.Element (m_ns + Xml.FolderHierarchy.ParentId).Value;
                            folder.DisplayName = change.Element (m_ns + Xml.FolderHierarchy.DisplayName).Value;
                            folder.Type = uint.Parse (change.Element (m_ns + Xml.FolderHierarchy.Type).Value);
                            Log.Info ("foldersync - update - " + folder);
                            DataSource.Owner.Db.Update (folder);
                            break;
                        case Xml.FolderHierarchy.Delete:
                            serverId = change.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                            folder = DataSource.Owner.Db.Table<McFolder> ().Where (rec => rec.ServerId == serverId).First ();
                            DataSource.Owner.Db.Delete (folder);
                            break;
                        }
                    }
                }
                if (HadFolderChanges) {
                    DataSource.Control.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_FolderSetChanged));
                }
                return Event.Create ((uint)SmEvt.E.Success, "FSYNCSUCCESS");
            default:
                // FIXME - case-specific behavior.
                Log.Error ("ASFoldersyncCommand: UNHANDLED status {0}", status);
                return Event.Create ((uint)SmEvt.E.HardFail, "FSYNCHARD");
            }
        }
    }
}

