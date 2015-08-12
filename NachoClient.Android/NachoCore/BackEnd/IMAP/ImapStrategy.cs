﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
        const int KFolderExamineInterval = 60 * 10;

        /// <summary>
        /// The default interval in seconds for QuickSync after which we'll re-examine the folder.
        /// </summary>
        const int KFolderExamineQSInterval = 30;

        /// <summary>
        /// The short interval in seconds for ForeGround after which we'll re-examine the folder.
        /// </summary>
        const int KFolderExamineFGShortInterval = 5;

        /// <summary>
        /// The time in seconds after which we'll add the inbox to the top of the list in SyncFolderList.
        /// </summary>
        const int KInboxMinSyncTime = 15 * 60;

        /// <summary>
        /// The Inbox message count after which we'll transition out of Stage/Rung 0
        /// </summary>
        const int KImapSyncRung0InboxCount = 400;

        /// <summary>
        /// The size of the initial (rung 0) sync window size
        /// </summary>
        const uint KRung0SyncWindowSize = 5;

        private static uint[] KRungSyncWindowSize = new uint[] {KRung0SyncWindowSize, KBaseOverallWindowSize, KBaseOverallWindowSize};

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
            return GenSyncKit (ref protocolState, folder, pending, true);
        }

        private static uint SpanSizeWithCommStatus (McProtocolState protocolState)
        {
            uint overallWindowSize = KRungSyncWindowSize[protocolState.ImapSyncRung];
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

        private static HashSet<HeaderId> ImapSummaryHeaders ()
        {
            HashSet<HeaderId> headers = new HashSet<HeaderId> ();
            headers.Add (HeaderId.Importance);
            headers.Add (HeaderId.DkimSignature);
            headers.Add (HeaderId.ContentClass);
            return headers;
        }

        private static MessageSummaryItems ImapSummaryitems (McProtocolState protocolState)
        {
            MessageSummaryItems NewMessageFlags = MessageSummaryItems.BodyStructure
                                                  | MessageSummaryItems.Envelope
                                                  | MessageSummaryItems.Flags
                                                  | MessageSummaryItems.InternalDate
                                                  | MessageSummaryItems.MessageSize
                                                  | MessageSummaryItems.UniqueId;
            ;

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
            return ((folder.ImapUidNext > 1) && (folder.ImapUidHighestUidSynced < folder.ImapUidNext - 1));
        }

        /// <summary>
        /// Generate the set of UIDs that we need to look at.
        /// </summary>
        /// <returns>A set of UniqueId's.</returns>
        /// <param name="protocolState">Protocol state.</param>
        /// <param name="folder">Folder.</param>
        public static IList<UniqueId> SyncSet (McFolder folder, ref McProtocolState protocolState)
        {
            bool needSync = needFullSync (folder);
            bool hasNewMail = HasNewMail (folder);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: NeedFullSync {1} HasNewMail {2}", folder.ImapFolderNameRedacted (), needSync, hasNewMail);
            if (needSync || hasNewMail) {
                resetLastSyncPoint (ref folder);
            }

            uint span = SpanSizeWithCommStatus (protocolState);
            IList<UniqueId> syncSet;
            // first see if there's new stuff to fetch
            syncSet = QuickSyncSet (folder.ImapUidNext, folder, span);
            if (null != syncSet && syncSet.Any ()) {
                return syncSet;
            }

            // there's no new stuff to fetch. See about older stuff.
            int startingPoint = (int)(0 != folder.ImapLastUidSynced ? folder.ImapLastUidSynced : folder.ImapUidNext);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Last {1} UidNext {2} Syncing from {3} for {4} messages",
                folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, folder.ImapUidNext, startingPoint, span);

            UniqueIdSet currentMails = getCurrentEmailUids (folder, 0, (uint)startingPoint, span);
            UniqueIdSet currentUidSet = getCurrentUIDSet (folder, 0, (uint)startingPoint, span);
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: currentMails(<{1}) {{{2}}} currentUidSet(<{1}) {{{3}}}",
                folder.ImapFolderNameRedacted (),
                startingPoint,
                currentMails,
                currentUidSet);
            if (!currentMails.Any () && !currentUidSet.Any ()) {
                return new UniqueIdSet ();
            }

            // Take the union of the two sets, so that we get new (only in the currentUidSet)
            // as well as removed (only in currentMails) Uids to look at when we perform the sync.
            syncSet = SyncKit.MustUniqueIdSet (currentMails.Union (currentUidSet).OrderByDescending (x => x).Take ((int)span).ToList ());
            return syncSet;
        }

        public static UniqueIdRange QuickSyncSet (uint UidNext, McFolder folder, uint span)
        {
            Log.Info (Log.LOG_IMAP, "GenSyncKit/Quick {0}: Last {1} UidNext {2} Highest {3}",
                folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, UidNext, folder.ImapUidHighestUidSynced);
            uint highest = UidNext - 1;
            if (highest <= 0) {
                return null;
            }

            uint lowest;
            if (highest > folder.ImapUidHighestUidSynced) {
                // there's new mail
                lowest = (uint)Math.Max (highest - span + 1, folder.ImapUidHighestUidSynced + 1);
            } else {
                // there might still be new mail. See if there's a range we can sync that's between
                // ImapLastUidSynced and the highest email in the DB.
                highest = folder.ImapLastUidSynced - 1;
                lowest = highest <= span ? 1 : highest - span + 1;
                UniqueIdSet currentMails = getCurrentEmailUids (folder, lowest, highest + 1, span);
                if (currentMails.Any ()) {
                    lowest = currentMails.Max ().Id + 1;
                }
            }
            if (lowest < highest) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit/Quick {0}: Last {1} UidNext {2} Syncing {3}:{4}",
                    folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, UidNext, highest, lowest);
                return new UniqueIdRange (new UniqueId (highest), new UniqueId (lowest));
            } else {
                Log.Info (Log.LOG_IMAP, "GenSyncKit/Quick {0}: nothing to do.",
                    folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, UidNext, folder.ImapUidHighestUidSynced);
                return null;
            }
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
            return false;
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
                return null;
            }
            if (folder.ImapNoSelect) {
                return null;
            }
            bool havePending = null != pending;
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Checking folder (last examined: {1}, HighestSynced {2}, UidNext {3}, Pending {4}, quickSync {5})",
                folder.ImapFolderNameRedacted (), folder.ImapLastExamine.ToString ("MM/dd/yyyy hh:mm:ss.fff tt"),
                folder.ImapUidHighestUidSynced, folder.ImapUidNext,
                havePending, quickSync);
            
            SyncKit syncKit = null;
            if (HasNewMail (folder) || havePending || quickSync) {
                // Let's try to get a chunk of new messages quickly.
                uint span = SpanSizeWithCommStatus (protocolState);
                syncKit = new SyncKit (folder, span, pending, ImapSummaryitems (protocolState), ImapSummaryHeaders ());
            } else if (NeedFolderMetadata (folder)) {
                // We really need to do an Open/SELECT to get UidNext, etc before we can sync this folder.
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: ImapUidSet {1} ImapLastExamine {2}",
                    folder.ImapFolderNameRedacted (), folder.ImapUidSet, folder.ImapLastExamine);
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    target.ImapNeedFullSync = true;
                    return true;
                });
                if (null != pending) {
                    // dispatch it and mark it deferred for later.
                    pending = pending.MarkDispached ();
                    pending = pending.ResolveAsDeferred (BEContext.ProtoControl, McPending.DeferredEnum.UntilFMetaData,
                        NcResult.Error (NcResult.SubKindEnum.Error_SyncFailedToComplete, NcResult.WhyEnum.UnavoidableDelay), true);
                }
                syncKit = new SyncKit (folder);
            } else if (folder.ImapUidNext > 1) {
                var syncSet = SyncSet (folder, ref protocolState);
                if (syncSet.Any ()) {
                    syncKit = new SyncKit (folder, syncSet, ImapSummaryitems (protocolState), ImapSummaryHeaders ());
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
                        pending = pending.MarkDispached ();
                    }
                    ResolveOneSync (BEContext, ref protocolState, folder, pending);
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
                    SyncKit syncKit = GenSyncKit (ref protocolState, folder, null, true);
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
                    SyncKit syncKit = GenSyncKit (ref protocolState, folder, null, false);
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

        /// <summary>
        /// Resolves the one sync, i.e. One SyncKit.
        /// </summary>
        /// <param name="BEContext">BEContext.</param>
        /// <param name="synckit">Synckit.</param>
        public static void ResolveOneSync (IBEContext BEContext, SyncKit synckit)
        {
            var protocolState = BEContext.ProtocolState;
            ResolveOneSync (BEContext, ref protocolState, synckit.Folder, synckit.PendingSingle);
            MaybeAdvanceSyncStage (ref protocolState);
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
                var exeCtxt = NcApplication.Instance.ExecutionContext;
                if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                    // Need to tell the BE that we did what it asked us to, i.e. sync. Even though there's nothing to do.
                    BEContext.Owner.StatusInd (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
                }
            }

            // If there's a pending, resolving it will send the StatusInd, otherwise, we need to send it ourselves.
            if (null != pending) {
                pending.ResolveAsSuccess (BEContext.ProtoControl);
            } else {
                BEContext.Owner.StatusInd (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
            }
        }

        private static uint MaybeAdvanceSyncStage (ref McProtocolState protocolState)
        {
            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            uint rung = protocolState.ImapSyncRung;
            switch (protocolState.ImapSyncRung) {
            case 0:
                if (defInbox.CountOfAllItems (McAbstrFolderEntry.ClassCodeEnum.Email) > KImapSyncRung0InboxCount ||
                    !SyncSet (defInbox, ref protocolState).Any ()) {
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

        private List<McFolder> SyncFolderList (ref McProtocolState protocolState, NcApplication.ExecutionContextEnum exeCtxt)
        {
            var folderList = new List<McFolder> ();
            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            switch (exeCtxt) {
            case NcApplication.ExecutionContextEnum.QuickSync:
                // the prioFolder could be the inbox, so check first before adding
                maybeAddFolderToList (folderList, defInbox);
                break;

            default:
                switch (protocolState.ImapSyncRung) {
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

