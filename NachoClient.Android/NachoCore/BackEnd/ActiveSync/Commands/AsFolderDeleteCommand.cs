//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsFolderDeleteCommand : AsCommand
    {
        public AsFolderDeleteCommand (IAsDataSource dataSource) :
            base (Xml.FolderHierarchy.FolderDelete, Xml.FolderHierarchy.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.FolderDelete);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderDelete = new XElement (m_ns + Xml.FolderHierarchy.FolderDelete,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, DataSource.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ServerId, Update.ServerId));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderDelete);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlFolderDelete = doc.Root;
            switch ((Xml.FolderHierarchy.FolderDeleteStatusCode)Convert.ToUInt32 (xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderDeleteStatusCode.Success:
                var protocolState = DataSource.ProtocolState;
                protocolState.AsSyncKey = xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "FDELSUCCESS");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.Special:
            case Xml.FolderHierarchy.FolderDeleteStatusCode.Missing:
            case Xml.FolderHierarchy.FolderDeleteStatusCode.ServerError:
            case Xml.FolderHierarchy.FolderDeleteStatusCode.ReSync:
            case Xml.FolderHierarchy.FolderDeleteStatusCode.BadFormat:
				// FIXME - all the error cases.
            default:
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAIL");
            }
        }
    }
}
