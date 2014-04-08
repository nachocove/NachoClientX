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
        public AsFolderDeleteCommand (IBEContext dataSource) :
            base (Xml.FolderHierarchy.FolderDelete, Xml.FolderHierarchy.Ns, dataSource)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.FolderDelete);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderDelete = new XElement (m_ns + Xml.FolderHierarchy.FolderDelete,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, BEContext.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ServerId, PendingSingle.ServerId));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderDelete);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            McProtocolState protocolState = BEContext.ProtocolState;
            var xmlFolderDelete = doc.Root;
            switch ((Xml.FolderHierarchy.FolderDeleteStatusCode)Convert.ToUInt32 (xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderDeleteStatusCode.Success_1:
                protocolState.AsSyncKey = xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl,
                    NcResult.Info (NcResult.SubKindEnum.Info_FolderDeleteSucceeded));
                return Event.Create ((uint)SmEvt.E.Success, "FDELSUCCESS");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.Special_3:
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_FolderDeleteFailed,
                        NcResult.WhyEnum.SpecialFolder));
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAILSPEC");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.Missing_4:
                if (0 == PendingSingle.DefersRemaining) {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderDeleteFailed,
                            NcResult.WhyEnum.MissingOnServer));
                    return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAILSPEC");
                } else {
                    PendingSingle.ResolveAsDeferredForce ();
                    return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC1");
                }

            case Xml.FolderHierarchy.FolderDeleteStatusCode.ServerError_6:
                /* TODO: "Retry the FolderDelete command. If continued attempts to synchronization fail,
                 * consider returning to synchronization key zero (0)."
                 * Right now, we don't retry - we just slam the key to 0.
                 */
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState.Update ();
                PendingSingle.ResolveAsDeferredForce ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC2");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.ReSync_9:
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState.Update ();
                PendingSingle.ResolveAsDeferredForce ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC3");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.BadFormat_10:
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAIL1");

            default:
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAIL2");
            }
        }
    }
}
