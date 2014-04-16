//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsMoveItemsCommand : AsCommand
    {
        public AsMoveItemsCommand (IBEContext beContext) : base (Xml.Mov.MoveItems, Xml.Mov.Ns, beContext)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.EmailMove);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            // We can aggregate multiple move operations in one command if we want to.
            var move = new XElement (m_ns + Xml.Mov.MoveItems,
                           new XElement (m_ns + Xml.Mov.Move,
                               new XElement (m_ns + Xml.Mov.SrcMsgId, PendingSingle.ServerId),
                               new XElement (m_ns + Xml.Mov.SrcFldId, PendingSingle.ParentId),
                               new XElement (m_ns + Xml.Mov.DstFldId, PendingSingle.DestParentId)));
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (move);
            return doc;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
        {
            // Right now, there will only be 1 Response because we issue 1 at a time.
            var xmlResponse = doc.Root.Element (m_ns + Xml.Mov.Response);
            var xmlStatus = xmlResponse.Element (m_ns + Xml.Mov.Status);
            var status = (Xml.Mov.StatusCode)uint.Parse (xmlStatus.Value);
            switch (status) {
            case Xml.Mov.StatusCode.InvalidSrc_1:
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSyncThenSync,
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed,
                        NcResult.WhyEnum.MissingOnServer));
                return Event.Create (new Event[] {
                    Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIIS1"),
                    Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "MIIS2"),
                });

            case Xml.Mov.StatusCode.InvalidDest_2:
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSync,
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed,
                        NcResult.WhyEnum.MissingOnServer));
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIID");

            case Xml.Mov.StatusCode.Success_3:
                var xmlDstMsgId = xmlResponse.Element (m_ns + Xml.Mov.DstMsgId);
                if (null != xmlDstMsgId) {
                    var SrcMsgId = xmlResponse.Element (m_ns + Xml.Mov.SrcMsgId).Value;
                    var emailMessage = McObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
                    if (null != emailMessage) {
                        emailMessage.ServerId = xmlDstMsgId.Value;
                        emailMessage.Update ();
                    }
                }
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
                return Event.Create ((uint)SmEvt.E.Success, "MVSUCCESS");

            case Xml.Mov.StatusCode.SrcDestSame_4:
                Log.Error ("Attempted to move an email where the destination folder == the source folder.");
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded));
                return Event.Create ((uint)SmEvt.E.Success, "MVIDIOT");

            case Xml.Mov.StatusCode.ClobberOrMulti_5:
                /* "One of the following failures occurred: the item cannot be moved to more than
                 * one item at a time, or the source or destination item was locked."
                 * Since we are 1-at-a-time, we can assume the latter.
                 */
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed,
                        NcResult.WhyEnum.LockedOnServer));
                return Event.Create ((uint)SmEvt.E.Success, "MVGRINF");

            case Xml.Mov.StatusCode.Locked_7:
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl,
                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed, 
                        NcResult.WhyEnum.LockedOnServer));
                return Event.Create ((uint)(uint)AsProtoControl.AsEvt.E.ReSync, "MVSYNC");
            
            default:
                Log.Error ("Unknown status code in AsMoveItemsCommand response: {0}", status);
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl,
                    NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed,
                        NcResult.WhyEnum.Unknown));
                return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUS");
            }
        }
    }
}

