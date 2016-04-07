//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Generic;
using MailKit;
using MimeKit;
using System.Linq;

namespace NachoCore.IMAP
{
    public partial class ImapStrategy
    {
        #region Sync Parameters

        /// <summary>
        /// The base sync-window size
        /// </summary>
        const uint KBaseOverallWindowSize = 5;

        /// <summary>
        /// The Inbox message count after which we'll transition out of Stage/Rung 0
        /// </summary>
        const int KImapSyncRung0InboxCount = 100;

        /// <summary>
        /// The maximum number of emails we'll delete in one go.
        /// </summary>
        const int KImapMaxEmailDeleteCount = 200;

        /// <summary>
        /// Easy way to disable old-email-deleting
        /// </summary>
        const bool KImapAllowDeleteOldEmails = false;

        /// <summary>
        /// The size of the initial (rung 0) sync window size. It's also the base-number for other
        /// window size calculations, i.e. multiplied by a certain number for CellFast and another
        /// number for Wifi, etc.
        /// </summary>
        const uint KRung0SyncWindowSize = 3;

        private static uint[] KRungSyncWindowSize = { KRung0SyncWindowSize, KBaseOverallWindowSize, KBaseOverallWindowSize };

        /// <summary>
        /// The default interval in seconds after which we'll re-examine a folder (i.e. fetch its metadata)
        /// </summary>
        const int KFolderExamineInterval = 60 * 30;

        /// <summary>
        /// The default interval in seconds for QuickSync after which we'll re-examine the folder.
        /// </summary>
        const int KFolderExamineQSInterval = 30;

        /// <summary>
        /// The time in seconds after which we'll add the inbox to the top of the list in SyncFolderList.
        /// </summary>
        const int KInboxMinSyncTime = 5 * 60;

        /// <summary>
        /// The multiplier we apply to the span for messages we're just resyncing, i.e. checking for flag changes and deletion.
        /// Resyncing per message runs on the average 20 times faster than fetching a new message.
        /// </summary>
        public const int KResyncMultiplier = 200;

        /// <summary>
        /// The Window multiplier for inbox, i.e. we fetch this many times more messages for inbox than for any other folder. (except in Quicksync)
        /// </summary>
        public const int KInboxWindowMultiplier = 2;

        public const string KXNachoChat = "X-Nacho-Chat";

        private static uint SpanSizeWithCommStatus (McProtocolState protocolState)
        {
            uint overallWindowSize = KRungSyncWindowSize [protocolState.ImapSyncRung];
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast_1:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi_0:
                overallWindowSize *= 3;
                break;
            }
            return overallWindowSize;
        }

        private int FolderExamineInterval { 
            get {
                return NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.QuickSync ? KFolderExamineQSInterval : KFolderExamineInterval;
            }
        }

        #endregion

        private static MessageSummaryItems FlagResyncFlags = MessageSummaryItems.Flags | MessageSummaryItems.UniqueId;

        private static HashSet<string> ImapSummaryHeaders ()
        {
            var headers = new HashSet<string> ();
            headers.Add (HeaderId.Importance.ToString ());
            headers.Add (HeaderId.DkimSignature.ToString ());
            headers.Add (HeaderId.ContentClass.ToString ());
            headers.Add (HeaderId.XPriority.ToString ());
            headers.Add (HeaderId.Priority.ToString ());
            headers.Add (HeaderId.XMSMailPriority.ToString ());
            headers.Add (KXNachoChat);
            return headers;
        }

        /// <summary>
        /// FIXME: Should calculate this in the ctor, ot every time. It's not like it's going to change.
        /// </summary>
        /// <returns>The summaryitems.</returns>
        /// <param name="protocolState">Protocol state.</param>
        private static MessageSummaryItems ImapSummaryitems (McProtocolState protocolState)
        {
            MessageSummaryItems NewMessageFlags = MessageSummaryItems.BodyStructure
                                                  | MessageSummaryItems.Envelope
                                                  | MessageSummaryItems.Flags
                                                  | MessageSummaryItems.InternalDate
                                                  | MessageSummaryItems.MessageSize
                                                  | MessageSummaryItems.UniqueId
                                                  | MessageSummaryItems.References;

            if (protocolState.ImapServerCapabilities.HasFlag (McProtocolState.NcImapCapabilities.GMailExt1)) {
                NewMessageFlags |= MessageSummaryItems.GMailMessageId;
                NewMessageFlags |= MessageSummaryItems.GMailThreadId;
                // TODO Perhaps we can use the gmail labels to give more hints to Brain, i.e. 'Important' or somesuch.
                //flags |= MessageSummaryItems.GMailLabels;
            }
            return NewMessageFlags;
        }

        #region GenSyncKit

        public SyncKit GenSyncKit (ref McProtocolState protocolState, NcApplication.ExecutionContextEnum exeCtxt, McPending pending)
        {
            foreach (var folder in SyncFolderList (protocolState.ImapSyncRung, exeCtxt)) {
                SyncKit syncKit = GenSyncKit (ref protocolState, folder, pending,
                                      exeCtxt == NcApplication.ExecutionContextEnum.QuickSync);
                if (null != syncKit) {
                    return syncKit;
                }
            }
            return null;
        }

        public SyncKit GenSyncKit (ref McProtocolState protocolState, McPending pending)
        {
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (exeCtxt != NcApplication.ExecutionContextEnum.Foreground) {
                Log.Warn (Log.LOG_IMAP, "GenSyncKit with Pending (i.e. user-request) but ExecutionContext is {0}", exeCtxt);
            }
            NcAssert.True (McPending.Operations.Sync == pending.Operation);
            var folder = McFolder.QueryByServerId<McFolder> (protocolState.AccountId, pending.ServerId);
            return GenSyncKit (ref protocolState, folder, pending, true);
        }

        /// <summary>
        /// GenSyncKit generates a data structure (SyncKit) that contains parameters and values
        /// needed for the BE to do a sync with the server.
        /// </summary>
        /// <param name="protocolState">The protocol state.</param>
        /// <param name="folder">The folder to sync.</param>
        /// <param name="pending">A pending (optional).</param>
        /// <param name="quickSync">Perform a quick sync, not a full sync</param>
        /// <remarks>
        /// This function reads folder.ImapUidHighestUidSynced and folder.ImapUidLowestUidSynced
        /// (and other values), but does NOT SET THEM. When the sync is executed (via ImapSymcCommand),
        /// it will set folder.ImapUidHighestUidSynced and folder.ImapUidLowestUidSynced. Next time
        /// GenSyncKit is called, these values are used to create the next SyncKit for ImapSyncCommand
        /// to consume.
        /// </remarks>
        public SyncKit GenSyncKit (ref McProtocolState protocolState, McFolder folder, McPending pending, bool quickSync)
        {
            if (null == folder) {
                Log.Error (Log.LOG_IMAP, "GenSyncKit({0}): no folder given", AccountId);
                if (null != pending) {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing, NcResult.WhyEnum.NotSpecified));
                }
                return null;
            }
            if (folder.ImapNoSelect) {
                Log.Error (Log.LOG_IMAP, "GenSyncKit({0}): folder is ImapNoSelect ({1})", AccountId, folder.ImapFolderNameRedacted ());
                if (null != pending) {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.Error (NcResult.SubKindEnum.Error_FolderMissing, NcResult.WhyEnum.AccessDeniedOrBlocked));
                }
                return null;
            }
            bool havePending = null != pending;
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Checking folder (UidNext {1}, LastExamined {2}, LastSynced {3}, HighestSynced {4}, LowestSynced {5}, Pending {6}, QuickSync {7}, ImapNeedFullSync {8})",
                folder.ImapFolderNameRedacted (), 
                folder.ImapUidNext,
                folder.ImapLastExamine.ToString ("MM/dd/yyyy hh:mm:ss.fff tt"),
                folder.ImapLastUidSynced,
                folder.ImapUidHighestUidSynced, 
                folder.ImapUidLowestUidSynced, 
                havePending,
                quickSync,
                folder.ImapNeedFullSync);

            SyncKit syncKit = null;
            if (HasNewMail (folder) || havePending || quickSync || NeedFolderMetadata (folder)) {
                // Let's try to get a chunk of new messages quickly.
                syncKit = new SyncKit (folder, pending);
            } else {
                uint span = SpanSizeWithCommStatus (protocolState);
                var outMessages = McEmailMessage.QueryImapMessagesToSend (protocolState.AccountId, folder.Id, span);
                List<SyncInstruction> instructions = (outMessages.Count < span) ? SyncInstructions (folder, ref protocolState, (uint)(span - outMessages.Count), pending != null) : null;
                if (null != instructions || outMessages.Any ()) {
                    syncKit = new SyncKit (folder, instructions);
                    syncKit.UploadMessages = outMessages;
                    if (null != syncKit && null != pending) {
                        syncKit.PendingSingle = pending;
                    }
                } else {
                    // Nothing to sync.
                    if (null != pending && pending.State == McPending.StateEnum.Eligible) {
                        // Mark the pending as dispatched, so we can resolve it right after.
                        // This can happen if we JUST refreshed the folder metadata within the 
                        // time-window (see NeedFolderMetadata()), and skipped the OpenOnly step.
                        // We need to dispatch the pending before ResolveOneSync() so we don't
                        // try to ResolveAsSuccess an eligible pending (which leads to a crash).
                        pending = pending.MarkDispatched ();
                    }
                    ResolveOneSync (BEContext, ref protocolState, folder, pending);
                }
                if (null == syncKit) {
                    // see if we can/should delete some older emails
                    var emailList = GetEmailsToDelete (folder);
                    if (null != emailList && emailList.Count > 0) {
                        syncKit = new SyncKit (folder, emailList);
                    }
                }
            }
            if (null == syncKit) {
                // update the sync count, even though there was nothing to do.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.SyncAttemptCount += 1;
                    target.LastSyncAttempt = DateTime.UtcNow;
                    return true;
                });
            }
            if (null != syncKit) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: {1}", folder.ImapFolderNameRedacted (), syncKit);
            } else {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: nothing to do", folder.ImapFolderNameRedacted ());
            }
            return syncKit;
        }

        /// <summary>
        /// Gets the list NcEmailMessageIndex to delete. We select emails to delete simply by querying for all
        /// existing emails with an ImapUid lower than the smallest one in the folder.ImapUidSet. We update
        /// the set (via GetFolderMetadata) by taking the DaysToSync into account, so the lowest number there 
        /// will match the user-set DaysToSync. McEmailMessage.ImapUid is indexed, so this should be a relatively
        /// quick query.
        /// </summary>
        /// <returns>The emails to delete.</returns>
        /// <param name="folder">Folder.</param>
        List<NcEmailMessageIndex> GetEmailsToDelete (McFolder folder)
        {
            if (!KImapAllowDeleteOldEmails ||
                folder.ImapNeedFullSync ||
                BEContext.Account.DaysToSyncEmail == NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0) {
                return null;
            }
            var uidSet = getCurrentUIDSet (folder, 0, 0, 0);
            if (uidSet == null || uidSet.Count == 0) {
                return null;
            }
            var lowestUid = uidSet.Min ().Id;
            // get list of email Ids less than lowestUid, ordered lowest to highest, limited to KImapMaxEmailDeleteCount.
            // this gives us a bounded list of oldest-first email IDs
            return NcModel.Instance.Db.Query<NcEmailMessageIndex> (
                "SELECT Id FROM McEmailMessage WHERE AccountId = ? AND ImapUid < ? ORDER BY ImapUid ASC LIMIT ?",
                folder.AccountId,
                lowestUid,
                KImapMaxEmailDeleteCount);
        }

        #endregion

        #region SyncInstructions

        /// <summary>
        /// Generate the set of UIDs that we need to look at.
        /// </summary>
        /// <returns>A set of UniqueId's.</returns>
        /// <param name="folder">Folder.</param>
        /// <param name="protocolState">Protocol state.</param>
        /// <param name="span">Span</param>
        /// <param name = "hasPending">If the sync is a pull-to-refresh</param>
        public static List<SyncInstruction> SyncInstructions (McFolder folder, ref McProtocolState protocolState, uint span, bool hasPending)
        {
            bool needSync = needFullSync (folder);
            bool hasNewMail = HasNewMail (folder);
            uint startingPoint;
            bool startingPointMustBeInSet = false;
            if (needSync || hasNewMail) {
                resetLastSyncPoint (ref folder);
                startingPoint = folder.ImapUidNext;
                startingPointMustBeInSet = true;
            } else {
                if (0 != folder.ImapLastUidSynced) {
                    startingPoint = folder.ImapLastUidSynced;
                } else {
                    startingPoint = folder.ImapUidNext;
                }
            }

            NcAssert.True (startingPoint > 0, "Possibly trying to get syncinstructions before the folder has been opened!");

            var defInbox = McFolder.GetDefaultInboxFolder (folder.AccountId);
            NcAssert.NotNull (defInbox, "No default inbox found.");
            if (!hasPending && defInbox.Id == folder.Id) {
                span *= KInboxWindowMultiplier;
            }

            List<SyncInstruction> instructions = new List<SyncInstruction> ();

            // Get the list of emails we have locally in the range (0-startingPoint) over span.
            UniqueIdSet currentMails = getCurrentEmailUids (folder, 0, startingPoint, span * KResyncMultiplier);
            // Get the list of emails on the server in the range (0-startingPoint) over span.
            UniqueIdSet currentUidSet = getCurrentUIDSet (folder, 0, startingPoint, span * KResyncMultiplier);
            // if both are empty, we're done. Nothing to do.
            var startingUid = new UniqueId (startingPoint - 1);
            if (currentMails.Any () || currentUidSet.Any ()) {
                // resync all the existing mails.
                if (currentMails.Any ()) {
                    if (startingPointMustBeInSet && !currentMails.Contains (startingUid)) {
                        // it doesn't hurt to add the starting Uid to both sets, if that winds up happening.
                        currentMails.Add (startingUid);
                    }
                    var uidSet = OrderedSetWithSpan (currentMails, span * KResyncMultiplier);
                    instructions.Add (SyncInstructionForFlagSync (uidSet));
                    span -= (uint)(uidSet.Count / KResyncMultiplier);
                }

                if (span > 0) {
                    var newMail = currentUidSet.Except (currentMails).ToList ();
                    if (newMail.Any ()) {
                        // If we're at the top, make sure we have the highest possible UID in the set. Otherwise,
                        // we might constantly loop looking to sync up to UidNext, when there's possibly no messages
                        // to sync (they might have gotten deleted).
                        if (startingPointMustBeInSet && !newMail.Contains (startingUid)) {
                            newMail.Add (startingUid);
                        }
                        var uidSet = OrderedSetWithSpan (newMail, span);
                        span -= (uint)(uidSet.Count);
                        instructions.Add (SyncInstructionForNewMails (ref protocolState, uidSet));
                    }
                }
            }
            return instructions.Any () ? instructions : null;
        }

        /// <summary>
        /// Generate the set of Sync Instructions that we need to look at.
        /// </summary>
        /// <returns>A set of UniqueId's.</returns>
        /// <param name="folder">Folder.</param>
        /// <param name="protocolState">Protocol state.</param>
        /// <param name = "hasPending">If the sync is a pull-to-refresh</param>
        public static List<SyncInstruction> SyncInstructions (McFolder folder, ref McProtocolState protocolState, bool hasPending)
        {
            uint span = SpanSizeWithCommStatus (protocolState);
            return SyncInstructions (folder, ref protocolState, span, hasPending);
        }

        public static SyncInstruction SyncInstructionForNewMails (ref McProtocolState protocolState, UniqueIdSet uidSet)
        {
            return new SyncInstruction (uidSet, ImapSummaryitems (protocolState), ImapSummaryHeaders (), true, true);
        }

        public static SyncInstruction SyncInstructionForFlagSync (UniqueIdSet uidSet)
        {
            return new SyncInstruction (uidSet, FlagResyncFlags, new HashSet<string> (), false, false);
        }

        #endregion

        public static UniqueIdRange QuickSyncSet (uint UidNext, McFolder folder, uint span)
        {
            uint highest = UidNext > 1 ? UidNext - 1 : 0;
            if (highest <= 0) {
                return null;
            }

            uint lowest;
            if (highest > folder.ImapUidHighestUidSynced) {
                // there's new mail
                lowest = Math.Max (span > highest ? 1 : highest - span + 1, folder.ImapUidHighestUidSynced + 1);
                return new UniqueIdRange (new UniqueId (highest), new UniqueId (lowest));
            } else {
                return null;
            }
        }

        public static void resetLastSyncPoint (ref McFolder folder)
        {
            if (folder.ImapLastUidSynced != folder.ImapUidNext || folder.ImapNeedFullSync) {
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    target.ImapLastUidSynced = target.ImapUidNext; // reset to the top
                    target.ImapNeedFullSync = false;
                    return true;
                });
            }
        }

        private bool NeedFolderMetadata (McFolder folder)
        {
            if (0 == folder.ImapUidNext) {
                return false;  // there's nothing in this folder.
            }
            if (null == folder.ImapUidSet) {
                return true; // new folder with emails
            }
            if (folder.ImapLastExamine < DateTime.UtcNow.AddSeconds (-FolderExamineInterval)) {
                return true;  // folder metadata is stale. Get new data.
            }
            if (folder.ImapNeedFullSync) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine what this quicksync will do:
        /// - If there's messages to be sent, do that first.
        /// - If there's new messages to fetch, add a SyncInstruction to the list
        /// - If we have any slots (span) left, fetch some flag-changes and look for deleted messages. For this, 
        ///    ignore the usual multiplier we apply to resync, since this is a *quick*sync.
        /// </summary>
        /// <returns><c>true</c>, if in quick sync kit was filled, <c>false</c> otherwise.</returns>
        /// <param name="protocolState">Protocol state.</param>
        /// <param name="Synckit">Synckit.</param>
        /// <param name="AccountId">Account identifier.</param>
        public static bool FillInQuickSyncKit (ref McProtocolState protocolState, ref SyncKit Synckit, int AccountId)
        {
            resetLastSyncPoint (ref Synckit.Folder);
            var startingPoint = Synckit.Folder.ImapUidNext;
            bool startingPointMustBeInSet = true;
            uint span = SpanSizeWithCommStatus (protocolState);
            Synckit.UploadMessages = McEmailMessage.QueryImapMessagesToSend (AccountId, Synckit.Folder.Id, span);
            span -= (uint)Synckit.UploadMessages.Count;
            if (span > 0) {
                var uidSet = SyncKit.MustUniqueIdSet (QuickSyncSet (startingPoint, Synckit.Folder, span));
                if (uidSet.Any ()) {
                    var startingUid = new UniqueId (startingPoint - 1);
                    if (startingPointMustBeInSet && !uidSet.Contains (startingUid)) {
                        uidSet.Add (startingUid);
                    }
                    startingPointMustBeInSet = false;
                    var syncInst = SyncInstructionForNewMails (ref protocolState, OrderedSetWithSpan (uidSet, span));
                    Synckit.SyncInstructions.Add (syncInst);
                    span -= (uint)syncInst.UidSet.Count;
                    startingPoint = syncInst.UidSet.Min ().Id;
                }
            }
            if (span > 0) {
                // don't use the multiplier here, since it's a quicksync.
                var emails = getCurrentEmailUids (Synckit.Folder, 0, startingPoint, span);
                if (emails.Any ()) {
                    var startingUid = new UniqueId (startingPoint - 1);
                    if (startingPointMustBeInSet && !emails.Contains (startingUid)) {
                        emails.Add (startingUid);
                        startingPointMustBeInSet = false;
                    }
                    var syncInst = SyncInstructionForFlagSync (OrderedSetWithSpan (emails, span));
                    Synckit.SyncInstructions.Add (syncInst);
                    span -= (uint)syncInst.UidSet.Count;
                }
            }
            return Synckit.SyncInstructions.Any () || Synckit.UploadMessages.Any ();
        }

        protected static UniqueIdSet OrderedSetWithSpan (IList<UniqueId> uids, uint span)
        {
            return SyncKit.MustUniqueIdSet (uids.OrderByDescending (x => x).Take ((int)span).ToList ());
        }

        private static UniqueIdSet getCurrentEmailUids (McFolder folder, uint min, uint max, uint span)
        {
            // Turn the result into a UniqueIdSet
            UniqueIdSet currentMails = new UniqueIdSet ();
            foreach (var uid in McEmailMessage.QueryByImapUidRange(folder.AccountId, folder.Id, min, max, span)) {
                currentMails.Add (new UniqueId ((uint)uid.Id));
            }
            return currentMails;
        }

        private static UniqueIdSet getCurrentUIDSet (McFolder folder, uint min, uint max, uint span)
        {
            UniqueIdSet uids;
            if (!string.IsNullOrEmpty (folder.ImapUidSet)) {
                if (!UniqueIdSet.TryParse (folder.ImapUidSet, folder.ImapUidValidity, out uids)) {
                    throw new ArgumentException (string.Format ("Could not parse folder.ImapUidSet {0}", folder.ImapUidSet));
                }
                NcAssert.NotNull (uids, "Parsed uidset is null");
            } else {
                return new UniqueIdSet ();
            }

            if (0 == max && 0 == min && 0 == span) {
                return uids;
            }

            var retUids = new UniqueIdSet ();
            foreach (UniqueId uid in uids.OrderByDescending (x => x)) {
                if ((0 == min || uid.Id >= min) &&
                    (0 == max || uid.Id < max)) {
                    retUids.Add (uid);
                }
                if (0 != span && retUids.Count >= span) {
                    break;
                }
            }
            return retUids;
        }

        private static bool needFullSync (McFolder folder)
        {
            bool needSync = false;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            switch (exeCtxt) {
            case NcApplication.ExecutionContextEnum.Foreground:
                needSync = folder.ImapNeedFullSync;
                break;
            }
            return needSync;
        }

        private static bool HasNewMail (McFolder folder)
        {
            return ((folder.ImapUidNext > 1) && (folder.ImapUidHighestUidSynced < folder.ImapUidNext - 1));
        }

        /// <summary>
        /// Resolves the one sync, i.e. One SyncKit.
        /// </summary>
        /// <param name="BEContext">BEContext.</param>
        /// <param name = "pending">A McPending</param>
        /// <param name = "folder">A McFolder</param>
        public static void ResolveOneSync (IBEContext BEContext, McPending pending, McFolder folder)
        {
            var protocolState = BEContext.ProtocolState;
            ResolveOneSync (BEContext, ref protocolState, folder, pending);
            MaybeAdvanceSyncStage (ref protocolState, pending != null);
        }

        /// <summary>
        /// Resolves the one sync.
        /// </summary>
        /// <param name="BEContext">BE context.</param>
        /// <param name="protocolState">Protocol state.</param>
        /// <param name="folder">The folder that was synced.</param>
        /// <param name="pending">The McPending, if any (can be null).</param>
        private static void ResolveOneSync (IBEContext BEContext, ref McProtocolState protocolState, McFolder folder, McPending pending)
        {
            // if this is the inbox and we have nothing to do, we need to still mark protocolState.HasSyncedInbox as True.
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type) {
                if (!protocolState.HasSyncedInbox) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.HasSyncedInbox = true;
                        return true;
                    });
                }
            }

            // If there's a pending, resolving it will send the StatusInd, otherwise, we need to send it ourselves.
            if (null != pending) {
                pending.ResolveAsSuccess (BEContext.ProtoControl);
            } else {
                BEContext.Owner.StatusInd (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
            }
        }

        private List<McFolder> SyncFolderList (uint ImapSyncRung, NcApplication.ExecutionContextEnum exeCtxt)
        {
            var folderList = new List<McFolder> ();
            McFolder defInbox = McFolder.GetDefaultInboxFolder (AccountId);
            switch (exeCtxt) {
            case NcApplication.ExecutionContextEnum.QuickSync:
                maybeAddFolderToList (folderList, defInbox);
                break;

            default:
                switch (ImapSyncRung) {
                case 0:
                    // the prioFolder could be the inbox, so check first before adding
                    maybeAddFolderToList (folderList, defInbox);
                    break;

                case 1:
                    Log.Warn (Log.LOG_IMAP, "SyncFolderList: Currently not implemented stage reached!");
                    NcAssert.True (false);
                    break;

                case 2:
                    // If inbox hasn't sync'd in kInboxMinSyncTime seconds, add it to the list at (or near) the top.
                    if (defInbox.LastSyncAttempt < DateTime.UtcNow.AddSeconds (-KInboxMinSyncTime)) {
                        maybeAddFolderToList (folderList, defInbox);
                    }

                    foreach (var folder in McFolder.QueryByIsClientOwned (AccountId, false).OrderBy (x => x.LastSyncAttempt)) {
                        if (folder.ImapNoSelect || // not a folder that ever contains mail. Don't sync it.
                            folder.ImapUidNext <= 1) { // this means there are no messages in the folder. Don't bother syncing it.
                            continue;
                        }
                        maybeAddFolderToList (folderList, folder);
                    }
                    break;
                }
                break;
            }
            return folderList;
        }

        private void maybeAddFolderToList (List<McFolder> folderList, McFolder folder)
        {
            if (!folderList.Any (x => x.Id == folder.Id)) {
                folderList.Add (folder);
            }
        }

        private static uint MaybeAdvanceSyncStage (ref McProtocolState protocolState, bool hasPending)
        {
            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            uint rung = protocolState.ImapSyncRung;
            switch (protocolState.ImapSyncRung) {
            case 0:
                var uidSet = new UniqueIdSet ();
                var syncInstList = SyncInstructions (defInbox, ref protocolState, hasPending);
                if (null != syncInstList) {
                    foreach (var inst in syncInstList) {
                        uidSet.AddRange (inst.UidSet);
                    }
                }
                if (defInbox.CountOfAllItems (McAbstrFolderEntry.ClassCodeEnum.Email) > KImapSyncRung0InboxCount ||
                    !uidSet.Any ()) {
                    // TODO For now skip stage 1, since it's not implemented.
                    rung = 2;
                    // reset the foldersync so we re-do it. In rung 0, we only sync'd Inbox.
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.AsLastFolderSync = DateTime.MinValue;
                        return true;
                    });
                }
                break;

            case 1:
                // TODO Fill in stage 1 later. For now just fall through to stage 2
                rung = 2;
                break;

            case 2:
                // we never exit this stage
                rung = 2;
                break;
            }

            if (rung != protocolState.ImapSyncRung) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit: Strategy rung update {0} -> {1}", protocolState.ImapSyncRung, rung);
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.ImapSyncRung = rung;
                    return true;
                });
            }
            return rung;
        }
    }
}

