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
        private NcResult LocalFailureInd;
        private NcResult LocalSuccessInd;
        private McItem.ClassCodeEnum ClassCode;

        public AsMoveItemsCommand (IBEContext beContext) : base (Xml.Mov.MoveItems, Xml.Mov.Ns, beContext)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.EmailMove);
            if (null != PendingSingle) {
                ClassCode = McFolderEntry.ClassCodeEnum.Email;
                LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded);
                LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed);
            } else {
                PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.CalMove);
                if (null != PendingSingle) {
                    ClassCode = McFolderEntry.ClassCodeEnum.Calendar;
                    LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_CalendarMoveSucceeded);
                    LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_CalendarMoveFailed);
                } else {
                    PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.ContactMove);
                    if (null != PendingSingle) {
                        ClassCode = McFolderEntry.ClassCodeEnum.Contact;
                        LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_ContactMoveSucceeded);
                        LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_ContactMoveFailed);
                    } else {
                        ClassCode = McFolderEntry.ClassCodeEnum.Tasks;
                        PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.TaskMove);
                        LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_TaskMoveSucceeded);
                        LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_TaskMoveFailed);
                    }
                }
            }
            NachoAssert.True (null != PendingSingle);
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
                LocalFailureInd.Why = NcResult.WhyEnum.MissingOnServer;
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSyncThenSync, LocalFailureInd);
                return Event.Create (new Event[] {
                    Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIIS1"),
                    Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "MIIS2"),
                });

            case Xml.Mov.StatusCode.InvalidDest_2:
                LocalFailureInd.Why = NcResult.WhyEnum.MissingOnServer;
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSync, LocalFailureInd);
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIID");

            case Xml.Mov.StatusCode.Success_3:
                var xmlDstMsgId = xmlResponse.Element (m_ns + Xml.Mov.DstMsgId);
                if (null != xmlDstMsgId) {
                    // We need to re-write the ServerId. TODO verify that SrcMsgId matches pending's ServerId.
                    var SrcMsgId = xmlResponse.Element (m_ns + Xml.Mov.SrcMsgId).Value;
                    McItem item = null;
                    switch (ClassCode) {
                    case McFolderEntry.ClassCodeEnum.Email:
                        item = McItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McFolderEntry.ClassCodeEnum.Calendar:
                        item = McItem.QueryByServerId<McCalendar> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McFolderEntry.ClassCodeEnum.Contact:
                        item = McItem.QueryByServerId<McContact> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McFolderEntry.ClassCodeEnum.Tasks:
                        item = McItem.QueryByServerId<McTask> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    default:
                        NachoAssert.True (false);
                        break;
                    }
                    if (null != item) {
                        // The item may have been subsequently deleted.
                        item.ServerId = xmlDstMsgId.Value;
                        item.Update ();
                    }
                }
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, LocalSuccessInd);
                return Event.Create ((uint)SmEvt.E.Success, "MVSUCCESS");

            case Xml.Mov.StatusCode.SrcDestSame_4:
                Log.Error ("Attempted to move where the destination folder == the source folder.");
                PendingSingle.ResolveAsSuccess (BEContext.ProtoControl, LocalSuccessInd);
                return Event.Create ((uint)SmEvt.E.Success, "MVIDIOT");

            case Xml.Mov.StatusCode.ClobberOrMulti_5:
                /* "One of the following failures occurred: the item cannot be moved to more than
                 * one item at a time, or the source or destination item was locked."
                 * Since we are 1-at-a-time, we can assume the latter.
                 */
                LocalFailureInd.Why = NcResult.WhyEnum.LockedOnServer;
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, LocalFailureInd);
                return Event.Create ((uint)SmEvt.E.Success, "MVGRINF");

            case Xml.Mov.StatusCode.Locked_7:
                LocalFailureInd.Why = NcResult.WhyEnum.LockedOnServer;
                PendingSingle.ResolveAsDeferred (BEContext.ProtoControl,
                    DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds), LocalFailureInd);
                return Event.Create ((uint)(uint)AsProtoControl.AsEvt.E.ReSync, "MVSYNC");
            
            default:
                Log.Error ("Unknown status code in AsMoveItemsCommand response: {0}", status);
                LocalFailureInd.Why = NcResult.WhyEnum.Unknown;
                PendingSingle.ResolveAsHardFail (BEContext.ProtoControl, LocalFailureInd);
                return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUS");
            }
        }
    }
}

