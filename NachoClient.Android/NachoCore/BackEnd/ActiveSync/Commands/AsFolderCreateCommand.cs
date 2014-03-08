// Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
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
    public class AsFolderCreateCommand : AsCommand
    {
        public AsFolderCreateCommand (IAsDataSource dataSource) :
            base (Xml.FolderHierarchy.FolderCreate, Xml.FolderHierarchy.Ns, dataSource)
        {
            Update = NextPending (McPending.Operations.FolderCreate);
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderCreate = new XElement (m_ns + Xml.FolderHierarchy.FolderCreate,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, DataSource.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ParentId, Update.DestFolderServerId),
                                   new XElement (m_ns + Xml.FolderHierarchy.DisplayName, Update.DisplayName),
                                   new XElement (m_ns + Xml.FolderHierarchy.Type, Update.FolderType));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderCreate);
            Update.IsDispatched = true;
            Update.Update ();
            return doc;		
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var xmlFolderCreate = doc.Root;
            switch ((Xml.FolderHierarchy.FolderCreateStatusCode)Convert.ToUInt32 (xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderCreateStatusCode.Success:
                var protocolState = DataSource.ProtocolState;
                protocolState.AsSyncKey = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                var serverId = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                var folder = McItem.QueryByServerId<McFolder> (DataSource.Account.Id, Update.ServerId);
                if (null != folder) {
                    folder.ServerId = serverId;
                    folder.Update ();
                }
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.Success, "FCRESUCCESS");

            case Xml.FolderHierarchy.FolderCreateStatusCode.Exists:
            case Xml.FolderHierarchy.FolderCreateStatusCode.SpecialParent:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BadParent:
            case Xml.FolderHierarchy.FolderCreateStatusCode.ServerError:
            case Xml.FolderHierarchy.FolderCreateStatusCode.ReSync:

            case Xml.FolderHierarchy.FolderCreateStatusCode.BadFormat:
            case Xml.FolderHierarchy.FolderCreateStatusCode.Unknown:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BackEndError:
				// FIXME - all the error cases.
            default:
                Update.Delete ();
                return Event.Create ((uint)SmEvt.E.HardFail, "FCREFAIL");
            }
        }
    }
}
