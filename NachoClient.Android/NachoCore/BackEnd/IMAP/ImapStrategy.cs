//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using MailKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Generic;
using MimeKit;
using MailKit.Net.Imap;

namespace NachoCore.IMAP
{
    public class ImapStrategy : NcStrategy
    {
        /// <summary>
        /// The base sync-window size
        /// </summary>
        const uint KBaseOverallWindowSize = 10;

        /// <summary>
        /// The default interval in seconds after which we'll re-examine a folder (i.e. fetch its metadata)
        /// </summary>
        const int KFolderExamineInterval = 60 * 5;

        /// <summary>
        /// The default interface in seconds for QuickSync after which we'll re-examine the folder.
        /// </summary>
        const int KFolderExamineQSInterval = 30;

        /// <summary>
        /// The time in seconds after which we'll add the inbox to the top of the list in SyncFolderList.
        /// </summary>
        const int KInboxMinSyncTime = 15*60;

        /// <summary>
        /// The Inbox message count after which we'll transition out of Stage/Rung 0
        /// </summary>
        const int KImapSyncRung0InboxCount = 400;

        McFolder PrioSyncFolder { get; set; }

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
        }

        #region GenSyncKit

        public SyncKit GenSyncKit (ref McProtocolState protocolState, McPending pending)
        {
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (exeCtxt != NcApplication.ExecutionContextEnum.Foreground) {
                Log.Warn (Log.LOG_IMAP, "GenSyncKit with Pending (i.e. user-request) but ExecutionContext is {0}", exeCtxt);
            }
            NcAssert.True (McPending.Operations.Sync == pending.Operation);
            var folder = McFolder.QueryByServerId<McFolder> (protocolState.AccountId, pending.ServerId);
            var syncKit = GenSyncKit (ref protocolState, folder, true);
            if (null != syncKit) {
                syncKit.PendingSingle = pending;
            }
            return syncKit;
        }

        private static uint SpanSizeWithCommStatus ()
        {
            uint overallWindowSize = KBaseOverallWindowSize;
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
                return NcApplication.Instance.ExecutionContext != NcApplication.ExecutionContextEnum.Foreground ? KFolderExamineQSInterval : KFolderExamineInterval;
            }
        }

        //MessageSummaryItems FlagResyncFlags = MessageSummaryItems.Flags | MessageSummaryItems.UniqueId;

        private static HashSet<HeaderId> ImapSummaryHeaders()
        {
            HashSet<HeaderId> headers = new HashSet<HeaderId> ();
            headers.Add (HeaderId.Importance);
            headers.Add (HeaderId.DkimSignature);
            headers.Add (HeaderId.ContentClass);
            return headers;
        }

        private static MessageSummaryItems ImapSummaryitems(McProtocolState protocolState)
        {
            MessageSummaryItems NewMessageFlags = MessageSummaryItems.BodyStructure
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.Flags
                | MessageSummaryItems.InternalDate
                | MessageSummaryItems.MessageSize
                | MessageSummaryItems.UniqueId;;

            if (protocolState.ImapServerCapabilities.HasFlag (McProtocolState.NcImapCapabilities.GMailExt1)) {
                NewMessageFlags |= MessageSummaryItems.GMailMessageId;
                NewMessageFlags |= MessageSummaryItems.GMailThreadId;
                // TODO Perhaps we can use the gmail labels to give more hints to Brain, i.e. 'Important' or somesuch.
                //flags |= MessageSummaryItems.GMailLabels;
            }
            return NewMessageFlags;
        }

        private static bool needFullSync (McFolder folder)
        {
            bool needSync = false;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            switch (exeCtxt) {
            case NcApplication.ExecutionContextEnum.Foreground:
                needSync = folder.ImapNeedFullSync;
                break;

            default:
                break;
            }
            return needSync;
        }

        private static bool HasNewMail (McFolder folder)
        {
            return ((0 != folder.ImapUidHighestUidSynced) && (folder.ImapUidHighestUidSynced < folder.ImapUidNext -1));
        }

        /// <summary>
        /// Generate the set of UIDs that we need to look at.
        /// </summary>
        /// <returns>A set of UniqueId's.</returns>
        /// <param name="folder">Folder.</param>
        public static UniqueIdSet SyncSet (McFolder folder)
        {
            bool needSync = needFullSync (folder);
            bool hasNewMail = HasNewMail (folder);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: NeedFullSync {1} HasNewMail {2}", folder.ImapFolderNameRedacted (), needSync, hasNewMail);
            if (needSync || hasNewMail) {
                resetLastSyncPoint (ref folder);
            }

            uint span = SpanSizeWithCommStatus ();
            int startingPoint = (int)(0 != folder.ImapLastUidSynced ? folder.ImapLastUidSynced : folder.ImapUidNext);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Last {1} UidNext {2} Syncing from {3} for {4} messages", folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, folder.ImapUidNext, startingPoint, span);

            UniqueIdSet currentMails = getCurrentEmailUids (folder, 0, (uint)startingPoint, span);
            UniqueIdSet currentUidSet = getCurrentUIDSet (folder, 0, (uint)startingPoint, span);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: currentMails(<{1}) {{{2}}} currentUidSet(<{1}) {{{3}}}",
                folder.ImapFolderNameRedacted (),
                startingPoint,
                currentMails,
                currentUidSet);
            UniqueIdSet syncSet;
            if (!currentMails.Any () && !currentUidSet.Any ()) {
                syncSet = new UniqueIdSet ();
            } else {
                // Take the union of the two sets, so that we get new (only in the currentUidSet)
                // as well as removed (only in currentMails) Uids to look at when we perform the sync.
                syncSet = SyncKit.MustUniqueIdSet (currentMails.Union (currentUidSet).OrderByDescending (x => x).Take ((int)span).ToList ());
                if (HasNewMail (folder)) {
                    var highestUid = new UniqueId (folder.ImapUidNext - 1);
                    if (syncSet.Any () && !syncSet.Contains (highestUid)) {
                        // need to artificially add this to the set, otherwise we'll loop forever if there's a hole at the top.
                        syncSet.Add (highestUid);
                        if (syncSet.Count > span) {
                            var lowest = syncSet.Min ();
                            syncSet.Remove (lowest);
                        }
                    }
                }
            }
            return syncSet;
        }

        private static void resetLastSyncPoint (ref McFolder folder)
        {
            if (folder.ImapLastUidSynced != folder.ImapUidNext) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Resetting sync pointer to highest point", folder.ImapFolderNameRedacted ());
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    target.ImapLastUidSynced = target.ImapUidNext; // reset to the top
                    target.ImapNeedFullSync = false;
                    return true;
                });
            }
        }

        /// <summary>
        /// GenSyncKit generates a data structure (SyncKit) that contains parameters and values
        /// needed for the BE to do a sync with the server.
        /// </summary>
        /// <param name="accountId">The account Id.</param>
        /// <param name="protocolState">The protocol state.</param>
        /// <param name="folder">The folder to sync.</param>
        /// <param name="UserRequested">Whether this is a user-requested action.</param>
        /// <remarks>
        /// This function reads folder.ImapUidHighestUidSynced and folder.ImapUidLowestUidSynced
        /// (and other values), but does NOT SET THEM. When the sync is executed (via ImapSymcCommand),
        /// it will set folder.ImapUidHighestUidSynced and folder.ImapUidLowestUidSynced. Next time
        /// GenSyncKit is called, these values are used to create the next SyncKit for ImapSyncCommand
        /// to consume.
        /// </remarks>
        public SyncKit GenSyncKit (ref McProtocolState protocolState, McFolder folder, bool UserRequested = false)
        {
            if (null == folder) {
                return null;
            }
            if (folder.ImapNoSelect) {
                return null;
            }
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Checking folder (last examined: {1}, HighestSynced {2}, UidNext {3}, UserRequested {4})",
                folder.ImapFolderNameRedacted (), folder.ImapLastExamine.ToString("MM/dd/yyyy hh:mm:ss.fff tt"),
                folder.ImapUidHighestUidSynced, folder.ImapUidNext,
                UserRequested);
            
            SyncKit syncKit = null;
            if (UserRequested ||
                0 == folder.ImapUidNext ||
                null == folder.ImapUidSet ||
                folder.ImapLastExamine < DateTime.UtcNow.AddSeconds (-FolderExamineInterval)) {
                // We really need to do an Open/SELECT to get UidNext, etc before we can sync this folder.
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: UserRequested {1} ImapUidSet {2} ImapLastExamine {3}", folder.ImapFolderNameRedacted (), UserRequested, folder.ImapUidSet, folder.ImapLastExamine);
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    target.ImapNeedFullSync = true;
                    return true;
                });
                syncKit = new SyncKit (folder);
                PrioSyncFolder = folder; // make sure when we get back to strategy after getting the requested info, we do this folder first.
            } else {
                List<McEmailMessage> outMessages;
                var syncSet = SyncSet (folder);
                uint span = SpanSizeWithCommStatus ();
                outMessages = McEmailMessage.QueryImapMessagesToSend (protocolState.AccountId, folder.Id, span);
                if (syncSet.Any () || outMessages.Any ()) {
                    syncKit = new SyncKit (folder, syncSet, ImapSummaryitems(protocolState), ImapSummaryHeaders());
                    syncKit.UploadMessages = outMessages;
                } else {
                    // Nothing to sync.

                    // if this is the inbox and we have nothing to do, we need to still mark protocolState.HasSyncedInbox as True.
                    if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == folder.Type) {
                        if (!protocolState.HasSyncedInbox) {
                            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                                var target = (McProtocolState)record;
                                target.HasSyncedInbox = true;
                                return true;
                            });
                        }
                        var exeCtxt = NcApplication.Instance.ExecutionContext;
                        if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                            // Need to tell the BE that we did what it asked us to, i.e. sync. Even though there's nothing to do.
                            BEContext.Owner.StatusInd (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
                        }
                    }
                }
            }
            if (null != syncKit) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: New SyncKit {1}", folder.ImapFolderNameRedacted (), syncKit);
            } else {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: No synckit for folder", folder.ImapFolderNameRedacted ());
                // update the sync count, even though there was nothing to do.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.SyncAttemptCount += 1;
                    target.LastSyncAttempt = DateTime.UtcNow;
                    return true;
                });
            }
            return syncKit;
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
                    return null;
                }
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

        #endregion

        #region Pick

        public Tuple<PickActionEnum, ImapCommand> PickUserDemand (NcImapClient Client)
        {
            var accountId = BEContext.Account.Id;
            var protocolState = BEContext.ProtocolState;

            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                // (FG) If the user has initiated a Search command, we do that.
                var search = McPending.QueryEligible (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailSearch == x.Operation).FirstOrDefault ();
                if (null != search) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailSearch");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp, 
                        new ImapSearchCommand (BEContext, Client, search));
                }
                // (FG) If the user has initiated a Sync, we do that.
                var sync = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.Sync == x.Operation).FirstOrDefault ();
                if (null != sync) {
                    SyncKit syncKit = GenSyncKit (ref protocolState, sync);
                    if (null != syncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG:Sync");
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                            new ImapSyncCommand (BEContext, Client, syncKit));
                    }
                }
                // (FG) If the user has initiated a body Fetch, we do that.
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailBodyDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailBodyDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchBodyCommand (BEContext, Client, fetch));
                }
                // (FG) If the user has initiated an attachment Fetch, we do that.
                fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.AttachmentDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:AttachmentDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchAttachmentCommand (BEContext, Client, fetch));
                }
            }
            return null;
        }

        public Tuple<PickActionEnum, ImapCommand> Pick (NcImapClient Client)
        {
            var accountId = BEContext.Account.Id;
            var protocolState = BEContext.ProtocolState;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Initializing == exeCtxt) {
                // ExecutionContext is not set until after BE is started.
                exeCtxt = NcApplication.Instance.PlatformIndication;
            }
            var userDemand = PickUserDemand (Client);
            if (null != userDemand) {
                return userDemand;
            }

            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                foreach (var folder in SyncFolderList (ref protocolState, exeCtxt)) {
                    SyncKit syncKit = GenSyncKit (ref protocolState, folder);
                    if (null != syncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:QS:Sync {0}", folder.ImapFolderNameRedacted ());
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, Client, syncKit));
                    }
                }
            }

            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                // (FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (accountId, McAccount.ImapCapabilities).FirstOrDefault ();
                if (null != next) {
                    NcAssert.True (McPending.Operations.Last == McPending.Operations.EmailSearch);
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:QOp:{0}", next.Operation.ToString ());
                    ImapCommand cmd = null;
                    var action = PickActionEnum.QOop;
                    switch (next.Operation) {
                    // It is likely that next is one of these at the top of the switch () ...
                    case McPending.Operations.FolderCreate:
                        cmd = new ImapFolderCreateCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.FolderUpdate:
                        cmd = new ImapFolderUpdateCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.FolderDelete:
                        cmd = new ImapFolderDeleteCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.EmailDelete:
                        cmd = new ImapEmailDeleteCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.EmailMove:
                        cmd = new ImapEmailMoveCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.EmailMarkRead:
                        cmd = new ImapEmailMarkReadCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.EmailSetFlag:
                        // FIXME - defer until we decide how to deal with deferred messages.
                        break;
                    case McPending.Operations.EmailClearFlag:
                        // FIXME - defer until we decide how to deal with deferred messages.
                        break;
                    case McPending.Operations.EmailMarkFlagDone:
                        // FIXME - defer until we decide how to deal with deferred messages.
                        break;
                    // ... however one of these below, which would have been handled above, could have been
                    // inserted into the Q while Pick() is in the middle of running.
                    case McPending.Operations.EmailSearch:
                        cmd = new ImapSearchCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.EmailBodyDownload:
                        cmd = new ImapFetchBodyCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.AttachmentDownload:
                        cmd = new ImapFetchAttachmentCommand (BEContext, Client, next);
                        break;
                    case McPending.Operations.Sync:
                        var uSyncKit = GenSyncKit (ref protocolState, next);
                        if (null != uSyncKit) {
                            cmd = new ImapSyncCommand (BEContext, Client, uSyncKit);
                            action = PickActionEnum.Sync;
                        } else {
                            // This should not happen, so just do a folder-sync because we always can.
                            Log.Error (Log.LOG_IMAP, "Strategy:FG/BG:QOp: null SyncKit");
                            cmd = new ImapFolderSyncCommand (BEContext, Client);
                            action = PickActionEnum.FSync;
                        }
                        break;

                    default:
                        NcAssert.CaseError (next.Operation.ToString ());
                        break;
                    }
                    return Tuple.Create<PickActionEnum, ImapCommand> (action, cmd);
                }
                // (FG, BG) If it has been more than 5 min since last FolderSync, do a FolderSync.
                // It seems we can't rely on the server to tell us to do one in all situations.
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddSeconds (-FolderExamineInterval)) {
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.FSync, new ImapFolderSyncCommand (BEContext, Client));
                }

                // (FG/BG) Sync
                foreach (var folder in SyncFolderList (ref protocolState, exeCtxt)) {
                    SyncKit syncKit = GenSyncKit (ref protocolState, folder);
                    if (null != syncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync {0}", folder.ImapFolderNameRedacted ());
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, Client, syncKit));
                    }
                }

                Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Ping");
                return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Ping,
                    new ImapIdleCommand (BEContext, Client));
            }
            // (QS) Wait.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                Log.Info (Log.LOG_IMAP, "Strategy:QS:Wait");
                return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Wait,
                    new ImapWaitCommand (BEContext, Client, 120, true));
            }
            NcAssert.True (false);
            return null;
        }

        #endregion

        public static uint MaybeAdvanceSyncStage (ref McProtocolState protocolState)
        {
            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            uint rung = protocolState.ImapSyncRung;
            switch (protocolState.ImapSyncRung) {
            case 0:
                var syncSet = SyncSet ( defInbox);
                if (defInbox.CountOfAllItems (McAbstrFolderEntry.ClassCodeEnum.Email) > KImapSyncRung0InboxCount ||
                    !syncSet.Any ()) {
                    // TODO For now skip stage 1, since it's not implemented.
                    rung = 2;
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
                Log.Info (Log.LOG_IMAP, "GenSyncKit: Strategy stage update {0} -> {1}", protocolState.ImapSyncRung, rung);
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.ImapSyncRung = rung;
                    return true;
                });
            }
            return rung;
        }

        private List<McFolder> SyncFolderList (ref McProtocolState protocolState, NcApplication.ExecutionContextEnum exeCtxt)
        {
            var folderList = new List<McFolder> ();
            if (null != PrioSyncFolder) {
                maybeAddFolderToList(folderList, PrioSyncFolder);
                // don't let the PrioSyncFolder exist past one round.
                PrioSyncFolder = null;
            }

            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            switch (exeCtxt) {
            case NcApplication.ExecutionContextEnum.QuickSync:
                // the prioFolder could be the inbox, so check first before adding
                maybeAddFolderToList(folderList, defInbox);
                break;

            default:
                switch (protocolState.ImapSyncRung) {
                case 0:
                    // the prioFolder could be the inbox, so check first before adding
                    maybeAddFolderToList(folderList, defInbox);
                    break;

                case 1:
                    Log.Warn (Log.LOG_IMAP, "SyncFolderList: Currently not implemented stage reached!");
                    NcAssert.True (false);
                    break;

                case 2:
                    // If inbox hasn't sync'd in kInboxMinSyncTime seconds, add it to the list at (or near) the top.
                    if (defInbox.LastSyncAttempt < DateTime.UtcNow.AddSeconds (-KInboxMinSyncTime)) {
                        maybeAddFolderToList(folderList, defInbox);
                    }

                    foreach (var folder in McFolder.QueryByIsClientOwned (protocolState.AccountId, false).OrderBy (x => x.LastSyncAttempt)) {
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
            foreach (var folder in folderList) {
                Log.Info (Log.LOG_IMAP, "SyncFolderList: {0} LastSyncAttempt {1}", folder.ImapFolderNameRedacted (), folder.LastSyncAttempt.ToString("MM/dd/yyyy hh:mm:ss.fff tt"));
            }
            return folderList;
        }

        private void maybeAddFolderToList (List<McFolder> folderList, McFolder folder)
        {
            if (!folderList.Any (x => x.Id == folder.Id)) {
                folderList.Add (folder);
            }
        }
    }
}

