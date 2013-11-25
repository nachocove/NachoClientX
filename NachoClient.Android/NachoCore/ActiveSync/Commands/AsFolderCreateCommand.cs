// Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsFolderCreateCommand : AsCommand
    {
        private NcPendingUpdate Update;

        public AsFolderCreateCommand (IAsDataSource dataSource) : 
            base (Xml.FolderHierarchy.FolderCreate, Xml.FolderHierarchy.Ns, dataSource)
        {
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            Update = NextToCreate ();
            var folder = DataSource.Owner.Db.Table<NcFolder> ().Single (rec => rec.Id == Update.FolderId);
            var fCreate = new XElement (m_ns + Xml.FolderHierarchy.FolderCreate, 
                              new XElement (m_ns + Xml.FolderHierarchy.SyncKey, folder.AsSyncKey),
                              new XElement (m_ns + Xml.FolderHierarchy.ParentId, folder.ParentId),
                              new XElement (m_ns + Xml.FolderHierarchy.DisplayName, folder.DisplayName),
                              new XElement (m_ns + Xml.FolderHierarchy.Type, folder.Type));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (fCreate);
            Update.IsDispatched = true;
            DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, Update);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var folder = DataSource.Owner.Db.Table<NcFolder> ().Single (rec => rec.Id == Update.FolderId);

            switch ((Xml.FolderHierarchy.FolderCreateStatusCode)Convert.ToUInt32 (doc.Root.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderCreateStatusCode.Success:
                // FIXME - check for flawed response.
                var protocolState = DataSource.ProtocolState;
                protocolState.AsSyncKey = doc.Root.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, protocolState);
                folder.ServerId = doc.Root.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                folder.IsAwatingCreate = false;
                DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, folder);
                DataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, Update);
                return Event.Create ((uint)SmEvt.E.Success);
            default:
                // FIXME - case-specific behavior.
                return Event.Create ((uint)SmEvt.E.HardFail);
            }
        }

        private NcPendingUpdate NextToCreate ()
        {
            var query = DataSource.Owner.Db.Table<NcPendingUpdate> ()
                .Where (rec => rec.AccountId == DataSource.Account.Id &&
                        NcPendingUpdate.DataTypes.Folder == rec.DataType &&
                        NcPendingUpdate.Operations.Write == rec.Operation);
            return (0 == query.Count ()) ? null : query.First ();
        }
    }
}

