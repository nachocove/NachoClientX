//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsFolderUpdateCommand : AsCommand
    {
        public AsFolderUpdateCommand (IBEContext dataSource, McPending pending) :
            base (Xml.FolderHierarchy.FolderUpdate, Xml.FolderHierarchy.Ns, dataSource)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispatched ();
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderUpdate = new XElement (m_ns + Xml.FolderHierarchy.FolderUpdate,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, BEContext.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ServerId, PendingSingle.ServerId),
                                   new XElement (m_ns + Xml.FolderHierarchy.ParentId, PendingSingle.DestParentId),
                                   new XElement (m_ns + Xml.FolderHierarchy.DisplayName, PendingSingle.DisplayName));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderUpdate);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "FLDUPDCANCEL");
            }
            McProtocolState protocolState = BEContext.ProtocolState;
            var xmlFolderUpdate = doc.Root;
            switch ((Xml.FolderHierarchy.FolderUpdateStatusCode)Convert.ToUInt32 (xmlFolderUpdate.Element (m_ns + Xml.FolderHierarchy.Status).Value)) {
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Success_1:
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.AsSyncKey = xmlFolderUpdate.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                    return true;
                });
                var pathElem = McPath.QueryByServerId (AccountId, PendingSingle.ServerId);
                pathElem.ParentId = PendingSingle.ParentId;
                pathElem.Update ();
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl,
                        NcResult.Info (NcResult.SubKindEnum.Info_FolderUpdateSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "FUPSUCCESS");

            case Xml.FolderHierarchy.FolderUpdateStatusCode.Exists_2:
                // "A folder with that name already exists" - makes no sense for update.
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Special_3:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                        McPending.BlockReasonEnum.MustPickNewParent,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderUpdateFailed,
                            NcResult.WhyEnum.SpecialFolder));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL1");

            case Xml.FolderHierarchy.FolderUpdateStatusCode.Missing_4:
                lock (PendingResolveLockObj) {
                    if (null == PendingSingle) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAILSPECC");
                    } else if (0 == PendingSingle.DefersRemaining) {
                        PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_FolderUpdateFailed,
                                NcResult.WhyEnum.MissingOnServer));
                        PendingSingle = null;
                        return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAILSPEC");
                    } else {
                        PendingSingle.ResolveAsDeferredForce (BEContext.ProtoControl);
                        PendingSingle = null;
                        return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FUPFSYNC1");
                    }
                }

            case Xml.FolderHierarchy.FolderUpdateStatusCode.MissingParent_5:
                lock (PendingResolveLockObj) {
                    if (null == PendingSingle) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAILMPC");
                    } else if (0 == PendingSingle.DefersRemaining) {
                        PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                            NcResult.Error (NcResult.SubKindEnum.Error_FolderUpdateFailed,
                                NcResult.WhyEnum.MissingOnServer));
                        PendingSingle = null;
                        return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAILMP");
                    } else {
                        PendingSingle.ResolveAsDeferredForce (BEContext.ProtoControl);
                        PendingSingle = null;
                        return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FUPFSYNC1");
                    }
                }

            case Xml.FolderHierarchy.FolderUpdateStatusCode.ServerError_6:
                /* TODO: "Retry the FolderUpdate command. If continued attempts to synchronization fail,
                 * consider returning to synchronization key zero (0)."
                 * Right now, we don't retry - we just slam the key to 0.
                 */
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.IncrementAsFolderSyncEpoch ();
                    return true;
                });
                PendingResolveApply ((pending) => {
                    PendingSingle.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FUPFSYNC2");

            case Xml.FolderHierarchy.FolderUpdateStatusCode.ReSync_9:
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.IncrementAsFolderSyncEpoch ();
                    return true;
                });
                PendingResolveApply ((pending) => {
                    PendingSingle.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FUPFSYNC3");

            case Xml.FolderHierarchy.FolderUpdateStatusCode.BadFormat_10:
            case Xml.FolderHierarchy.FolderUpdateStatusCode.Unknown_11:
            default:
                PendingResolveApply ((pending) => {
                    PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderUpdateFailed));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FUPFAIL");
            }
        }
    }
}
