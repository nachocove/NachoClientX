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

namespace NachoCore.IMAP
{
    public class ImapStrategy : NcStrategy
    {
        public const uint KBaseOverallWindowSize = 10;
        public const int KBaseNoIdlePollTime = 60;
        // seconds
        public const int kFolderExamineInterval = 60 * 5;
        public const int kFolderExamineQSInterval = 30;

        McFolder PrioSyncFolder { get; set; }

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending)
        {
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (exeCtxt != NcApplication.ExecutionContextEnum.Foreground) {
                Log.Warn (Log.LOG_IMAP, "GenSyncKit with Pending (i.e. user-request) but ExecutionContext is {0}", exeCtxt);
            }
            NcAssert.True (McPending.Operations.Sync == pending.Operation);
            var folder = McFolder.QueryByServerId<McFolder> (accountId, pending.ServerId);
            var syncKit = GenSyncKit (accountId, protocolState, folder, true);
            if (null != syncKit) {
                syncKit.PendingSingle = pending;
            }
            return syncKit;
        }

        private uint SpanSizeWithCommStatus ()
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
                return NcApplication.Instance.ExecutionContext != NcApplication.ExecutionContextEnum.Foreground ? kFolderExamineQSInterval : kFolderExamineInterval;
            }
        }

        private int NoIdlePollTime { get { return KBaseNoIdlePollTime; } }

        MessageSummaryItems NewMessageFlags = MessageSummaryItems.BodyStructure
                                              | MessageSummaryItems.Envelope
                                              | MessageSummaryItems.Flags
                                              | MessageSummaryItems.InternalDate
                                              | MessageSummaryItems.MessageSize
                                              | MessageSummaryItems.UniqueId
                                              | MessageSummaryItems.GMailMessageId
                                              | MessageSummaryItems.GMailThreadId;

        //MessageSummaryItems FlagResyncFlags = MessageSummaryItems.Flags | MessageSummaryItems.UniqueId;

        uint SyncSpan (McFolder folder)
        {
            return UInt32.MinValue == folder.ImapUidHighestUidSynced ? KBaseOverallWindowSize : SpanSizeWithCommStatus ();
        }

        private bool needFullSync (McFolder folder)
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
        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder, bool UserRequested = false)
        {
            if (null == folder) {
                return null;
            }
            if (folder.ImapNoSelect) {
                return null;
            }
            Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Checking folder (last checked: {1}, HighestSynced {2}, UidNext {3}, UserRequested {4})",
                folder.ImapFolderNameRedacted (), folder.ImapLastExamine,
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
                PrioSyncFolder = folder;
            } else {
                bool needSync = needFullSync (folder);
                bool hasNewMail = HasNewMail (folder);
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: NeedFullSync {1} HasNewMail {2}", folder.ImapFolderNameRedacted (), needSync, hasNewMail);
                if (needSync || hasNewMail) {
                    Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Resetting sync pointer to highest point", folder.ImapFolderNameRedacted ());
                    resetLastSyncPoint (ref folder);
                    folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                        var target = (McFolder)record;
                        target.ImapNeedFullSync = false;
                        return true;
                    });
                }

                uint span = SyncSpan (folder);
                int startingPoint = (int)(0 != folder.ImapLastUidSynced ? folder.ImapLastUidSynced : folder.ImapUidNext);
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Last {1} UidNext {2} Syncing from {3} for {4} messages", folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, folder.ImapUidNext, startingPoint, span);

                if (McProtocolState.ImapSyncTypeEnum.Initial == protocolState.ImapSyncType) {
                    // If we're still in the initial sync, stop after a certain cut-off,
                    // so that we can populate all folders at least a little bit at the beginning.
                    var numMessages = folder.ImapUidNext - startingPoint - 1;
                    // the cutoff-point depends on comm-status, but since the initial sync is always 10,
                    // this will have the effect that we (mostly) will sync 10 + 30 before stopping.
                    if (numMessages >= SpanSizeWithCommStatus()) {
                        Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: Cutting off sync (Sync State {1}) after {2} messages", folder.ImapFolderNameRedacted (), protocolState.ImapSyncType, numMessages);
                        return null;
                    }
                }

                UniqueIdSet currentMails = getCurrentEmailUids (folder, 0, (uint)startingPoint, span);
                UniqueIdSet currentUidSet = getCurrentUIDSet (folder, 0, (uint)startingPoint, span);
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: currentMails {{{1}}} currentUidSet {{{2}}}", folder.ImapFolderNameRedacted (), currentMails, currentUidSet);
                if (!currentMails.Any () && !currentUidSet.Any ()) {
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
                    return null;
                }
                UniqueIdSet syncSet = SyncKit.MustUniqueIdSet (currentMails.Union (currentUidSet).OrderByDescending (x => x).Take ((int)span).ToList ());

                MessageSummaryItems flags = NewMessageFlags;
                HashSet<HeaderId> headers = new HashSet<HeaderId> ();
                headers.Add (HeaderId.Importance);
                headers.Add (HeaderId.DkimSignature);
                headers.Add (HeaderId.ContentClass);

                if (HasNewMail (folder)) {
                    Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: HasNewMail", folder.ImapFolderNameRedacted ());
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

                if (syncSet.Any ()) {
                    syncKit = new SyncKit (folder, syncSet, flags, headers);
                }
            }
            if (null != syncKit) {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: New SyncKit {1}", folder.ImapFolderNameRedacted (), syncKit);
            } else {
                Log.Info (Log.LOG_IMAP, "GenSyncKit {0}: No synckit for folder", folder.ImapFolderNameRedacted ());
            }
            return syncKit;
        }

        private void resetLastSyncPoint (ref McFolder folder)
        {
            if (folder.ImapLastUidSynced != folder.ImapUidNext) {
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    target.ImapLastUidSynced = target.ImapUidNext; // reset to the top
                    return true;
                });
            }
        }

        private bool HasNewMail (McFolder folder)
        {
            return ((0 != folder.ImapUidHighestUidSynced) && (folder.ImapUidHighestUidSynced < folder.ImapUidNext -1));
        }

        private static bool HasDeletedMail (McFolder folder, UniqueIdSet currentMails, UniqueIdSet currentUidSet, out UniqueIdSet uids)
        {
            // Need to pass in the FULL currentUidSet, not one narrowed down by startingPoint and span!!
            uids = new UniqueIdSet (currentMails.Except (currentUidSet));
            return uids.Any ();
        }

        private bool HasChangedMails (McFolder folder, out UniqueIdSet uids)
        {
            uids = new UniqueIdSet ();
            // FIXME How to determine (ignoring ModSeq for now)?
            return false;
        }

        private UniqueIdSet NotOnDeviceUids (McFolder folder, UniqueIdSet currentMails, UniqueIdSet currentUidSet)
        {
            UniqueIdSet uids = new UniqueIdSet (currentUidSet.Except (currentMails).OrderByDescending (x => x).ToList ());
            Log.Info (Log.LOG_IMAP, "GenSyncKit/NotOnDeviceUids {0}: ImapLastUidSynced {1}, ImapUidNext {2}, current mails: {{{3}}}",
                folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, folder.ImapUidNext, uids.ToString ());
            return uids;
        }

        private UniqueIdSet ReSyncUids (McFolder folder, UniqueIdSet currentMails)
        {
            Log.Info (Log.LOG_IMAP, "GenSyncKit/ReSyncUids {0}: ImapLastUidSynced {1}, ImapUidNext {2}, current mails: {{{3}}}",
                folder.ImapFolderNameRedacted (), folder.ImapLastUidSynced, folder.ImapUidNext, currentMails.ToString ());
            return currentMails;
        }

        private UniqueIdSet getCurrentEmailUids (McFolder folder, uint min, uint max, uint span)
        {
            // FIXME Scalability
            // FIXME: Turn into query, not loop!
            UniqueIdSet currentMails = new UniqueIdSet ();
            foreach (McEmailMessage emailMessage in McEmailMessage.QueryByFolderId<McEmailMessage> (folder.AccountId, folder.Id)
                .OrderByDescending (x => ImapProtoControl.ImapMessageUid(x.ServerId))) {
                var uid = ImapProtoControl.ImapMessageUid (emailMessage.ServerId);
                if ((0 == min || uid.Id >= min) &&
                    (0 == max || uid.Id < max)) {
                    currentMails.Add (uid);
                }
                if (0 != span && currentMails.Count >= span) {
                    break;
                }
            }
            return currentMails;
        }

        private UniqueIdSet getCurrentUIDSet (McFolder folder, uint min, uint max, uint span)
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

        public Tuple<PickActionEnum, ImapCommand> PickUserDemand (NcImapClient Client)
        {
            var accountId = BEContext.Account.Id;
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
                    SyncKit syncKit = GenSyncKit (BEContext.Account.Id, BEContext.ProtocolState, sync);
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
                foreach (var folder in SyncFolderList (accountId, exeCtxt)) {
                    SyncKit syncKit = GenSyncKit (accountId, protocolState, folder);
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
                        var uSyncKit = GenSyncKit (accountId, protocolState, next);
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
                bool doAgain;
                do {
                    doAgain = false;
                    foreach (var folder in SyncFolderList (accountId, exeCtxt)) {
                        SyncKit syncKit = GenSyncKit (accountId, protocolState, folder);
                        if (null != syncKit) {
                            Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync {0}", folder.ImapFolderNameRedacted ());
                            return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                                new ImapSyncCommand (BEContext, Client, syncKit));
                        }
                    }
                    // if we got here, and we're still in Initial sync, then move up to the regular sync strategy.
                    if (McProtocolState.ImapSyncTypeEnum.Initial == protocolState.ImapSyncType) {
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.ImapSyncType = McProtocolState.ImapSyncTypeEnum.Regular;
                            return true;
                        });
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Switch to ImapSyncTypeEnum.Regular");
                        doAgain = true;
                    }
                } while (doAgain);

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

        private List<McFolder> SyncFolderList (int accountId, NcApplication.ExecutionContextEnum exeCtxt)
        {
            var list = new List<McFolder> ();
            if (null != PrioSyncFolder) {
                list.Add (PrioSyncFolder);
            }

            // Always make sure Inbox is checked first (but possibly after PrioFolder).
            McFolder defInbox = McFolder.GetDefaultInboxFolder (accountId);
            if (null == PrioSyncFolder || defInbox.Id != PrioSyncFolder.Id) {
                list.Add (defInbox);
            }

            // if in FG, add all other folders. Otherwise, only Inbox (and PrioFolder) gets syncd
            if (NcApplication.ExecutionContextEnum.QuickSync != exeCtxt) {
                foreach (var folder in McFolder.QueryByIsClientOwned (accountId, false).OrderBy (x => x.SyncAttemptCount)) {
                    if (folder.ImapNoSelect ||
                        defInbox.Id == folder.Id ||
                        (null != PrioSyncFolder && folder.Id == PrioSyncFolder.Id) ||
                        folder.ImapUidNext <= 1)
                    {
                        continue;
                    }
                    list.Add (folder);
                }
            }
            if (null != PrioSyncFolder) {
                // don't let the PrioSyncFolder exist past one round.
                PrioSyncFolder = null;
            }
            return list;
        }
    }
}

