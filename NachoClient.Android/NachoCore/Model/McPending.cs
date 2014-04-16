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
    public class McPending : McObjectPerAccount
    {
        // Parameterless constructor only here for use w/LINQ. Please only use w/accountId.
        public McPending ()
        {
            DefersRemaining = KMaxDeferCount;
            Token = DateTime.UtcNow.Ticks.ToString (); // FIXME - use Id?
        }

        public McPending (int accountId) : this ()
        {
            AccountId = accountId;
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
            AttachmentDownload,
            // Note that pending searches aren't considered relevant across app
            // re-starts, and so they are purged from the DB on app launch.
            ContactSearch,
            ContactCreate,
            ContactUpdate,
            ContactDelete,
            EmailDelete,
            EmailMove,
            EmailMarkRead,
            EmailSetFlag,
            EmailClearFlag,
            EmailMarkFlagDone,
            CalCreate,
            CalUpdate,
            CalDelete,
            CalRespond,
            TaskCreate,
            TaskUpdate,
            TaskDelete,
        };
        // Lifecycle of McPening:
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
        public StateEnum State { set; get; }
        // Always valid.
        [Indexed]
        public string Token { set; get; }
        // Always valid.
        [Indexed]
        public Operations Operation { set; get; }
        // The number of paths that are stored.
        public uint PathCount { set; get; }
        // Valid when in PredBlocked state.
        [Indexed]
        public int PredPendingId { set; get; }
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

        public Xml.FolderHierarchy.TypeCode FolderCreate_Type { set; get; }

        public const string KSynchronouslyCompleted	= "synchronously completed";



        // To be used by app/ui when dealing with McPending.
        // To be used by Commands when dealing with McPending.
        public void MarkDispached ()
        {
            State = StateEnum.Dispatched;
            Update ();
        }

        public void MarkPredBlocked (int predPendingId)
        {
            State = StateEnum.PredBlocked;
            PredPendingId = predPendingId;
            if (0 == Id) {
                Insert ();
            } else {
                Update ();
            }
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
            case Operations.TaskUpdate:
            case Operations.TaskDelete:
                // FIXME!
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
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            if (null != result) {
                NachoAssert.True (null != control);
                control.StatusInd (result, new [] { Token });
            }
            State = StateEnum.Deleted;
            Update ();
            // FIXME: Find a clean way to send UpdateQ event to TL SM.
            UnblockSuccessors ();
            // Why update and then delete? I think we may want to defer deletion at some point.
            // If we do, then these are a good "log" of what has been done. So keep the records 
            // accurate.
            Delete ();
        }

        public void ResolveAsCancelled ()
        {
            State = StateEnum.Deleted;
            Delete ();
        }

        public void ResolveAsHardFail (ProtoControl control, NcResult result)
        {
            // This is the designated ResolveAsHardFail.
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            control.StatusInd (result, new [] { Token });
            State = StateEnum.Failed;
            Update ();
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
            case Operations.AttachmentDownload:
                return NcResult.SubKindEnum.Error_AttDownloadFailed;
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
            case Operations.CalCreate:
                return NcResult.SubKindEnum.Error_CalendarCreateFailed;
            case Operations.CalUpdate:
                return NcResult.SubKindEnum.Error_CalendarUpdateFailed;
            case Operations.CalDelete:
                return NcResult.SubKindEnum.Error_CalendarDeleteFailed;
            case Operations.CalRespond:
                return NcResult.SubKindEnum.Error_MeetingResponseFailed;
            case Operations.ContactCreate:
                return NcResult.SubKindEnum.Error_ContactCreateFailed;
            case Operations.ContactUpdate:
                return NcResult.SubKindEnum.Error_ContactUpdateFailed;
            case Operations.ContactDelete:
                return NcResult.SubKindEnum.Error_ContactDeleteFailed;
            case Operations.ContactSearch:
                return NcResult.SubKindEnum.Error_SearchCommandFailed;

                // FIXME TASKS.
            default:
                throw new Exception (string.Format ("default subKind not specified for Operation {0}", Operation));
            }
        }

        private bool UnblockSuccessors ()
        {
            var successors = QuerySuccessors (AccountId, Id);
            foreach (var succ in successors) {
                succ.PredPendingId = 0;
                succ.State = StateEnum.Eligible;
                succ.Update ();
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
            }
            return (0 != makeEligible.Count);
        }

        public static bool MakeEligibleOnSync (int accountId)
        {
            var makeEligible = QueryDeferredSync (accountId);
            foreach (var pending in makeEligible) {
                pending.State = StateEnum.Eligible;
                pending.Update ();
            }
            return (0 != makeEligible.Count);
        }

        public static bool MakeEligibleOnTime (int accountId)
        {
            var makeEligible = QueryDeferredUntilNow (accountId);
            foreach (var pending in makeEligible) {
                pending.State = StateEnum.Eligible;
                pending.Update ();
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
            if (0 >= DefersRemaining) {
                ResolveAsHardFail (control, onFail);
            }
            DefersRemaining--;
            DeferredReason = reason;
            State = StateEnum.Deferred;
            Update ();
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

        public void ResolveAsDeferredForce ()
        {
            State = StateEnum.Deferred;
            DeferredReason = DeferredEnum.UntilTime;
            DeferredUntilTime = DateTime.UtcNow;
            Update ();
        }

        public void ResolveAsUserBlocked (ProtoControl control, BlockReasonEnum reason, NcResult result)
        {
            // This is the designated ResoveAsUserBlocked.
            ResultKind = result.Kind;
            ResultSubKind = result.SubKind;
            ResultWhy = result.Why;
            BlockReason = reason;
            control.StatusInd (result, new [] { Token });
            State = StateEnum.UserBlocked;
            Update ();
        }

        public void ResolveAsUserBlocked (ProtoControl control, BlockReasonEnum reason, NcResult.WhyEnum why)
        {
            ResolveAsUserBlocked (control, reason, NcResult.Error (DefaultErrorSubKind (), why));
        }
        // Special-purpose resolve APIs for commands.
        public static void ResolvePendingSearchReqs (int accountId, string token, bool ignoreDispatched)
        {
            var query = BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
                        rec.Token == token);
            if (ignoreDispatched) {
                query = query.Where (rec => StateEnum.Dispatched != rec.State);
            }
            var killList = query.ToList ();
            foreach (var kill in killList) {
                kill.ResolveAsCancelled ();
            }
        }

        public static void ResolveAllDispatchedAsDeferred (int accountId)
        {
            BackEnd.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.State == StateEnum.Dispatched).All (y => {
                y.ResolveAsDeferredForce ();
                return true;
            });
        }

        // Query APIs for any & all to call.
        public static List<McPending> Query (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .Where (x => x.AccountId == accountId)
                    .OrderBy (x => x.Id).ToList ();
        }

        public static McPending GetOldestYoungerThanId (int accountId, int priorId)
        {
            return Query (accountId).FirstOrDefault<McPending> (x => x.Id > priorId);
        }

        public static List<McPending> QueryEligible (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Eligible
            ).OrderBy (x => x.Id).ToList ();
        }

        public static List<McPending> QuerySuccessors (int accountId, int predId)
        {
            return BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.PredBlocked &&
            rec.PredPendingId == predId
            ).OrderBy (x => x.Id).ToList ();
        }

        public static List<McPending> QueryDeferredFSync (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            (rec.DeferredReason == DeferredEnum.UntilFSync ||
            rec.DeferredReason == DeferredEnum.UntilFSyncThenSync)).OrderBy (x => x.Id).ToList ();
        }

        public static List<McPending> QueryDeferredSync (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            rec.DeferredReason == DeferredEnum.UntilSync).OrderBy (x => x.Id).ToList ();
        }

        public static List<McPending> QueryDeferredUntilNow (int accountId)
        {
            return BackEnd.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Deferred &&
            rec.DeferredReason == DeferredEnum.UntilTime &&
            rec.DeferredUntilTime < DateTime.UtcNow
            ).OrderBy (x => x.Id).ToList ();
        }

        public static McPending QueryByToken (int accountId, string token)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .SingleOrDefault (x => 
                        x.AccountId == accountId &&
            x.Token == token);
        }

        public static List<McPending> QueryByOperation (int accountId, McPending.Operations operation)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .Where (rec =>
                        rec.AccountId == accountId &&
            rec.Operation == operation).ToList ();
        }

        public static McPending QueryFirstEligibleByOperation (int accountId, McPending.Operations operation)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .FirstOrDefault (rec =>
                        rec.AccountId == accountId &&
            rec.Operation == operation &&
            rec.State == StateEnum.Eligible);
        }

        public static McPending QueryByClientId (int accountId, string clientId)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .FirstOrDefault (rec =>
                        rec.AccountId == accountId &&
            rec.ClientId == clientId);
        }

        public static List<McPending> QueryEligibleByFolderServerId (int accountId, string folderServerId)
        {
            return BackEnd.Instance.Db.Table<McPending> ()
                    .Where (rec =>
                        rec.AccountId == accountId &&
            rec.ParentId == folderServerId &&
            rec.State == StateEnum.Eligible).ToList ();
        }

        public static McPending QueryByServerId (int accountId, string serverId)
        {
            // FIXME - is FirstOrDefault correct here?
            return BackEnd.Instance.Db.Table<McPending> ()
                    .FirstOrDefault (rec =>
                        rec.AccountId == accountId &&
            rec.ServerId == serverId);
        }
        // For re-write McPending objects on sync conflict resolution.
        public class ReWrite
        {
            public delegate bool IsMatchDelegate (McPending McPending);

            public IsMatchDelegate IsMatch;

            public delegate DbActionEnum PerformReWriteDelegate (McPending pending);

            public PerformReWriteDelegate PerformReWrite;

            public enum LocalActionEnum
            {
                ReplaceField,
                MoveToLostAndFound,
                Delete,
            };

            public enum FieldEnum
            {
                ServerId,
                ParentId,
            };

            public LocalActionEnum Action { get; set; }

            public FieldEnum Field { get; set; }

            public string Match { get; set; }

            public string ReplaceWith { get; set; }
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
                switch (reWrite.Field) {
                case ReWrite.FieldEnum.ServerId:
                    if (null != ServerId && ServerId == reWrite.Match) {
                        ServerId = reWrite.ReplaceWith;
                        updateNeeded = true;
                    }
                    break;
                }
            }
            return (updateNeeded) ? DbActionEnum.Update : DbActionEnum.DoNothing;
        }
       
        public bool FolderCompletelyDominates (string serverId)
        {
            // FIXME - build and check path.
            return false;
        }
    }


    public class McPendingPath : McObject
    {
        // Foreign key.
        [Indexed]
        public int PendingId { set; get; }
        // The path component.
        [Indexed]
        public string ServerId { set; get; }
        // The location within the path, where 0 is the top (child of root).
        public uint Order { set; get; }
    }
}

