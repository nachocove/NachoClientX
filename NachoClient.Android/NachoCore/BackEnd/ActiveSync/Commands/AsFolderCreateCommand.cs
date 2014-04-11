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
        public AsFolderCreateCommand (IBEContext dataSource) :
            base (Xml.FolderHierarchy.FolderCreate, Xml.FolderHierarchy.Ns, dataSource)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.FolderCreate);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var folderCreate = new XElement (m_ns + Xml.FolderHierarchy.FolderCreate,
                                   new XElement (m_ns + Xml.FolderHierarchy.SyncKey, BEContext.ProtocolState.AsSyncKey),
                                   new XElement (m_ns + Xml.FolderHierarchy.ParentId, PendingSingle.DestFolderServerId),
                                   new XElement (m_ns + Xml.FolderHierarchy.DisplayName, PendingSingle.DisplayName),
                                   new XElement (m_ns + Xml.FolderHierarchy.Type, ((int)PendingSingle.FolderType)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (folderCreate);
            return doc;		
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            var protocolState = BEContext.ProtocolState;
            var xmlFolderCreate = doc.Root;
            var xmlStatus = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.Status);
            var status = uint.Parse (xmlStatus.Value);
            switch ((Xml.FolderHierarchy.FolderCreateStatusCode)status) {
            case Xml.FolderHierarchy.FolderCreateStatusCode.Success_1:
                protocolState.AsSyncKey = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.SyncKey).Value;
                protocolState.Update ();
                var serverId = xmlFolderCreate.Element (m_ns + Xml.FolderHierarchy.ServerId).Value;
                var folder = McItem.QueryByServerId<McFolder> (BEContext.Account.Id, PendingSingle.ServerId);
                if (null != folder) {
                    folder.ServerId = serverId;
                    folder.AsFolderSyncEpoch = protocolState.AsFolderSyncEpoch;
                    folder.Update ();
                }
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl,
                    NcResult.Info (NcResult.SubKindEnum.Info_FolderCreateSucceeded));
                return Event.Create ((uint)SmEvt.E.Success, "FCRESUCCESS");

            case Xml.FolderHierarchy.FolderCreateStatusCode.Exists_2:
                PendingSingle.ResolveAsUserBlocked (BEContext.ProtoControl,
                    McPending.BlockReasonEnum.MustChangeName,
                    NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed,
                        NcResult.WhyEnum.AlreadyExistsOnServer));
                return Event.Create ((uint)SmEvt.E.HardFail, "FCREDUP2");

            case Xml.FolderHierarchy.FolderCreateStatusCode.SpecialParent_3:
                PendingSingle.ResolveAsUserBlocked (BEContext.ProtoControl,
                    McPending.BlockReasonEnum.MustPickNewParent,
                    NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                return Event.Create ((uint)SmEvt.E.HardFail, "FCRESPECIAL");

            case Xml.FolderHierarchy.FolderCreateStatusCode.BadParent_5:
                // Need to ask user for different name *after* the FolderSync.
                // So we check to see if it is out of DefersRemaining. If yes, THEN we bug the user.
                if (0 == PendingSingle.DefersRemaining) {
                    PendingSingle.ResolveAsUserBlocked (BEContext.ProtoControl,
                        McPending.BlockReasonEnum.MustPickNewParent,
                        NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                    return Event.Create ((uint)SmEvt.E.HardFail, "FCREBADP");
                } else {
                    PendingSingle.ResolveAsDeferredForce ();
                    return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC");
                }

            case Xml.FolderHierarchy.FolderCreateStatusCode.ServerError_6:
                // Trust server to tell FolderSync if we need to reset SyncKey.
                PendingSingle.ResolveAsDeferredForce ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC");

            case Xml.FolderHierarchy.FolderCreateStatusCode.ReSync_9:
                PendingSingle.ResolveAsDeferredForce ();
                protocolState.IncrementAsFolderSyncEpoch ();
                protocolState.Update ();
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "FCREFSYNC2");

            default:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BadFormat_10:
            case Xml.FolderHierarchy.FolderCreateStatusCode.Unknown_11:
            case Xml.FolderHierarchy.FolderCreateStatusCode.BackEndError_12:
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_FolderCreateFailed));
                return Event.Create ((uint)SmEvt.E.HardFail, "FCREFAIL");
            }
        }
    }
}
