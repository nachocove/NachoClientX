// Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsFolderCreateCommand : AsCommand
    {
        public AsFolderCreateCommand (IBEContext dataSource, McPending pending) :
            base (Xml.FolderHierarchy.FolderCreate, Xml.FolderHierarchy.Ns, dataSource)
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
            var folderCreate = new XElement (m_ns + Xml.FolderHierarchy.FolderCreate,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, BEContext.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ParentId, PendingSingle.ParentId),
                                   new XElement (m_ns + Xml.FolderHierarchy.DisplayName, PendingSingle.DisplayName),
                                   new XElement (m_ns + Xml.FolderHierarchy.Type, ((int)PendingSingle.Folder_Type)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderCreate);
            return doc;		
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "FLDCRECANCEL");
            }
            var protocolState = BEContext.ProtocolState;
            var xmlFolderCreate = doc.Root;
            var xmlStatus = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.Status);
            var status = uint.Parse (xmlStatus.Value);
            switch ((Xml.FolderHierarchy.FolderCreateStatusCode)status) {
            case Xml.FolderHierarchy.FolderCreateStatusCode.Success_1:
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.AsSyncKey = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                    return true;
                });
                var serverId = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                var pathElem = new McPath (AccountId);
                pathElem.ServerId = serverId;
                pathElem.ParentId = PendingSingle.ParentId;
                pathElem.IsFolder = true;
                pathElem.Insert ();
                var applyFolderCreate = new ApplyCreateFolder (AccountId) {
                    PlaceholderId = PendingSingle.ServerId,
                    FinalServerId = serverId,
                };
                applyFolderCreate.ProcessServerCommand ();

                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl,
                        NcResult.Info (NcResult.SubKindEnum.Info_FolderCreateSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.Success, "FCRESUCCESS");

            case Xml.FolderHierarchy.FolderCreateStatusCode.Exists_2:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                        McPending.BlockReasonEnum.MustChangeName,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed,
                            NcResult.WhyEnum.AlreadyExistsOnServer));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FCREDUP2");

            case Xml.FolderHierarchy.FolderCreateStatusCode.SpecialParent_3:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsUserBlocked (BEContext.ProtoControl,
                        McPending.BlockReasonEnum.MustPickNewParent,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FCRESPECIAL");

            case Xml.FolderHierarchy.FolderCreateStatusCode.BadParent_5:
                // Need to ask user for different name *after* the FolderSync.
                // So we check to see if it is out of DefersRemaining. If yes, THEN we bug the user.
                lock (PendingResolveLockObj) {
                    if (null == PendingSingle) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "FCREBADPC");
                    } else if (0 == PendingSingle.DefersRemaining) {
                        PendingSingle.ResolveAsUserBlocked (BEContext.ProtoControl,
                            McPending.BlockReasonEnum.MustPickNewParent,
                            NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                        PendingSingle = null;
                        return Event.Create ((uint)SmEvt.E.HardFail, "FCREBADP");
                    } else {
                        PendingSingle.ResolveAsDeferredForce (BEContext.ProtoControl);
                        PendingSingle = null;
                        return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC");
                    }
                }

            case Xml.FolderHierarchy.FolderCreateStatusCode.ServerError_6:
                // Trust server to tell FolderSync if we need to reset SyncKey.
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC");

            case Xml.FolderHierarchy.FolderCreateStatusCode.ReSync_9:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.IncrementAsFolderSyncEpoch ();
                    return true;
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC2");

            default:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BadFormat_10:
            case Xml.FolderHierarchy.FolderCreateStatusCode.Unknown_11:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BackEndError_12:
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "FCREFAIL");
            }
        }

        private class ApplyCreateFolder : NcApplyServerCommand
        {
            public string FinalServerId { set; get; }

            public string PlaceholderId { set; get; }

            public ApplyCreateFolder (int accountId)
                : base (accountId)
            {
            }

            protected override List<McPending.ReWrite> ApplyCommandToPending (McPending pending, 
                out McPending.DbActionEnum action,
                out bool cancelDelta)
            {
                // TODO - need a McPending method that acts on ALL ServerId fields.
                action = McPending.DbActionEnum.DoNothing;
                cancelDelta = false;
                if (null != pending.ServerId && pending.ServerId == PlaceholderId) {
                    pending.ServerId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                if (null != pending.DestParentId && pending.DestParentId == PlaceholderId) {
                    pending.DestParentId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                if (null != pending.ParentId && pending.ParentId == PlaceholderId) {
                    pending.ParentId = FinalServerId;
                    action = McPending.DbActionEnum.Update;
                }
                return null;
            }

            protected override void ApplyCommandToModel ()
            {
                var created = McFolder.QueryByServerId<McFolder> (AccountId, PlaceholderId);
                if (null != created) {
                    created = created.UpdateWithOCApply<McFolder> ((record) => {
                        var target = (McFolder)record;
                        target.ServerId = FinalServerId;
                        target.IsAwaitingCreate = false;
                        return true;
                    });
                    var account = McAccount.QueryById<McAccount> (AccountId);
                    var protocolState = McProtocolState.QueryByAccountId<McProtocolState> (account.Id).SingleOrDefault ();
                    var folders = McFolder.QueryByParentId (AccountId, PlaceholderId);
                    foreach (var child in folders) {
                        child.UpdateWithOCApply<McFolder> ((record) => {
                            var target = (McFolder)record;
                            target.ParentId = FinalServerId;
                            target.AsFolderSyncEpoch = protocolState.AsFolderSyncEpoch;
                            return true;
                        });
                    }
                }
            }
        }
    }
}
