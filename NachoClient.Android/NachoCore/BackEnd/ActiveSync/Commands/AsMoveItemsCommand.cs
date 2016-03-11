//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Xml.Linq;
using System.Threading;
using NachoCore.ActiveSync;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsMoveItemsCommand : AsCommand
    {
        private MoveKit MoveKit;

        public AsMoveItemsCommand (IBEContext beContext, MoveKit moveKit) : 
            base (Xml.Mov.MoveItems, Xml.Mov.Ns, beContext)
        {
            MoveKit = moveKit;
            PendingList.AddRange (MoveKit.Pendings);
            NcModel.Instance.RunInTransaction (() => {
                foreach (var pending in PendingList) {
                    pending.MarkDispatched ();
                }
            });
        }

        protected override bool RequiresPending ()
        {
            return true;
        }

        protected override XDocument ToXDocument (AsHttpOperation Sender)
        {
            var moveItems = new XElement (m_ns + Xml.Mov.MoveItems);
            foreach (var pending in PendingList) {
                moveItems.Add (new XElement (m_ns + Xml.Mov.Move,
                    new XElement (m_ns + Xml.Mov.SrcMsgId, pending.ServerId),
                    new XElement (m_ns + Xml.Mov.SrcFldId, pending.ParentId),
                    new XElement (m_ns + Xml.Mov.DstFldId, pending.DestParentId)));
            }
            var doc = AsCommand.ToEmptyXDocument ();
            doc.Add (moveItems);
            return doc;
        }

        private McPending FindPending (XElement xmlSrcMsgId)
        {
            return PendingList.Where (x => x.ServerId == xmlSrcMsgId.Value).FirstOrDefault ();
        }

        private McAbstrFolderEntry.ClassCodeEnum? FindClassCode (McPending pending)
        {
            var index = MoveKit.Pendings.IndexOf (pending);
            if (0 > index) {
                Log.Error (Log.LOG_AS, "MoveItems: FindClassCode: McPending not in list");
                return null;
            }
            return MoveKit.ClassCodes [index];
        }

        private NcResult.SubKindEnum AppropriateSubKind (McPending pending, bool didSucceed)
        {
            var classCode = FindClassCode (pending);
            if (null != classCode) {
                switch (classCode) {
                case McAbstrFolderEntry.ClassCodeEnum.Email:
                    return (didSucceed) ? NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded : NcResult.SubKindEnum.Error_EmailMessageMoveFailed;
                case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                    return (didSucceed) ? NcResult.SubKindEnum.Info_CalendarMoveSucceeded : NcResult.SubKindEnum.Error_CalendarMoveFailed;
                case McAbstrFolderEntry.ClassCodeEnum.Contact:
                    return (didSucceed) ? NcResult.SubKindEnum.Info_ContactMoveSucceeded : NcResult.SubKindEnum.Error_ContactMoveFailed;
                case McAbstrFolderEntry.ClassCodeEnum.Tasks:
                    return (didSucceed) ? NcResult.SubKindEnum.Info_TaskMoveSucceeded : NcResult.SubKindEnum.Error_TaskMoveFailed;
                default:
                    Log.Error (Log.LOG_AS, "MoveItems: AppropriateSubKind: Unknown classCode {0}", classCode);
                    break;
                }
            }
            // If something is screwed up, use email as a default. We already logged above.
            return (didSucceed) ? NcResult.SubKindEnum.Info_EmailMessageMoveSucceeded : NcResult.SubKindEnum.Error_EmailMessageMoveFailed;
        }

        public override Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            if (!SiezePendingCleanup ()) {
                return Event.Create ((uint)SmEvt.E.TempFail, "MICANCEL");
            }
            bool mustReFSync = false;
            bool mustReSync = false;
            var xmlMoveItems = doc.Root;
            var xmlStatusTop = xmlMoveItems.Element (m_ns + Xml.Mov.Status);
            if (null != xmlStatusTop) {
                switch ((Xml.Mov.StatusCode)xmlStatusTop.Value.ToInt ()) {
                case Xml.Mov.StatusCode.ClobberOrMulti_5:
                    return CompleteAsTempFail ((uint)Xml.Mov.StatusCode.ClobberOrMulti_5);
                }
            }
            var xmlResponses = xmlMoveItems.Elements (m_ns + Xml.Mov.Response);
            foreach (var xmlResponse in xmlResponses) {
                var xmlStatus = xmlResponse.Element (m_ns + Xml.Mov.Status);
                if (null == xmlStatus) {
                    Log.Error (Log.LOG_AS, "MoveItems: missing Status in Response");
                    continue;
                }
                var xmlSrcMsgId = xmlResponse.Element (m_ns + Xml.Mov.SrcMsgId);
                if (null == xmlSrcMsgId) {
                    Log.Error (Log.LOG_AS, "MoveItems: missing SrcMsgId in Response with Status {0}", xmlStatus.Value);
                    continue;
                }
                var pending = FindPending (xmlSrcMsgId);
                if (null == pending) {
                    Log.Error (Log.LOG_AS, "MoveItems: can't find McPending with ServerId {0}", xmlSrcMsgId.Value);
                    continue;
                }
                switch ((Xml.Mov.StatusCode)xmlStatus.Value.ToInt ()) {
                case Xml.Mov.StatusCode.InvalidSrc_1:
                    mustReFSync = true;
                    mustReSync = true;
                    pending.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSyncThenSync,
                        NcResult.Error (AppropriateSubKind (pending, false), NcResult.WhyEnum.MissingOnServer));
                    McFolder.UpdateSet_AsSyncMetaToClientExpected (AccountId, true);
                    PendingList.Remove (pending);
                    break;

                case Xml.Mov.StatusCode.InvalidDest_2:
                    mustReFSync = true;
                    pending.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFSync,
                        NcResult.Error (AppropriateSubKind (pending, false), NcResult.WhyEnum.MissingOnServer));
                    PendingList.Remove (pending);
                    break;

                case Xml.Mov.StatusCode.Success_3:
                    var oldServerId = xmlSrcMsgId.Value;
                    var newServerId = oldServerId;
                    var xmlDstMsgId = xmlResponse.Element (m_ns + Xml.Mov.DstMsgId);
                    NcModel.Instance.RunInTransaction (() => {
                        if (null != xmlDstMsgId && xmlDstMsgId.Value != oldServerId) {
                            // We need to re-write the ServerId.
                            newServerId = xmlDstMsgId.Value;
                            McAbstrItem item = null;
                            var classCode = FindClassCode (pending);
                            if (null != classCode) {
                                switch (classCode) {
                                case McAbstrFolderEntry.ClassCodeEnum.Email:
                                    item = McAbstrItem.QueryByServerId<McEmailMessage> (AccountId, pending.ServerId);
                                    break;

                                case McAbstrFolderEntry.ClassCodeEnum.Calendar:
                                    item = McAbstrItem.QueryByServerId<McCalendar> (AccountId, pending.ServerId);
                                    break;

                                case McAbstrFolderEntry.ClassCodeEnum.Contact:
                                    item = McAbstrItem.QueryByServerId<McContact> (AccountId, pending.ServerId);
                                    break;

                                case McAbstrFolderEntry.ClassCodeEnum.Tasks:
                                    item = McAbstrItem.QueryByServerId<McTask> (AccountId, pending.ServerId);
                                    break;

                                default:
                                    Log.Error (Log.LOG_AS, "AsMoveItemsCommand: Unknown classCode: {0}", classCode);
                                    break;
                                }
                                if (null != item) {
                                    // The item may have been subsequently deleted.
                                    if (item is McEmailMessage) {
                                        item = item.UpdateWithOCApply<McEmailMessage> ((record) => {
                                            var target = (McEmailMessage)record;
                                            target.ServerId = newServerId;
                                            return true;
                                        });
                                    } else {
                                        item.ServerId = newServerId;
                                        item.Update ();
                                    }
                                }
                            }
                        }
                        var pathElem = McPath.QueryByServerId (AccountId, oldServerId);
                        if (null == pathElem) {
                            Log.Error (Log.LOG_AS, "AsMoveItemsCommand: can't find McPath for {0}", oldServerId);
                        } else {
                            pathElem.Delete ();
                        }
                        pathElem = new McPath (AccountId);
                        pathElem.WasMoveDest = true;
                        pathElem.ServerId = newServerId;
                        pathElem.ParentId = pending.DestParentId;
                        pathElem.Insert ();
                    });
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (AppropriateSubKind (pending, true)));
                    PendingList.Remove (pending);
                    break;

                case Xml.Mov.StatusCode.SrcDestSame_4:
                    Log.Error (Log.LOG_AS, "MoveItems: Attempted to move where the destination folder == the source folder.");
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (AppropriateSubKind (pending, true)));
                    PendingList.Remove (pending);
                    break;

                case Xml.Mov.StatusCode.ClobberOrMulti_5:
                    /* Per-spec, this is supposed to be only a global status value.
                     * "One of the following failures occurred: the item cannot be moved to more than
                     * one item at a time, or the source or destination item was locked."
                     * Since we won't include the same ServerId twice in PendingList, we can assume the latter.
                     */
                    pending.ResolveAsDeferred (BEContext.ProtoControl, 
                        DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds), 
                        NcResult.Error (AppropriateSubKind (pending, false), NcResult.WhyEnum.LockedOnServer));
                    PendingList.Remove (pending);
                    break;

                case Xml.Mov.StatusCode.Locked_7:
                    mustReSync = true;
                    pending.ResolveAsDeferred (BEContext.ProtoControl,
                        DateTime.UtcNow.AddSeconds (McPending.KDefaultDeferDelaySeconds),
                        NcResult.Error (AppropriateSubKind (pending, false), NcResult.WhyEnum.LockedOnServer));
                    McFolder.UpdateSet_AsSyncMetaToClientExpected (AccountId, true);
                    PendingList.Remove (pending);
                    break;

                default:
                    Log.Error (Log.LOG_AS, "Unknown status code in AsMoveItemsCommand response: {0}", xmlStatus.Value);
                    pending.ResolveAsHardFail (BEContext.ProtoControl,
                        NcResult.Error (AppropriateSubKind (pending, false), NcResult.WhyEnum.Unknown));
                    PendingList.Remove (pending);
                    break;
                }
            }
            // Decide which event(s) to return.
            if (mustReFSync && mustReSync) {
                return Event.Create (new Event[] {
                    Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIIS1"),
                    Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "MIIS2"),
                });
            } else if (mustReFSync) {
                return Event.Create ((uint)AsProtoControl.CtlEvt.E.ReFSync, "MIID");
            } else if (mustReSync) {
                return Event.Create ((uint)(uint)AsProtoControl.AsEvt.E.ReSync, "MVSYNC");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "MVSUCCESS");
            }
        }
    }
}

