using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using SQLite;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McPending : McAbstrObjectPerAcc
    {
        // Parameterless constructor only here for use w/LINQ. Please only use w/accountId.
        public McPending ()
        {
            DefersRemaining = KMaxDeferCount;
            // TODO: Perhaps Id suffices?
            Token = Guid.NewGuid ().ToString ("N");
        }

        public McPending (int accountId) : this ()
        {
            AccountId = accountId;
        }

        public McPending (int accountId, McAbstrItem item) : this (accountId)
        {
            Item = item;
        }

        // These are the things that we can do.
        public enum Operations
        {
            FolderCreate,
            FolderUpdate,
            FolderDelete,
            EmailForward,
            EmailReply,
            EmailSend,
            EmailDelete,
            EmailMove,
            EmailMarkRead,
            EmailSetFlag,
            EmailClearFlag,
            EmailMarkFlagDone,
            EmailBodyDownload,
            // Note that pending searches aren't considered relevant across app
            // re-starts, and so they are purged from the DB on app launch.
            ContactSearch,
            ContactCreate,
            ContactUpdate,
            ContactDelete,
            ContactMove,
            ContactBodyDownload,
            CalCreate,
            CalUpdate,
            CalDelete,
            CalMove,
            CalRespond,
            CalBodyDownload,
            TaskCreate,
            TaskUpdate,
            TaskDelete,
            TaskMove,
            TaskBodyDownload,
            AttachmentDownload,
            Last = AttachmentDownload,
        };
        // Lifecycle of McPending:
        // - Protocol control API creates it (Eligible or PredBlocked) and puts it into the Q. Event goes to TL SM.
        // - XxxCommand marks it Dispatched and attempts to execute it against the server.
        // -- On success: StatusInd (maybe), then delete of McPending.
        // -- On temp failure: goto Deferred.
        // -- On hard failure: goto Failed.
        // -- On blocking failure: goto UserBlocked.
        // - When deferred, priority lowered, possibly not-before-time, retry-count.
        // -- On success: same.
        // -- On retry-count failure: transition to Failed.
        // - When Failed, sit in DB and wait for app to delete it.
        // - When UserBlocked, status-ind to app and sit in DB.
        // -- On app/ui fixing the issue, move state to deferred.
        // - When PredBlocked sit in DB until pred is done, then goto Eligible.
        public enum StateEnum
        {
            Eligible = 0,
            Dispatched = 1,
            Deferred = 2,
            UserBlocked = 3,
            PredBlocked = 4,
            Failed = 5,
            Deleted = 100,
        }

        public enum BlockReasonEnum
        {
            NotBlocked = 0,
            MustChangeName = 1,
            MustPickNewParent = 2,
            AdminRemediation = 3,
            UserRemediation = 4,
            Permanent_IllegalParent = 100,
        }

        public enum DeferredEnum
        {
            UntilSync,
            UntilFSync,
            UntilFSyncThenSync,
            UntilTime,
        };

        public enum XmlStatusKindEnum
        {
            TopLevel,
            Folder,
            Command,
        };

        public const double KDefaultDeferDelaySeconds = 60.0;
        public const uint KMaxDeferCount = 5;
        // Always valid.
        [Indexed]
        public float Priority { set; get; }
        // Always valid.
        [Indexed]
        public StateEnum State { set; get; }
        // Always valid.
        [Indexed]
        public string Token { set; get; }
        // Always valid.
        [Indexed]
        public Operations Operation { set; get; }
        // Valid when in Deferred state.
        [Indexed]
        // FIXME - need code in sync, fsync and here to manage this reason state.
        public DeferredEnum DeferredReason { set; get; }
        // FIXME - need code to make time part of the ready query.
        // Valid when in Deferred state and DeferredReason is UntilTime.
        [Indexed]
        public DateTime DeferredUntilTime { set; get; }
        // Valid when in Deferred state.
        public uint DefersRemaining { set; get; }
        // Valid when in Deferred state.
        [Indexed]
        public bool DeferredSerialIssueOnly { set; get; }
        // Valid when Deferred, Blocked, or Failed.
        public XmlStatusKindEnum ResponseXmlStatusKind { set; get; }
        // Valid when Deferred, Blocked, or Failed. 0 is unset.
        public uint ResponsegXmlStatus { set; get; }
        // Valid when Deferred, Blocked, or Failed. 0 is unset.
        public uint ResponseHttpStatusCode { set; get; }
        // Valid when in a Blocked state.
        public BlockReasonEnum BlockReason { set; get; }
        // Valid after a result has been created & status-ind'ed.
        public NcResult.KindEnum ResultKind { set; get; }
        // Valid after a result has been created & status-ind'ed.
        public NcResult.SubKindEnum ResultSubKind { set; get; }
        // Valid after a result has been created & status-ind'ed.
        public NcResult.WhyEnum ResultWhy { set; get; }

        // ****************************************************
        // GENERAL USE PROPERTIES
        // For FolderCreate, the value of ServerId is a provisional GUID.
        // The BE uses the GUID until the FolderCreate can be executed by the
        // server. After that, the GUID is then replaced by the server-supplied
        // ServerId value throughout the DB.
        [Indexed]
        public string ServerId { set; get; }

        [Indexed]
        // ParentId MUST be set for any Operation to be executed by Sync command!!!
        // ParentId indicates the folder containing the referenced FolderEntry, and indicates the "source" folder
        // in a move situation.
        public string ParentId { set; get; }

        // ONLY valid in a move scenario.
        public string DestParentId { set; get; }

        [Indexed]
        public string ClientId { set; get; }

        [Indexed]
        // ONLY to be used when there is content that needs to be referenced at the time when the
        // command is executed against the server (Create/Update/Send/Forward/Reply).
        public int ItemId { set; get; }

        // NOT a property. Used to increment refcount during Insert().
        private McAbstrItem Item;

        // ****************************************************
        // PROPERTIES SPECIFIC TO OPERATIONS (effectively subclasses)

        public string Search_Prefix { set; get; }

        public uint Search_MaxResults { set; get; }

        public string EmailSetFlag_FlagType { set; get; }

        public DateTime EmailSetFlag_Start { set; get; }

        public DateTime EmailSetFlag_UtcStart { set; get; }

        public DateTime EmailSetFlag_Due { set; get; }

        public DateTime EmailSetFlag_UtcDue { get; set; }

        public DateTime EmailMarkFlagDone_CompleteTime { get; set; }

        public DateTime EmailMarkFlagDone_DateCompleted { get; set; }

        [Indexed]
        public string DisplayName { set; get; }

        public Xml.MeetingResp.UserResponseCode CalResponse { set; get; }

        [Indexed]
        public int AttachmentId { set; get; }

        public bool Smart_OriginalEmailIsEmbedded { set; get; }

        public Xml.FolderHierarchy.TypeCode Folder_Type { set; get; }

        public const string KSynchronouslyCompleted	= "synchronously completed";



        // To be used by app/ui when dealing with McPending.
        // To be used by Commands when dealing with McPending.
        public void MarkDispached ()
        {
            State = StateEnum.Dispatched;
            Update ();
            Log.Info (Log.LOG_SYNC, "Pending:MarkDispached:{0}", Id);
        }

        public void MarkPredBlocked (int predPendingId)
        {
            State = StateEnum.PredBlocked;
            var dep = new McPendDep (predPendingId, Id);
            dep.Insert ();
            if (0 == Id) {
                Insert ();
            } else {
                Update ();
            }
            Log.Info (Log.LOG_SYNC, "Pending:MarkPredBlocked:{0}", Id);
        }

        private bool CanDepend ()
        {
            switch (Operation) {
            case Operations.FolderCreate:
            case Operations.FolderDelete:
            case Operations.FolderUpdate:
            case Operations.EmailForward:
            case Operations.CalRespond:
            case Operations.CalMove:
            case Operations.ContactMove:
            case Operations.EmailMove:
            case Operations.TaskMove:
            case Operations.CalCreate:
            case Operations.ContactCreate:
            case Operations.TaskCreate:
            case Operations.CalUpdate:
            case Operations.CalDelete:
            case Operations.ContactUpdate:
            case Operations.ContactDelete:
            case Operations.EmailClearFlag:
            case Operations.EmailMarkFlagDone:
            case Operations.EmailMarkRead:
            case Operations.EmailSetFlag:
            case Operations.EmailDelete:
            case Operations.TaskUpdate:
            case Operations.TaskDelete:
                return true;
            }
            return false;
        }

        private bool DependsUpon (McPending pred)
        {
            switch (Operation) {
            case Operations.EmailForward:
                return Smart_OriginalEmailIsEmbedded &&
                Operations.AttachmentDownload == pred.Operation &&
                pred.ServerId == ServerId;

            case Operations.FolderCreate:
                return Operations.FolderCreate == pred.Operation && pred.ServerId == ParentId;

            case Operations.FolderDelete:
                // FIXME - this could create too many McPendDeps. 
                return true;

            case Operations.FolderUpdate:
                return (Operations.FolderCreate == pred.Operation || Operations.FolderUpdate == pred.Operation)
                && pred.ServerId == ServerId;

            case Operations.CalRespond:
                return Operations.CalRespond == pred.Operation && pred.ServerId == ServerId;

            case Operations.CalMove:
            case Operations.ContactMove:
            case Operations.EmailMove:
            case Operations.TaskMove:
                switch (pred.Operation) {
                case Operations.FolderCreate:
                    return pred.ServerId == ParentId || pred.ServerId == DestParentId;

                case Operations.CalCreate:
                case Operations.CalUpdate:
                case Operations.ContactCreate:
                case Operations.ContactUpdate:
                case Operations.EmailClearFlag:
                case Operations.EmailMarkFlagDone:
                case Operations.EmailMarkRead:
                case Operations.EmailSetFlag:
                case Operations.TaskCreate:
                case Operations.TaskUpdate:
                    return pred.ServerId == ServerId;
                }
                return false;

            case Operations.CalCreate:
            case Operations.ContactCreate:
            case Operations.TaskCreate:
                return Operations.FolderCreate == pred.Operation && pred.ServerId == ParentId;

            case Operations.CalUpdate:
            case Operations.CalDelete:
            case Operations.ContactUpdate:
            case Operations.ContactDelete:
            case Operations.EmailClearFlag:
            case Operations.EmailMarkFlagDone:
            case Operations.EmailMarkRead:
            case Operations.EmailSetFlag:
            case Operations.EmailDelete:
            case Operations.TaskUpdate:
            case Operations.TaskDelete:
                return pred.ServerId == ServerId;
            }
            return false;
        }

        public void ResolveAsSuccess (ProtoControl control)
        {
            // Pick the default SubKind based on the Operation.
            // All Sync-command Ops must be covered. Non-Sync-commands need not be covered here.
            NcResult.SubKindEnum subKind;
            switch (Operation) {
            case Operations.EmailDelete:
                subKind = NcResult.SubKindEnum.Info_EmailMessageDeleteSucceeded;
                break;
            case Operations.EmailMarkRead:
                subKind = NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded;
                break;
            case Operations.EmailSetFlag:
                subKind = NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded;
                break;
            case Operations.EmailClearFlag:
                subKind = NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded;
                break;
            case Operations.EmailMarkFlagDone:
                subKind = NcResult.SubKindEnum.Info_EmailMessageMarkFlagDoneSucceeded;
                break;
            case Operations.CalCreate:
                subKind = NcResult.SubKindEnum.Info_CalendarCreateSucceeded;
                break;
            case Operations.CalUpdate:
                subKind = NcResult.SubKindEnum.Info_CalendarUpdateSucceeded;
                break;
            case Operations.CalDelete:
                subKind = NcResult.SubKindEnum.Info_CalendarDeleteSucceeded;
                break;
            case Operations.ContactCreate:
                subKind = NcResult.SubKindEnum.Info_ContactCreateSucceeded;
                break;
            case Operations.ContactUpdate:
                subKind = NcResult.SubKindEnum.Info_ContactUpdateSucceeded;
                break;
            case Operations.ContactDelete:
                subKind = NcResult.SubKindEnum.Info_ContactDeleteSucceeded;
                break;

            case Operations.TaskCreate:
                subKind = NcResult.SubKindEnum.Info_TaskCreateSucceeded;
                break;
            case Operations.TaskUpdate:
                subKind = NcResult.SubKindEnum.Info_TaskUpdateSucceeded;
                break;
            case Operations.TaskDelete:
                subKind = NcResult.SubKindEnum.Info_TaskDeleteSucceeded;
                break;

            default:
                throw new Exception (string.Format ("default subKind not specified for Operation {0}", Operation));
            }
            var result = NcResult.Info (subKind);
            ResolveAsSuccess (control, result);
        }

        public void ResolveAsSuccess (ProtoControl control, NcResult result)
        {
            // This is the designated ResolveAsSuccess.
            NcAssert.True (StateEnum.Dispatched == State);
            NcAssert.True (NcResult.KindEnum.Info == result.Kind);
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            if (null != result) {
                NcAssert.True (null != control);
                control.StatusInd (result, new [] { Token });
            }
            State = StateEnum.Deleted;
            Update ();
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsSuccess:{0}", Id);
            // FIXME: Find a clean way to send UpdateQ event to TL SM.
            UnblockSuccessors ();
            // Why update and then delete? I think we may want to defer deletion at some point.
            // If we do, then these are a good "log" of what has been done. So keep the records 
            // accurate.
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsSuccess:{0}", Id);
            Delete ();
        }

        public void ResolveAsCancelled (bool onlyDeferred)
        {
            // FIXME - need lock to ensure that pending state does not change while in this function.
            NcAssert.True (StateEnum.Dispatched == State || !onlyDeferred);
            State = StateEnum.Deleted;
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsCancelled:{0}", Id);
            Delete ();
        }

        public void ResolveAsCancelled ()
        {
            ResolveAsCancelled (true);
        }

        public void ResolveAsHardFail (ProtoControl control, NcResult result)
        {
            // This is the designated ResolveAsHardFail.
            NcAssert.True (StateEnum.Dispatched == State);
            NcAssert.True (NcResult.KindEnum.Error == result.Kind);
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            control.StatusInd (result, new [] { Token });
            State = StateEnum.Failed;
            Update ();
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsHardFail:{0}", Id);
        }

        private NcResult.SubKindEnum DefaultErrorSubKind ()
        {
            // Pick the default SubKind based on the Operation.
            // All Ops must be covered.
            switch (Operation) {
            case Operations.FolderCreate:
                return NcResult.SubKindEnum.Error_FolderCreateFailed;
            case Operations.FolderUpdate:
                return NcResult.SubKindEnum.Error_FolderUpdateFailed;
            case Operations.FolderDelete:
                return NcResult.SubKindEnum.Error_FolderDeleteFailed;
            case Operations.EmailForward:
                return NcResult.SubKindEnum.Error_EmailMessageForwardFailed;
            case Operations.EmailReply:
                return NcResult.SubKindEnum.Error_EmailMessageReplyFailed;
            case Operations.EmailSend:
                return NcResult.SubKindEnum.Error_EmailMessageSendFailed;
            case Operations.EmailDelete:
                return NcResult.SubKindEnum.Error_EmailMessageDeleteFailed;
            case Operations.EmailMove:
                return NcResult.SubKindEnum.Error_EmailMessageMoveFailed;
            case Operations.EmailMarkRead:
                return NcResult.SubKindEnum.Error_EmailMessageMarkedReadFailed;
            case Operations.EmailSetFlag:
                return NcResult.SubKindEnum.Error_EmailMessageSetFlagFailed;
            case Operations.EmailClearFlag:
                return NcResult.SubKindEnum.Error_EmailMessageClearFlagFailed;
            case Operations.EmailMarkFlagDone:
                return NcResult.SubKindEnum.Error_EmailMessageMarkFlagDoneFailed;
            case Operations.EmailBodyDownload:
                return NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed;
            case Operations.CalCreate:
                return NcResult.SubKindEnum.Error_CalendarCreateFailed;
            case Operations.CalUpdate:
                return NcResult.SubKindEnum.Error_CalendarUpdateFailed;
            case Operations.CalDelete:
                return NcResult.SubKindEnum.Error_CalendarDeleteFailed;
            case Operations.CalRespond:
                return NcResult.SubKindEnum.Error_MeetingResponseFailed;
            case Operations.CalBodyDownload:
                return NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed;
            case Operations.ContactCreate:
                return NcResult.SubKindEnum.Error_ContactCreateFailed;
            case Operations.ContactUpdate:
                return NcResult.SubKindEnum.Error_ContactUpdateFailed;
            case Operations.ContactDelete:
                return NcResult.SubKindEnum.Error_ContactDeleteFailed;
            case Operations.ContactBodyDownload:
                return NcResult.SubKindEnum.Error_ContactBodyDownloadFailed;
            case Operations.TaskCreate:
                return NcResult.SubKindEnum.Error_TaskCreateFailed;
            case Operations.TaskUpdate:
                return NcResult.SubKindEnum.Error_TaskUpdateFailed;
            case Operations.TaskDelete:
                return NcResult.SubKindEnum.Error_TaskDeleteFailed;
            case Operations.ContactSearch:
                return NcResult.SubKindEnum.Error_SearchCommandFailed;
            case Operations.AttachmentDownload:
                return NcResult.SubKindEnum.Error_AttDownloadFailed;

            default:
                throw new Exception (string.Format ("default subKind not specified for Operation {0}", Operation));
            }
        }

        // PUBLIC FOR TEST USE ONLY. OTHERWISE CONSIDER IT PRIVATE.
        public bool UnblockSuccessors ()
        {
            var successors = QuerySuccessors (AccountId, Id);
            McPendDep.DeleteAllSucc (Id);
            foreach (var succ in successors) {
                var remaining = McPendDep.QueryBySuccId (succ.Id);
                if (0 == remaining.Count ()) {
                    succ.State = StateEnum.Eligible;
                    succ.Update ();
                    Log.Info (Log.LOG_SYNC, "Pending:UnblockSuccessors:{0}=>{1}", Id, succ.Id);
                }
            }
            return (0 != successors.Count);
        }

        public static bool MakeEligibleOnFSync (int accountId)
        {
            var makeEligible = QueryDeferredFSync (accountId);
            foreach (var pending in makeEligible) {
                if (DeferredEnum.UntilFSyncThenSync == pending.DeferredReason) {
                    pending.DeferredReason = DeferredEnum.UntilSync;
                } else {
                    pending.State = StateEnum.Eligible;
                }
                pending.Update ();
                Log.Info (Log.LOG_SYNC, "Pending:MakeEligibleOnFSync:{0}", pending.Id);
            }
            return (0 != makeEligible.Count);
        }

        public static bool MakeEligibleOnSync (int accountId)
        {
            var makeEligible = QueryDeferredSync (accountId);
            foreach (var pending in makeEligible) {
                pending.State = StateEnum.Eligible;
                pending.Update ();
                Log.Info (Log.LOG_SYNC, "Pending:MakeEligibleOnSync:{0}", pending.Id);
            }
            return (0 != makeEligible.Count);
        }

        public static bool MakeEligibleOnTime (int accountId)
        {
            var makeEligible = QueryDeferredUntilNow (accountId);
            foreach (var pending in makeEligible) {
                pending.State = StateEnum.Eligible;
                pending.Update ();
                Log.Info (Log.LOG_SYNC, "Pending:MakeEligibleOnTime:{0}", pending.Id);
            }
            return (0 != makeEligible.Count);
        }
        // register for status-ind, look for FSync and Sync success.
        public void ResolveAsHardFail (ProtoControl control, NcResult.WhyEnum why)
        {
            var result = NcResult.Error (DefaultErrorSubKind (), why);
            ResolveAsHardFail (control, result);
        }

        public void ResolveAsDeferred (ProtoControl control, DeferredEnum reason, NcResult onFail)
        {
            NcAssert.True (StateEnum.Dispatched == State);
            // Added check in case of any bug causing underflow.
            if (0 >= DefersRemaining || KMaxDeferCount < DefersRemaining) {
                ResolveAsHardFail (control, onFail);
            } else {
                DefersRemaining--;
                DeferredReason = reason;
                State = StateEnum.Deferred;
                Update ();
                Log.Info (Log.LOG_SYNC, "Pending:ResolveAsDeferred:{0}", Id);
            }
        }

        public void ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult onFail)
        {
            DeferredReason = DeferredEnum.UntilTime;
            DeferredUntilTime = eligibleAfter;
            ResolveAsDeferred (control, DeferredEnum.UntilTime, onFail);
        }

        public void ResolveAsDeferred (ProtoControl control, DateTime eligibleAfter, NcResult.WhyEnum why)
        {
            var result = NcResult.Error (DefaultErrorSubKind (), why);
            ResolveAsDeferred (control, eligibleAfter, result);
        }

        public void ResolveAsDeferredForce (ProtoControl control)
        {
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsDeferredForce:{0}", Id);
            ResolveAsDeferred (control, DateTime.UtcNow, NcResult.WhyEnum.NotSpecified);
        }

        public void ResolveAsUserBlocked (ProtoControl control, BlockReasonEnum reason, NcResult result)
        {
            // This is the designated ResolveAsUserBlocked.
            NcAssert.True (StateEnum.Dispatched == State);
            NcAssert.True (NcResult.KindEnum.Error == result.Kind);
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            BlockReason = reason;
            control.StatusInd (result, new [] { Token });
            State = StateEnum.UserBlocked;
            Update ();
            Log.Info (Log.LOG_SYNC, "Pending:ResolveAsUserBlocked:{0}", Id);
        }

        public void ResolveAsUserBlocked (ProtoControl control, BlockReasonEnum reason, NcResult.WhyEnum why)
        {
            ResolveAsUserBlocked (control, reason, NcResult.Error (DefaultErrorSubKind (), why));
        }
        // Special-purpose resolve APIs for commands.
        public static void ResolvePendingSearchReqs (int accountId, string token, bool ignoreDispatched)
        {
            var query = NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
                        rec.Token == token);
            if (ignoreDispatched) {
                query = query.Where (rec => StateEnum.Dispatched != rec.State);
            }
            var killList = query.ToList ();
            foreach (var kill in killList) {
                kill.ResolveAsCancelled (false);
            }
        }

        public static void ResolveAllDispatchedAsDeferred (ProtoControl control, int accountId)
        {
            NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.State == StateEnum.Dispatched).All (y => {
                y.ResolveAsDeferredForce (control);
                return true;
            });
        }

        public override int Insert ()
        {
            var predIds = new List<int> ();
            try {
                NcModel.Instance.RunInTransaction (() => {
                    if (CanDepend ()) {
                        // Walk from the back toward the front of the Q looking for anything this pending might depend upon.
                        // If this gets to be expensive, we can implement a scoreboard (and possibly also RAM cache).
                        // Note that items might get deleted out from under us.
                        var pendq = Query (AccountId).OrderByDescending (x => x.Priority);
                        foreach (var elem in pendq) {
                            if (DependsUpon (elem)) {
                                predIds.Add (elem.Id);
                            }
                        }
                        if (0 != predIds.Count) {
                            State = StateEnum.PredBlocked;
                        }
                    }
                    if (null != Item) {
                        ItemId = Item.Id;
                        Item.PendingRefCount++;
                        Item.Update ();
                    }
                    base.Insert ();
                    Priority = Id;
                    base.Update ();
                    foreach (var predId in predIds) {
                        var pendDep = new McPendDep (predId, Id);
                        pendDep.Insert ();
                    }
                });
            } catch (SQLiteException ex) {
                Log.Error (Log.LOG_SYNC, "McPending.Insert: RunInTransaction: {0}", ex);
                return 0;
            }
            if (null != Item) {
                Log.Info (Log.LOG_SYNC, "Item {0}: PendingRefCount+: {1}", Item.Id, Item.PendingRefCount);
            }
            Log.Info (Log.LOG_SYNC, "Pending:Insert:{0}", Id);
            return 1;
        }

        public McAbstrItem QueryItemUsingServerId ()
        {
            switch (Operation) {
            case Operations.EmailMove:
                return McAbstrFolderEntry.QueryByServerId<McEmailMessage> (AccountId, ServerId);
            case Operations.CalUpdate:
            case Operations.CalMove:
                return McAbstrFolderEntry.QueryByServerId<McCalendar> (AccountId, ServerId);
            case Operations.ContactUpdate:
            case Operations.ContactMove:
                return McAbstrFolderEntry.QueryByServerId<McContact> (AccountId, ServerId);
            case Operations.TaskUpdate:
            case Operations.TaskMove:
                return McAbstrFolderEntry.QueryByServerId<McTask> (AccountId, ServerId);
            }
            return null;
        }

        public override int Delete ()
        {
            McAbstrItem item = null;
            try {
                NcModel.Instance.RunInTransaction (() => {
                    if (0 != ItemId) {
                        switch (Operation) {
                        case Operations.EmailSend:
                        case Operations.EmailForward:
                        case Operations.EmailReply:
                            item = McAbstrObject.QueryById<McEmailMessage> (ItemId);
                            break;

                        case Operations.CalCreate:
                        case Operations.CalUpdate:
                            item = McAbstrObject.QueryById<McCalendar> (ItemId);
                            break;

                        case Operations.ContactCreate:
                        case Operations.ContactUpdate:
                            item = McAbstrObject.QueryById<McContact> (ItemId);
                            break;

                        case Operations.TaskCreate:
                        case Operations.TaskUpdate:
                            item = McAbstrObject.QueryById<McTask> (ItemId);
                            break;

                        default:
                            Log.Error (Log.LOG_SYS, "Pending ItemId set to {0} for {1}.", ItemId, Operation);
                            NcAssert.True (false);
                            break;
                        }
                        NcAssert.NotNull (item);
                        NcAssert.True (0 < item.PendingRefCount);
                        item.PendingRefCount--;
                        item.Update ();
                        Log.Info (Log.LOG_SYNC, "Item {0}: PendingRefCount-: {1}", item.Id, item.PendingRefCount);
                        if (0 == item.PendingRefCount && item.IsAwaitingDelete) {
                            item.Delete ();
                        }
                    }
                    base.Delete ();
                });
            } catch (SQLiteException ex) {
                Log.Error (Log.LOG_SYNC, "McPending.Delete: RunInTransaction: {0}", ex);
                return 0;
            }
            Log.Info (Log.LOG_SYNC, "Pending:Delete:{0}", Id);
            return 1;
        }

        // Query APIs for any & all to call.
        public static List<McPending> Query (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                    .Where (x => x.AccountId == accountId)
                .OrderBy (x => x.Priority).ToList ();
        }

        public static IEnumerable<McPending> QueryEligible (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Eligible
            ).OrderBy (x => x.Priority);
        }

        public static List<McPending> QueryPredecessors (int accountId, int succId)
        {
            return NcModel.Instance.Db.Query<McPending> (
                "SELECT p.* FROM McPending AS p JOIN McPendDep AS m ON p.Id = m.PredId WHERE " +
                "p.AccountId = ? AND " +
                "m.SuccId = ? " +
                "ORDER BY Priority ASC",
                accountId, succId).ToList ();
        }

        public static List<McPending> QuerySuccessors (int accountId, int predId)
        {
            return NcModel.Instance.Db.Query<McPending> (
                "SELECT p.* FROM McPending AS p JOIN McPendDep AS m ON p.Id = m.SuccId WHERE " +
                "p.AccountId = ? AND " +
                "p.State = ? AND " +
                "m.PredId = ? " +
                "ORDER BY Priority ASC",
                accountId, (uint)StateEnum.PredBlocked, predId).ToList ();
        }

        public static List<McPending> QueryDeferredFSync (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            (rec.DeferredReason == DeferredEnum.UntilFSync ||
            rec.DeferredReason == DeferredEnum.UntilFSyncThenSync)).OrderBy (x => x.Priority).ToList ();
        }

        public static List<McPending> QueryDeferredSync (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            rec.DeferredReason == DeferredEnum.UntilSync).OrderBy (x => x.Priority).ToList ();
        }

        public static List<McPending> QueryDeferredUntilNow (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            rec.DeferredReason == DeferredEnum.UntilTime &&
            rec.DeferredUntilTime < DateTime.UtcNow
            ).OrderBy (x => x.Priority).ToList ();
        }

        public static IEnumerable<McPending> QueryByToken (int accountId, string token)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (x => 
                x.AccountId == accountId &&
            x.Token == token);
        }

        public static List<McPending> QueryByOperation (int accountId, McPending.Operations operation)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                    .Where (rec =>
                        rec.AccountId == accountId &&
            rec.Operation == operation).OrderBy (x => x.Priority).ToList ();
        }

        public static McPending QueryFirstEligibleByOperation (int accountId, McPending.Operations operation)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                        rec.AccountId == accountId &&
            rec.Operation == operation &&
            rec.State == StateEnum.Eligible).OrderBy (x => x.Priority).FirstOrDefault ();
        }

        public static IEnumerable<McPending> QueryFirstNEligibleByOperation (int accountId, 
            McPending.Operations operation, int n)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
                rec.Operation == operation &&
            rec.State == StateEnum.Eligible).OrderBy (x => x.Id).Take (n);
        }

        public static McPending QueryByClientId (int accountId, string clientId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                        rec.AccountId == accountId &&
            rec.ClientId == clientId).OrderBy (x => x.Priority).FirstOrDefault ();
        }

        public static List<McPending> QueryEligibleByFolderServerId (int accountId, string folderServerId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                    .Where (rec =>
                        rec.AccountId == accountId &&
            rec.ParentId == folderServerId &&
            rec.State == StateEnum.Eligible).OrderBy (x => x.Priority).ToList ();
        }

        public static McPending QueryByServerId (int accountId, string serverId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                        rec.AccountId == accountId &&
            rec.ServerId == serverId).OrderBy (x => x.Priority).FirstOrDefault ();
        }

        public static McPending QueryByAttachmentId (int accountId, int AttachmentId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
                    rec.AttachmentId == AttachmentId
            ).FirstOrDefault ();
        }

        public class ReWrite
        {
            public enum ObjActionEnum
            {
                ReWriteServerParentIdString,
            };

            public ObjActionEnum ObjAction;
            public string MatchString;
            public string ReplaceString;
        }

        public enum DbActionEnum
        {
            DoNothing,
            Update,
            Delete,
        };

        public DbActionEnum ApplyReWrites (List<ReWrite> reWrites)
        {
            bool updateNeeded = false;
            foreach (var reWrite in reWrites) {
                switch (reWrite.ObjAction) {
                case ReWrite.ObjActionEnum.ReWriteServerParentIdString:
                    if (ServerId == reWrite.MatchString) {
                        ServerId = reWrite.ReplaceString;
                        updateNeeded = true;
                    }
                    if (ParentId == reWrite.MatchString) {
                        ParentId = reWrite.ReplaceString;
                        updateNeeded = true;
                    }
                    if (DestParentId == reWrite.MatchString) {
                        DestParentId = reWrite.ReplaceString;
                        updateNeeded = true;
                    }
                    break;
                }
            }
            return (updateNeeded) ? DbActionEnum.Update : DbActionEnum.DoNothing;
        }

        public void ConvertToEmailSend ()
        {
            NcAssert.True (false);
            // FIXME. NYI.
        }

        public bool CommandDominatesParentId (string cmdServerId)
        {
            return McPath.Dominates (AccountId, cmdServerId, ParentId);
        }

        public bool CommandDominatesServerId (string cmdServerId)
        {
            return McPath.Dominates (AccountId, cmdServerId, ServerId);
        }

        public bool CommandDominatesDestParentId (string cmdServerId)
        {
            return McPath.Dominates (AccountId, cmdServerId, DestParentId);
        }

        public bool CommandDominatesItem (string cmdServerId)
        {
            var item = GetItem ();
            NcAssert.NotNull (item);
            return (item.ServerId == cmdServerId || McPath.Dominates (AccountId, cmdServerId, item.ServerId));
        }

        public bool ServerIdDominatesCommand (string cmdServerId)
        {
            return McPath.Dominates (AccountId, ServerId, cmdServerId);
        }

        // returns item associated with pending
        public McAbstrItem GetItem ()
        {
            switch (Operation) {
            case Operations.CalCreate:
            case Operations.CalDelete:
            case Operations.CalMove:
            case Operations.CalRespond:
            case Operations.CalUpdate:
                return McCalendar.QueryById<McCalendar> (ItemId);
            case Operations.ContactCreate:
            case Operations.ContactDelete:
            case Operations.ContactMove:
            case Operations.ContactSearch:
            case Operations.ContactUpdate:
                return McContact.QueryById<McContact> (ItemId);
            case Operations.EmailClearFlag:
            case Operations.EmailDelete:
            case Operations.EmailForward:
            case Operations.EmailMarkFlagDone:
            case Operations.EmailMarkRead:
            case Operations.EmailMove:
            case Operations.EmailReply:
            case Operations.EmailSend:
            case Operations.EmailSetFlag:
                return McContact.QueryById<McEmailMessage> (ItemId);
            case Operations.TaskCreate:
            case Operations.TaskDelete:
            case Operations.TaskMove:
            case Operations.TaskUpdate:
                return McTask.QueryById<McTask> (ItemId);
            default:
                return null;
            }
        }
    }
}

