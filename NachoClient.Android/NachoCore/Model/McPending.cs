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
        // TEST USE ONLY.
        static IBackEnd _backEnd;
        public static IBackEnd _BackEnd {
            get {
                return _backEnd ?? BackEnd.Instance;
            }
            set {
                _backEnd = value;
            }
        }
        // Incremented on every table write.
        static int _Version = 0;

        public static int Version { get { return _Version; } }

        // Parameterless constructor only here for use w/LINQ. Please only use w/accountId.
        public McPending ()
        {
            DefersRemaining = KMaxDeferCount;
            // TODO: Perhaps Id suffices?
            Token = Guid.NewGuid ().ToString ("N");
        }

        public McPending (int accountId, McAccount.AccountCapabilityEnum capability) : this ()
        {
            AccountId = accountId;
            Capability = capability;
        }

        public McPending (int accountId, McAccount.AccountCapabilityEnum capability, McAbstrItem item) : this (accountId, capability)
        {
            Item = item;
        }

        public override string ToString ()
        {
            return string.Format ("McPending({0}/{1}/{2})", Id, Token, Operation);
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
            Sync,
            CalForward,
            // These values are persisted in the DB, so only add at the end.
            EmailSearch,
            Last = EmailSearch,
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
            UntilFMetaData,
        };

        public enum XmlStatusKindEnum
        {
            TopLevel,
            Folder,
            Command,
        };

        public const double KDefaultDeferDelaySeconds = 60.0;
        public const uint KMaxDeferCount = 5;

        public const string MarkReadFlag = "Read";
        public const string MarkUnreadFlag = "Unread";

        // Always valid.
        [Indexed]
        // FIXME - rename this column - this is for sequencing, not priority.
        public float Priority { set; get; }
        // Always valid.
        [Indexed]
        public StateEnum State { set; get; }
        // Always valid.
        [Indexed]
        public string Token { set; get; }
        // Always valid.
        [Indexed]
        public DateTime PriorityStamp { set; get; }
        // Always valid.
        [Indexed]
        public Operations Operation { set; get; }

        [Indexed]
        public McAccount.AccountCapabilityEnum Capability { set; get; }
        // Valid when in Deferred state.
        [Indexed]
        public DeferredEnum DeferredReason { set; get; }
        // Valid when in Deferred state and DeferredReason is UntilTime.
        [Indexed]
        public DateTime DeferredUntilTime { set; get; }
        // Valid when in Deferred state.
        public uint DefersRemaining { set; get; }
        // Valid when in Deferred state.
        [Indexed]
        public bool DeferredSerialIssueOnly { set; get; }
        // Valid when Deferred, Blocked, or Failed.
        [Indexed]
        // Set if the McPending may not be delayed or deferred.
        // Has the side-effect that the McPending will be deleted on restart.
        // Always valid.
        public bool DelayNotAllowed { set; get; }

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

        public bool CalUpdate_SendBody { get; set; }

        [Indexed]
        public string DisplayName { set; get; }

        public Xml.MeetingResp.UserResponseCode CalResponse { set; get; }

        public DateTime CalResponseInstance { set; get; }

        [Indexed]
        public int AttachmentId { set; get; }

        public bool Smart_OriginalEmailIsEmbedded { set; get; }

        public Xml.FolderHierarchy.TypeCode Folder_Type { set; get; }

        public const string KSynchronouslyCompleted	= "synchronously completed";

        public static bool Cancel (int accountId, string token)
        {
            var retval = false;
            NcModel.Instance.RunInTransaction (() => {
                var pendings = McPending.QueryByToken (accountId, token);
                foreach (var iterPending in pendings) {
                    var pending = iterPending;
                    switch (pending.State) {
                    case McPending.StateEnum.Eligible:
                        pending.ResolveAsCancelled (false);
                        retval = true;
                        break;

                    case McPending.StateEnum.Deferred:
                    case McPending.StateEnum.Failed:
                    case McPending.StateEnum.PredBlocked:
                    case McPending.StateEnum.UserBlocked:
                        if (McPending.Operations.ContactSearch == pending.Operation ||
                            McPending.Operations.EmailSearch == pending.Operation) {
                            McPending.ResolvePendingSearchReqs (accountId, token, false);
                        } else {
                            pending.ResolveAsCancelled (false);
                        }
                        retval = true;
                        break;

                    case McPending.StateEnum.Dispatched:
                        // Prevent any more high-level attempts after Cancel().
                        // TODO - need method to find executing Op/Cmd so we can prevent HTTP retries.
                        pending.UpdateWithOCApply<McPending> ((record) => {
                            var target = (McPending)record;
                            target.DefersRemaining = 0;
                            return true;
                        });
                        retval = false;
                        break;

                    case McPending.StateEnum.Deleted:
                        // Nothing to do.
                        retval = true;
                        break;

                    default:
                        NcAssert.CaseError (string.Format ("Unknown State {0}", pending.State));
                        break;
                    }
                }
            });
            return retval;
        }

        public static McPending UnblockPending (int accountId, int pendingId)
        {
            McPending retval = null;
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (accountId == pending.AccountId);
                    NcAssert.True (McPending.StateEnum.UserBlocked == pending.State);
                    retval = pending.UpdateWithOCApply<McPending> ((record) => {
                        var target = (McPending)record;
                        target.BlockReason = McPending.BlockReasonEnum.NotBlocked;
                        target.State = McPending.StateEnum.Eligible;
                        return true;
                    });
                }
            });
            return retval;
        }

        public virtual McPending DeletePendingCmd (int accountId, int pendingId)
        {
            McPending retval = null;
            NcModel.Instance.RunInTransaction (() => {
                var pending = McAbstrObject.QueryById<McPending> (pendingId);
                if (null != pending) {
                    NcAssert.True (accountId == pending.AccountId);
                    retval = pending.ResolveAsCancelled (false);
                }
            });
            return retval;
        }

        public static void Prioritize (int accountId, string token)
        {
            NcModel.Instance.RunInTransaction (() => {
                var pendings = McPending.QueryByToken (accountId, token);
                foreach (var pending in pendings) {
                    pending.Prioritize ();
                }
            });
        }

        public void Prioritize ()
        {
            UpdateWithOCApply<McPending> ((record) => {
                var target = (McPending)record;
                target.PriorityStamp = DateTime.UtcNow;
                target.DelayNotAllowed = true;
                Log.Info (Log.LOG_BACKEND, "{0}: Prioritized", target);
                return true;
            });
        }

        // To be used by app/ui when dealing with McPending.
        // To be used by Commands when dealing with McPending.
        public McPending MarkDispatched ()
        {
            Log.Info (Log.LOG_SYNC, "{0}:MarkDispatched", this);
            return UpdateWithOCApply<McPending> ((record) => {
                var target = (McPending)record;
                target.State = StateEnum.Dispatched;
                return true;
            });
        }

        public McPending MarkPredBlocked (int predPendingId)
        {
            var retval = this;
            var dep = new McPendDep (AccountId, predPendingId, Id);
            dep.Insert ();
            if (0 == Id) {
                State = StateEnum.PredBlocked;
                Insert ();
            } else {
                retval = UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.State = StateEnum.PredBlocked;
                    return true;
                });
            }
            Log.Info (Log.LOG_SYNC, "{0}:MarkPredBlocked", this);
            return retval;
        }

        public bool IsDuplicate ()
        {
            McPending dummy;
            return IsDuplicate (out dummy);
        }

        public bool IsDuplicate (out McPending dupRef)
        {
            switch (Operation) {
            case Operations.EmailBodyDownload:
                // TODO: if we add more cases, have lambda-per-Operation.
                var sameServerId = McPending.QueryByServerId (AccountId, ServerId).Where (x => x.State != StateEnum.Failed);
                foreach (var pending in sameServerId) {
                    if (pending.Operation == Operation &&
                        pending.ParentId == ParentId) {
                        dupRef = pending;
                        return true;
                    }
                }
                dupRef = null;
                return false;

            case Operations.AttachmentDownload:
                // TODO: take Operation out of query API.
                var sameAttachmentId = McPending.QueryByOperationAndAttId (AccountId, McPending.Operations.AttachmentDownload, AttachmentId)
                    .Where (x => x.State != StateEnum.Failed);
                foreach (var pending in sameAttachmentId) {
                    if (pending.Operation == Operation &&
                        pending.ServerId == ServerId &&
                        pending.AttachmentId == AttachmentId) {
                        dupRef = pending;
                        return true;
                    }
                }
                dupRef = null;
                return false;

            case Operations.Sync:
                sameServerId = McPending.QueryByServerId (AccountId, ServerId).Where (x => x.State != StateEnum.Failed);
                foreach (var pending in sameServerId) {
                    if (pending.Operation == Operation) {
                        dupRef = pending;
                        return true;
                    }
                }
                dupRef = null;
                return false;

            default:
                // TODO: implement additional cases as we care about them.
                NcAssert.True (false);
                dupRef = null;
                return false;
            }
        }

        private bool CanDepend ()
        {
            switch (Operation) {
            case Operations.FolderCreate:
            case Operations.FolderDelete:
            case Operations.FolderUpdate:
            case Operations.EmailForward:
            case Operations.EmailReply:
            case Operations.EmailSend:
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
                // TODO - this could create too many McPendDeps. 
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

        public McPending ResolveAsSuccess (NcProtoControl control)
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

            case Operations.Sync:
                subKind = NcResult.SubKindEnum.Info_SyncSucceeded;
                break;

            default:
                throw new Exception (string.Format ("{0}: default subKind not specified for Operation {1}", this, Operation));
            }
            var result = NcResult.Info (subKind);
            return ResolveAsSuccess (control, result);
        }

        public McPending ResolveAsSuccess (NcProtoControl control, NcResult result)
        {
            // This is the designated ResolveAsSuccess.
            var retval = this;
            NcAssert.True (StateEnum.Dispatched == State);
            NcAssert.True (NcResult.KindEnum.Info == result.Kind);
            if (Operation == Operations.EmailSend ||
                Operation == Operations.EmailForward ||
                Operation == Operations.EmailReply) {
                control.Owner.SendEmailResp (control, ItemId, true);
            }
            if (null != result) {
                NcAssert.True (null != control);
                control.StatusInd (result, new [] { Token });
            }
            retval = UpdateWithOCApply<McPending> ((record) => {
                var target = (McPending)record;
                target.ResultKind = result.Kind;
                target.ResultSubKind = result.SubKind;
                target.ResultWhy = result.Why;
                target.State = StateEnum.Deleted;
                return true;
            });
            Log.Info (Log.LOG_SYNC, "{0}:ResolveAsSuccess", this);
            UnblockSuccessors (control, StateEnum.Eligible);
            // Why update and then delete? I think we may want to defer deletion at some point.
            // If we do, then these are a good "log" of what has been done. So keep the records 
            // accurate.
            Delete ();
            return retval;
        }

        public McPending ResolveAsCancelled (bool onlyDispatched)
        {
            var retval = this;
            NcAssert.True (StateEnum.Dispatched == State || !onlyDispatched);
            retval = UpdateWithOCApply<McPending> ((record) => {
                var target = (McPending)record;
                target.State = StateEnum.Deleted;
                return true;
            });
            UnblockSuccessors (null, StateEnum.Eligible);
            Log.Info (Log.LOG_SYNC, "{0}:ResolveAsCancelled", this);
            Delete ();
            return retval;
        }

        public McPending ResolveAsCancelled ()
        {
            return ResolveAsCancelled (true);
        }

        private void EmailBodyError (int accountId, string serverId)
        {
            var email = McEmailMessage.QueryByServerId<McEmailMessage> (accountId, serverId);
            if (null == email) {
                Log.Warn (Log.LOG_AS, "{0}: ResolveAsHardFail/EmailBodyError: can't find McEmailMessage with ServerId {1}", this, serverId);
                return;
            }
            McBody body = null;
            if (0 != email.BodyId) {
                body = McBody.QueryById<McBody> (email.BodyId);
                if (null == body) {
                    Log.Error (Log.LOG_AS, "{0}: ResolveAsHardFail/EmailBodyError: BodyId {1} has no body", this, email.BodyId);
                }
            }
            if (null == body) {
                body = McBody.InsertError (accountId);
                email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                    email.BodyId = body.Id;
                    return true;
                });
            } else {
                body.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Error);
                body.Update ();
            }
        }

        private void EmailBodyClear (int accountId, string serverId)
        {
            var email = McEmailMessage.QueryByServerId<McEmailMessage> (accountId, serverId);
            if (null == email) {
                Log.Warn (Log.LOG_AS, "{0}: ResolveAsHardFail/EmailBodyClear: can't find McEmailMessage with ServerId {1}", this, serverId);
                return;
            }
            if (0 == email.BodyId) {
                return;
            }
            McBody body = McBody.QueryById<McBody> (email.BodyId);
            if (null == body) {
                Log.Error (Log.LOG_AS, "{0}: ResolveAsHardFail/EailBodyClear: BodyId {1} has no body", this, email.BodyId);
                return;
            }
            body.DeleteFile (); // Sets FilePresence to None and Updates the item
        }

        private void AttachmentError (int attachmentId)
        {
            var attachment = McAttachment.QueryById<McAttachment> (attachmentId);
            if (null == attachment) {
                Log.Warn (Log.LOG_AS, "{0}: ResolveAsHardFail/AttachmentError: Attachment {1} does not exist", this, attachmentId);
                return;
            }
            attachment.SetFilePresence (McAbstrFileDesc.FilePresenceEnum.Error);
            attachment.Update ();
        }

        private void AttachmentClear (int attachmentId)
        {
            var attachment = McAttachment.QueryById<McAttachment> (attachmentId);
            if (null == attachment) {
                Log.Warn (Log.LOG_AS, "{0}: ResolveAsHardFail/AttachmentClear: Attachment {1} does not exist", this, attachmentId);
                return;
            }
            attachment.DeleteFile (); // Sets FilePresence to None and Updates the item
        }

        public McPending ResolveAsHardFail (NcProtoControl control, NcResult result)
        {
            // This is the designated ResolveAsHardFail.
            var retval = this;
            NcAssert.True (NcResult.KindEnum.Error == result.Kind);
            control.StatusInd (result, new [] { Token });
            if (Operation == Operations.EmailSend ||
                Operation == Operations.EmailForward ||
                Operation == Operations.EmailReply) {
                control.Owner.SendEmailResp (control, ItemId, false);
            }
            NcModel.Instance.RunInTransaction (() => {
                retval = UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.ResultKind = result.Kind;
                    target.ResultSubKind = result.SubKind;
                    target.ResultWhy = result.Why;
                    target.State = StateEnum.Failed;
                    return true;
                });
                if (McPending.Operations.EmailBodyDownload == Operation) {
                    if (NcResult.WhyEnum.InterruptedByAppExit == ResultWhy) {
                        EmailBodyClear (AccountId, ServerId);
                    } else {
                        EmailBodyError (AccountId, ServerId);
                    }
                } else if (McPending.Operations.AttachmentDownload == Operation) {
                    if (NcResult.WhyEnum.InterruptedByAppExit == ResultWhy) {
                        AttachmentClear (AttachmentId);
                    } else {
                        AttachmentError (AttachmentId);
                    }
                }
                UnblockSuccessors (control, DelayNotAllowed ? StateEnum.Eligible : StateEnum.Failed);
            });

            if (DelayNotAllowed) {
                Log.Info (Log.LOG_SYNC, "{0}:ResolveAsHardFail:Reason:{1}:{2}", this, ResultSubKind.ToString (), ResultWhy.ToString ());
            } else {
                Log.Warn (Log.LOG_SYNC, "{0}:ResolveAsHardFail:Reason:{1}:{2}", this, ResultSubKind.ToString (), ResultWhy.ToString ());
            }
            return retval;
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
            case Operations.CalForward:
                return NcResult.SubKindEnum.Error_CalendarForwardFailed;
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
            case Operations.EmailSearch:
                return NcResult.SubKindEnum.Error_EmailSearchCommandFailed;
            case Operations.ContactSearch:
                return NcResult.SubKindEnum.Error_ContactSearchCommandFailed;
            case Operations.AttachmentDownload:
                return NcResult.SubKindEnum.Error_AttDownloadFailed;
            case Operations.Sync:
                return NcResult.SubKindEnum.Error_SyncFailed;
            default:
                throw new Exception (string.Format ("{0}: default subKind not specified for Operation {1}", this, Operation));
            }
        }

        // PUBLIC FOR TEST USE ONLY. OTHERWISE CONSIDER IT PRIVATE.
        public bool UnblockSuccessors (NcProtoControl control, StateEnum toState)
        {
            var successors = QuerySuccessors (Id);
            McPendDep.DeleteAllSucc (Id);
            foreach (var iter in successors) {
                var succ = iter;
                var remaining = McPendDep.QueryBySuccId (succ.Id);
                Log.Info (Log.LOG_SYNC, "{0}:UnblockSuccessors: {1} now {2}", this, succ.Id, toState.ToString ());
                switch (toState) {
                case StateEnum.Eligible:
                    if (0 == remaining.Count ()) {
                        // Just enable execution.
                        succ = succ.UpdateWithOCApply<McPending> ((record) => {
                            var target = (McPending)record;
                            target.State = toState;
                            return true;
                        });
                    }
                    break;
                case StateEnum.Failed:
                    foreach (var dep in remaining) {
                        dep.Delete ();
                    }
                    if (succ.AccountId != AccountId) {
                        // This is tricky, because succ may not have the same AccountId as this.
                        // Scenario: an attachment download in account A unblocking an email send in account B.
                        // TODO have a general way to get from pending -> appropriate controller.
                        // right now, we know there is only one such case.
                        var otherControl = _BackEnd.GetService (succ.AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                        succ.ResolveAsHardFail (otherControl, NcResult.WhyEnum.PredecessorFailed);
                    } else {
                        succ.ResolveAsHardFail (control, NcResult.WhyEnum.PredecessorFailed);
                    }
                    break;
                default:
                    NcAssert.CaseError (string.Format ("{0}:UnblockSuccessors: {1}", this, toState));
                    break;
                }
            }
            return (0 != successors.Count);
        }

        public void DoNotDelay ()
        {
            DelayNotAllowed = true;
        }

        public static bool MakeEligibleCore (string methodName, List<McPending> makeEligible, Func<McPending, bool> proc)
        {
            var eligibleInds = new Dictionary<int,McAccount.AccountCapabilityEnum> ();
            foreach (var pending in makeEligible) {
                pending.UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    if (proc (target)) {
                        if (eligibleInds.ContainsKey (target.AccountId)) {
                            eligibleInds [target.AccountId] |= target.Capability;
                        } else {
                            eligibleInds [target.AccountId] = target.Capability;
                        }
                    }
                    return true;
                });
                Log.Info (Log.LOG_SYNC, "{0}:{1}", pending, methodName);
            }
            if (0 != makeEligible.Count) {
                foreach (var accountId in eligibleInds.Keys) {
                    _BackEnd.PendQHotInd (accountId, eligibleInds [accountId]);
                }
                return true;
            }
            return false;
        }

        public static bool MakeEligibleOnFSync (int accountId)
        {
            return MakeEligibleCore ("MakeEligibleOnFSync", QueryDeferredFSync (accountId),
                (pending) => {
                    if (DeferredEnum.UntilFSyncThenSync == pending.DeferredReason) {
                        pending.DeferredReason = DeferredEnum.UntilSync;
                        return false;
                    } 
                    pending.State = StateEnum.Eligible;
                    return true;
                });
        }

        public static bool MakeEligibleOnSync (int accountId)
        {
            return MakeEligibleCore ("MakeEligibleOnSync", QueryDeferredSync (accountId),
                (pending) => {
                    pending.State = StateEnum.Eligible;
                    return true;
                });
        }

        public static bool MakeEligibleOnFMetaData (McFolder folder)
        {
            return MakeEligibleCore ("MakeEligibleOnFMetaData", QueryDeferredFMetaData (folder),
                (pending) => {
                    pending.State = StateEnum.Eligible;
                    return true;
                });
        }

        public static bool MakeEligibleOnTime ()
        {
            return MakeEligibleCore ("MakeEligibleOnTime", QueryDeferredUntilNow (),
                (pending) => {
                    pending.State = StateEnum.Eligible;
                    return true;
                });
        }

        // register for status-ind, look for FSync and Sync success.
        public McPending ResolveAsHardFail (NcProtoControl control, NcResult.WhyEnum why)
        {
            var result = NcResult.Error (DefaultErrorSubKind (), why);
            return ResolveAsHardFail (control, result);
        }

        /// <summary>
        /// Resolve a McPending as deferred
        /// </summary>
        /// <returns>The as deferred.</returns>
        /// <param name="control">NcProtoControl.</param>
        /// <param name="reason">DeferredEnum.</param>
        /// <param name="onFail">NcResult, which gets used if we deferred too many times (or are not allowed).</param>
        /// <param name="force">If set, ignore Force the deferral, ignoring things like DelayNotAllowed, but NOT the DefersRemaining.</param>
        public McPending ResolveAsDeferred (NcProtoControl control, DeferredEnum reason, NcResult onFail, bool force = false)
        {
            NcAssert.True (StateEnum.Dispatched == State);
            // Added check in case of any bug causing underflow.
            if ((DelayNotAllowed && !force) || 0 >= DefersRemaining || KMaxDeferCount < DefersRemaining) {
                return ResolveAsHardFail (control, onFail);
            } else {
                Log.Info (Log.LOG_SYNC, "{0}:ResolveAsDeferred", this);
                return UpdateWithOCApply<McPending> ((record) => {
                    var target = (McPending)record;
                    target.DefersRemaining--;
                    target.DeferredReason = reason;
                    target.State = StateEnum.Deferred;
                    return true;
                });
            }
        }

        public void ResolveAsDeferred (NcProtoControl control, DateTime eligibleAfter, NcResult onFail)
        {
            DeferredReason = DeferredEnum.UntilTime;
            DeferredUntilTime = eligibleAfter;
            ResolveAsDeferred (control, DeferredEnum.UntilTime, onFail);
        }

        public void ResolveAsDeferred (NcProtoControl control, DateTime eligibleAfter, NcResult.WhyEnum why)
        {
            var result = NcResult.Error (DefaultErrorSubKind (), why);
            ResolveAsDeferred (control, eligibleAfter, result);
        }

        public void ResolveAsDeferredForce (NcProtoControl control)
        {
            Log.Info (Log.LOG_SYNC, "{0}:ResolveAsDeferredForce", this);
            ResolveAsDeferred (control, DateTime.UtcNow, NcResult.WhyEnum.NotSpecified);
        }

        public McPending ResolveAsUserBlocked (NcProtoControl control, BlockReasonEnum reason, NcResult result)
        {
            // This is the designated ResolveAsUserBlocked.
            NcAssert.True (StateEnum.Dispatched == State);
            NcAssert.True (NcResult.KindEnum.Error == result.Kind);

            control.StatusInd (result, new [] { Token });
            State = StateEnum.UserBlocked;
            Log.Info (Log.LOG_SYNC, "{0}:ResolveAsUserBlocked", this);
            return UpdateWithOCApply<McPending> ((record) => {
                var target = (McPending)record;
                target.ResultKind = result.Kind;
                target.ResultSubKind = result.SubKind;
                target.ResultWhy = result.Why;
                target.BlockReason = reason;
                return true;
            });
        }

        public void ResolveAsUserBlocked (NcProtoControl control, BlockReasonEnum reason, NcResult.WhyEnum why)
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
                NcAssert.True (Operations.ContactSearch == kill.Operation || Operations.EmailSearch == kill.Operation);
                kill.ResolveAsCancelled (false);
            }
        }

        public static void ResolveAllDelayNotAllowedAsFailed (NcProtoControl control, int accountId)
        {
            NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
                    (rec.Capability & control.Capabilities) == rec.Capability &&
                    rec.DelayNotAllowed &&
                    rec.State != StateEnum.Failed).All (y => {
                        y.ResolveAsHardFail (control, NcResult.WhyEnum.UnavoidableDelay);
                        return true;
                    });
        }

        public static void ResolveAllDispatchedAsDeferred (NcProtoControl control, int accountId)
        {
            NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
                    (rec.Capability & control.Capabilities) == rec.Capability &&
                    rec.State == StateEnum.Dispatched).All (y => {
                        y.ResolveAsDeferred (control, DateTime.UtcNow, NcResult.WhyEnum.InterruptedByAppExit);
                        return true;
                    });
        }

        public override int Insert ()
        {
            using (var capture = CaptureWithStart ("Insert")) {
                var predIds = new List<int> ();

                NcModel.Instance.RunInTransaction (() => {
                    if (CanDepend ()) {
                        // Email sends with attachments are a special case.
                        // We need to inject any missing download operations ahead of the send and make the send dependent.
                        if (Operations.EmailSend == Operation || 
                            Operations.EmailForward == Operation || 
                            Operations.EmailReply == Operation) {
                            var atts = McAttachment.QueryByItem (Item);
                            foreach (var att in atts) {
                                if (McAbstrFileDesc.FilePresenceEnum.None == att.FilePresence) {
                                    var protoControl = _BackEnd.GetService (att.AccountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                                    var result = protoControl.DnldAttCmd (att.Id);
                                    if (result.isError ()) {
                                        // strip attachment if we can't initate download.
                                        // TODO let recipient/user know.
                                        Log.Error (Log.LOG_SYNC, "{0}: Unable to initiate attachment.", this);
                                        att.Unlink (Item);
                                    } else {
                                        var pend = McPending.QueryByToken (att.AccountId, (string)result.Value).First ();
                                        predIds.Add (pend.Id);
                                    }
                                }
                            }
                        }
                        // Walk from the back toward the front of the Q looking for anything this pending might depend upon.
                        // If this gets to be expensive, we can implement a scoreboard (and possibly also RAM cache).
                        var pendq = QueryNonFailedNonDeleted (AccountId).OrderByDescending (x => x.Priority);
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
                        // TODO Implement OC for all McItem subclasses.
                        if (Item is McEmailMessage) {
                            Item = Item.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.PendingRefCount++;
                                return true;
                            });
                        } else {
                            Item.PendingRefCount++;
                            Item.Update ();
                        }
                    }
                    base.Insert ();
                    ++_Version;
                    // Note that because Insert & Update are in the same transaction, we don't really need UpdateWithOCApply here.
                    // But we must to avoid the assert in Update().
                    base.UpdateWithOCApply<McPending> ((record) => {
                        var target = (McPending)record;
                        target.Priority = target.Id;
                        return true;
                    });
                    foreach (var predId in predIds) {
                        var pendDep = new McPendDep (AccountId, predId, Id);
                        pendDep.Insert ();
                    }
                });

                if (null != Item) {
                    Log.Info (Log.LOG_SYNC, "{0}: Item {1}: PendingRefCount+: {2}", this, Item.Id, Item.PendingRefCount);
                }
                Log.Info (Log.LOG_SYNC, "{0}:Insert", this);
                return 1;
            }
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, out int count, int tries = 100)
        {
            T retval = null;
            int innerCount = 0;
            NcModel.Instance.RunInLock (() => {
                retval = base.UpdateWithOCApply<T> (mutator, out innerCount, tries);
                ++_Version;
            });
            count = innerCount;
            return retval;
        }

        public override T UpdateWithOCApply<T> (Mutator mutator, int tries = 100)
        {
            T retval = null;
            NcModel.Instance.RunInLock (() => {
                retval = base.UpdateWithOCApply<T> (mutator, tries);
                ++_Version;
            });
            return retval;
        }

        public override int Update ()
        {
            NcAssert.True (false, "Must use UpdateWithOCApply.");
            return 0;
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
            using (var capture = CaptureWithStart ("Delete")) {
                McAbstrItem item = null;

                NcModel.Instance.RunInTransaction (() => {
                    // Deal with referenced McItem ref count if needed.
                    if (0 != ItemId) {
                        switch (Operation) {
                        case Operations.EmailSend:
                        case Operations.EmailForward:
                        case Operations.EmailReply:
                        case Operations.CalForward: // An e-mail message is used when forwarding a calendar item.
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
                            Log.Error (Log.LOG_SYS, "{0}: ItemId set to {1} for {2}.", this, ItemId, Operation);
                            NcAssert.True (false);
                            break;
                        }
                        NcAssert.NotNull (item);
                        NcAssert.True (0 < item.PendingRefCount);
                        // TODO Implement OC for all McItem subclasses.
                        if (item is McEmailMessage) {
                            item = item.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.PendingRefCount --;
                                return true;
                            });
                        } else {
                            item.PendingRefCount--;
                            item.Update ();
                        }
                        Log.Info (Log.LOG_SYNC, "{0}: Item {1}: PendingRefCount-: {2}", this, item.Id, item.PendingRefCount);
                        if (0 == item.PendingRefCount && item.IsAwaitingDelete) {
                            item.Delete ();
                        }
                        // Deal with any dependent McPending (if there are any, it is an error).
                        var successors = QuerySuccessors (Id);
                        if (0 != successors.Count) {
                            Log.Error (Log.LOG_SYNC, "{0}: {1} successors found in McPending.Delete.", this, successors.Count);
                            foreach (var succ in successors) {
                                succ.Delete ();
                            }
                        }
                    }
                    base.Delete ();
                    ++_Version;
                });
            
                Log.Info (Log.LOG_SYNC, "{0}:Delete", this);
                return 1;
            }
        }

        // Query APIs for any & all to call.

        /// <summary>
        /// All McPendings for an account, unordered.
        /// </summary>
        public static List<McPending> Query (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                    .Where (x => x.AccountId == accountId)
                    .ToList ();
        }

        public static List<McPending> QueryNonFailedNonDeleted (int accountId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (x => x.AccountId == accountId &&
            StateEnum.Failed != x.State &&
            StateEnum.Deleted != x.State).ToList ();
        }

        public static IEnumerable<McPending> QueryEligible (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Eligible &&
            rec.Capability == (rec.Capability & capabilities)
            ).OrderBy (x => x.Priority);
        }

        public static IEnumerable<McPending> QueryAllNonDispatchedNonFailedDoNotDelay (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
                rec.State != StateEnum.Dispatched &&
                rec.State != StateEnum.Failed &&
                rec.State != StateEnum.Deleted &&
                rec.DelayNotAllowed == true &&
                rec.Capability == (rec.Capability & capabilities));
        }

        public static IEnumerable<McPending> QueryEligibleOrderByPriorityStamp (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == accountId &&
            rec.State == StateEnum.Eligible &&
            rec.Capability == (rec.Capability & capabilities)
            ).OrderByDescending (x => x.PriorityStamp);
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

        public static List<McPending> QuerySuccessors (int predId)
        {
            return NcModel.Instance.Db.Query<McPending> (
                "SELECT p.* FROM McPending AS p JOIN McPendDep AS m ON p.Id = m.SuccId WHERE " +
                "p.State = ? AND " +
                "m.PredId = ? " +
                "ORDER BY Priority ASC",
                (uint)StateEnum.PredBlocked, predId).ToList ();
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

        public static List<McPending> QueryDeferredFMetaData (McFolder folder)
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
                rec.AccountId == folder.AccountId &&
                rec.ServerId == folder.ServerId &&
                rec.State == StateEnum.Deferred &&
                rec.DeferredReason == DeferredEnum.UntilFMetaData).OrderBy (x => x.Priority).ToList ();
        }

        public static List<McPending> QueryDeferredUntilNow ()
        {
            return NcModel.Instance.Db.Table<McPending> ().Where (rec => 
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

        public static IEnumerable<McPending> QueryByOperationAndAttId (int accountId, McPending.Operations operation, int attId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.Operation == operation &&
            rec.AttachmentId == attId).OrderBy (x => x.Priority);
        }

        public static List<McPending> QueryByOperation (int accountId, McPending.Operations operation)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                    .Where (rec =>
                        rec.AccountId == accountId &&
            rec.Operation == operation).OrderBy (x => x.Priority).ToList ();
        }

        public static List<McPending> QueryFirstEligibleByOperation (int accountId, 
                                                                     Operations operation1, Operations operation2, Operations operation3, Operations operation4,
                                                                     int limit)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            (rec.Operation == operation1 || rec.Operation == operation2 || rec.Operation == operation3 || rec.Operation == operation4) &&
            rec.State == StateEnum.Eligible).OrderBy (x => x.Priority).Take (limit).ToList ();
        }

        public static McPending QueryFirstEligibleByOperation (int accountId, Operations operation)
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

        public static IEnumerable<McPending> QueryByServerId (int accountId, string serverId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                        rec.AccountId == accountId &&
            rec.ServerId == serverId).OrderBy (x => x.Priority);
        }

        public static McPending QueryByAttachmentId (int accountId, int AttachmentId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.AttachmentId == AttachmentId
            ).FirstOrDefault ();
        }

        public static McPending QueryByEmailMessageId (int accountId, int emailMessageId)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.ItemId == emailMessageId &&
            (rec.Operation == Operations.EmailSend ||
            rec.Operation == Operations.EmailForward ||
            rec.Operation == Operations.EmailReply)).FirstOrDefault ();
        }

        public static IEnumerable<McPending> QueryOlderThanByState (int accountId, DateTime olderThan, StateEnum state)
        {
            return NcModel.Instance.Db.Table<McPending> ()
                .Where (rec =>
                    rec.AccountId == accountId &&
            rec.State == state &&
            rec.LastModified < olderThan);
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
            Log.Error (Log.LOG_AS, "{0}: SmartForward/Reply not converted to SendMail. Command will likely fail.", this);
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
            case Operations.CalForward: // An e-mail message is used when forwarding a calendar item
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

    public class McPendingHelper
    {
        static McPendingHelper _Instance;
        static object LockObj = new object ();

        public static McPendingHelper Instance {
            get {
                if (_Instance == null) {
                    lock (LockObj) {
                        if (_Instance == null) {
                            _Instance = new McPendingHelper ();
                        }
                    }
                }
                return _Instance;
            }
        }

        public void Start ()
        {
            PendingOnTimeTimerStart ();
        }

        public void Stop ()
        {
            PendingOnTimeTimerStop ();
        }

        public static bool IsUnitTest { get; set; }

        NcTimer PendingOnTimeTimer;
        object PendingOnTimeTimerLockObj = new object ();

        void PendingOnTimeTimerStart ()
        {
            if (IsUnitTest) {
                return;
            }

            lock (PendingOnTimeTimerLockObj) {
                if (null == PendingOnTimeTimer) {
                    PendingOnTimeTimer = new NcTimer ("BackEnd:PendingOnTimeTimer", state => McPending.MakeEligibleOnTime (), null, 1000, 1000);
                    PendingOnTimeTimer.Stfu = true;
                }                        
            }
        }

        void PendingOnTimeTimerStop ()
        {
            lock (PendingOnTimeTimerLockObj) {
                if (null != PendingOnTimeTimer) {
                    PendingOnTimeTimer.Dispose ();
                    PendingOnTimeTimer = null;
                }
            }
        }
    }
}

