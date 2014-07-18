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
        private McAbstrItem.ClassCodeEnum ClassCode;

        public AsMoveItemsCommand (IBEContext beContext) : base (Xml.Mov.MoveItems, Xml.Mov.Ns, beContext)
        {
            PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.EmailMove);
            if (null != PendingSingle) {
                ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email;
                LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded);
                LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_EmailMessageMoveFailed);
            } else {
                PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.CalMove);
                if (null != PendingSingle) {
                    ClassCode = McAbstrFolderEntry.ClassCodeEnum.Calendar;
                    LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_CalendarMoveSucceeded);
                    LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_CalendarMoveFailed);
                } else {
                    PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.ContactMove);
                    if (null != PendingSingle) {
                        ClassCode = McAbstrFolderEntry.ClassCodeEnum.Contact;
                        LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_ContactMoveSucceeded);
                        LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_ContactMoveFailed);
                    } else {
                        ClassCode = McAbstrFolderEntry.ClassCodeEnum.Tasks;
                        PendingSingle = McPending.QueryFirstEligibleByOperation (BEContext.Account.Id, McPending.Operations.TaskMove);
                        LocalSuccessInd = NcResult.Info (NcResult.SubKindEnum.Info_TaskMoveSucceeded);
                        LocalFailureInd = NcResult.Error (NcResult.SubKindEnum.Error_TaskMoveFailed);
                    }
                }
            }
            NcAssert.True (null != PendingSingle);
            PendingSingle.MarkDispached ();
        }

        public override XDocument ToXDocument (AsHttpOperation Sender)
        {
            NcAssert.True (null != PendingSingle);
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
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSyncThenSync, LocalFailureInd);
                });
                McFolder.AsSetExpected (BEContext.Account.Id);
                return Event.Create (new Event[] {
                    Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIIS1"),
                    Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "MIIS2"),
                });

            case Xml.Mov.StatusCode.InvalidDest_2:
                LocalFailureInd.Why = NcResult.WhyEnum.MissingOnServer;
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSync, LocalFailureInd);
                });
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIID");

            case Xml.Mov.StatusCode.Success_3:
                var xmlSrcMsgId = xmlResponse.Element (m_ns + Xml.Mov.SrcMsgId);
                if (null == xmlSrcMsgId || null == xmlSrcMsgId.Value) {
                    return Event.Create ((uint)SmEvt.E.HardFail, "MINOSRC");
                }
                var oldServerId = xmlSrcMsgId.Value;
                var newServerId = oldServerId;
                var xmlDstMsgId = xmlResponse.Element (m_ns + Xml.Mov.DstMsgId);
                if (null != xmlDstMsgId) {
                    // We need to re-write the ServerId.
                    newServerId = xmlDstMsgId.Value;
                    McAbstrItem item = null;
                    switch (ClassCode) {
                    case McAbstrFolderEntry.ClassCodeEnum.Email:
                        item = McAbstrItem.QueryByServerId<McEmailMessage> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                        item = McAbstrItem.QueryByServerId<McCalendar> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Contact:
                        item = McAbstrItem.QueryByServerId<McContact> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    case McAbstrFolderEntry.ClassCodeEnum.Tasks:
                        item = McAbstrItem.QueryByServerId<McTask> (BEContext.Account.Id, PendingSingle.ServerId);
                        break;

                    default:
                        NcAssert.True (false);
                        break;
                    }
                    if (null != item) {
                        // The item may have been subsequently deleted.
                        item.ServerId = newServerId;
                        item.Update ();
                    }
                }
                var pathElem = McPath.QueryByServerId (BEContext.Account.Id, oldServerId);
                pathElem.Delete ();
                pathElem = new McPath (BEContext.Account.Id);
                pathElem.ServerId = newServerId;
                pathElem.ParentId = PendingSingle.DestParentId;
                pathElem.Insert ();

                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, LocalSuccessInd);
                });
                return Event.Create ((uint)SmEvt.E.Success, "MVSUCCESS");

            case Xml.Mov.StatusCode.SrcDestSame_4:
                Log.Error (Log.LOG_AS, "Attempted to move where the destination folder == the source folder.");
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, LocalSuccessInd);
                });
                return Event.Create ((uint)SmEvt.E.Success, "MVIDIOT");

            case Xml.Mov.StatusCode.ClobberOrMulti_5:
                /* "One of the following failures occurred: the item cannot be moved to more than
                 * one item at a time, or the source or destination item was locked."
                 * Since we are 1-at-a-time, we can assume the latter.
                 */
                LocalFailureInd.Why = NcResult.WhyEnum.LockedOnServer;
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, LocalFailureInd);
                });
                return Event.Create ((uint)SmEvt.E.Success, "MVGRINF");

            case Xml.Mov.StatusCode.Locked_7:
                LocalFailureInd.Why = NcResult.WhyEnum.LockedOnServer;
                PendingResolveApply ((pending) => {
                    pending.ResolveAsDeferred (BEContext.ProtoControl,
                        DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds), LocalFailureInd);
                });
                McFolder.AsSetExpected (BEContext.Account.Id);
                return Event.Create ((uint)(uint)AsProtoControl.AsEvt.E.ReSync, "MVSYNC");
            
            default:
                Log.Error (Log.LOG_AS, "Unknown status code in AsMoveItemsCommand response: {0}", status);
                LocalFailureInd.Why = NcResult.WhyEnum.Unknown;
                PendingResolveApply ((pending) => {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, LocalFailureInd);
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "MVUNKSTATUS");
            }
        }
    }
}

