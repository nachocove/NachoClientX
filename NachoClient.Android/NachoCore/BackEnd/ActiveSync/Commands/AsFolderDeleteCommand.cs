﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        public AsFolderDeleteCommand (IBEContext dataSource, McPending pending) :
            base (Xml.FolderHierarchy.FolderDelete, Xml.FolderHierarchy.Ns, dataSource)
        {
            PendingSingle = pending;
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
            var pathElem = McPath.QueryByServerId (BEContext.Account.Id, PendingSingle.ServerId);
            var folder = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ServerId);
            switch ((Xml.FolderHierarchy.FolderDeleteStatusCode)Convert.ToUInt32 (xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderDeleteStatusCode.Success_1:
                protocolState.AsSyncKey = xmlFolderDelete.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                pathElem.Delete ();
                folder.Delete ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl,
                        NcResult.Info (NcResult.SubKindEnum.Info_FolderDeleteSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "FDELSUCCESS");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.Special_3:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderDeleteFailed,
                            NcResult.WhyEnum.SpecialFolder));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAILSPEC");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.Missing_4:
                // If it is missing on the server, then let's it be missing here too.
                pathElem.Delete ();
                folder.Delete ();
                lock (PendingResolveLockObj) {
                    if (null == PendingSingle) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAILSPECC");
                    } else if (0 == PendingSingle.DefersRemaining) {
                        PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_FolderDeleteFailed,
                                NcResult.WhyEnum.MissingOnServer));
                        PendingSingle = null;
                        return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAILSPEC");
                    } else {
                        PendingSingle.ResolveAsDeferredForce ();
                        PendingSingle = null;
                        return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC1");
                    }
                }

            case Xml.FolderHierarchy.FolderDeleteStatusCode.ServerError_6:
                /* TODO: "Retry the FolderDelete command. If continued attempts to synchronization fail,
                 * consider returning to synchronization key zero (0)."
                 * Right now, we don't retry - we just slam the key to 0.
                 */
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState.Update ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferredForce ();
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC2");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.ReSync_9:
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState.Update ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferredForce ();
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FDELFSYNC3");

            case Xml.FolderHierarchy.FolderDeleteStatusCode.BadFormat_10:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_ProtocolError));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAIL1");

            default:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_UnknownCommandFailure));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FDELFAIL2");
            }
        }
    }
}
