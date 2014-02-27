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
    public class AsFolderUpdateCommand : AsCommand
    {
        public AsFolderUpdateCommand (IAsDataSource dataSource) :
            base (Xml.FolderHierarchy.FolderUpdate, Xml.FolderHierarchy.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.FolderUpdate);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderUpdate = new XElement (m_ns + Xml.FolderHierarchy.FolderUpdate,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, DataSource.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ServerId, Update.ServerId),
                                   new XElement (m_ns + Xml.FolderHierarchy.ParentId, Update.DestFolderServerId),
                                   new XElement (m_ns + Xml.FolderHierarchy.DisplayName, Update.DisplayName));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderUpdate);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlFolderUpdate = doc.Root;
            switch ((Xml.FolderHierarchy.FolderUpdateStatusCode)Convert.ToUInt32 (xmlFolderUpdate.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Success:
                var protocolState = DataSource.ProtocolState;
                protocolState.AsSyncKey = xmlFolderUpdate.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "FUPSUCCESS");
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Exists:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Special:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Missing:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.BadParent:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.ServerError:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.ReSync:
				// FIXME - all the error cases.
            default:
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL");
            }
        }
    }
}
