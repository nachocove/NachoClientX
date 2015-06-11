//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoCore.IMAP
{
    public class ImapStrategy : NcStrategy
    {
        public const uint KBaseOverallWindowSize = 25;
        public const int KBaseNoIdlePollTime = 60; // seconds

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending)
        {
            NcAssert.True (McPending.Operations.Sync == pending.Operation);
            var folder = McFolder.QueryByServerId<McFolder> (accountId, pending.ServerId);
            var syncKit = GenSyncKit (accountId, protocolState, folder, true);
            if (null != syncKit) {
                syncKit.PendingSingle = pending;
            }
            return syncKit;
        }

        private uint SpanSizeWithCommStatus()
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

        private int NoIdlePollTime()
        {
            // customize the PollTime here, depending on circumstances.
            return KBaseNoIdlePollTime;
        }

        MessageSummaryItems SummaryFlags = MessageSummaryItems.BodyStructure
            | MessageSummaryItems.Envelope
            | MessageSummaryItems.Flags
            | MessageSummaryItems.InternalDate
            | MessageSummaryItems.MessageSize
            | MessageSummaryItems.UniqueId
            | MessageSummaryItems.GMailMessageId
            | MessageSummaryItems.GMailThreadId;

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
        /// 
        /// The current 'strategy' (I use the term lightly) is as follows:
        ///   - If the folder has never been sync'd (or hasn't been sync'd in a while), we tell
        ///     ImapSyncCommand to merely open the folder and record the current state (mainly UIDNEXT).
        ///     folder.ImapUidHighestUidSynced and folder.ImapUidLowestUidSynced will remain unchanged at
        ///     their default values.
        ///   - Once we know UIDNEXT, starting with the newest X messages, where X is expected to be small.
        ///     This is to get us going fast and have something to display in the UI. Currently we use 10.
        ///     ImapSyncCommand sets folder.ImapUidHighestUidSynced to UIDNEXT-1, since that's the highest
        ///     UID we've sync'd. ImapSyncCommand also sets folder.ImapUidLowestUidSynced to the lowest
        ///     we've sync'd to now (presumably UIDNEXT-11, but that depends on how many messages there are)
        ///   - After that, we start syncing from folder.ImapUidLowestUidSynced downwards, in chunks of 
        ///     'span' (which depends on NcCommStatus). Unless new mail comes in, ImapUidHighestUidSynced
        ///     is unlikely to change.
        ///   - When new mail comes in, UIDNEXT will be higher than ImapUidHighestUidSynced, so we again try
        ///     to sync the newest messages.
        ///   - After new mail has been processed, we can continue syncing 'downwards', knowing where we
        ///     left off via ImapUidLowestUidSynced.
        /// 
        /// There is a bug with this approach, however, in that the two variables are not enough to keep
        /// track of all possible cases. In the case where the number of new emails that have arrived is
        /// > than the span selected, we would sync the newest messages, but since we don't want to move 
        /// ImapUidLowestUidSynced (because then we'd lose track of where to continue syncing older messages),
        /// we have no way of keeping track of the downward progress of the 'new mail' sync. So instead,
        /// for now, we sync from ImapUidHighestUidSynced upwards, rather then from UIDNEXT downwards.
        /// This is not optimal and will be changed in the future.
        /// </remarks>
        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder, bool UserRequested = false)
        {
            if (folder.ImapNoSelect) {
                return null;
            }

            if (null == folder) {
                return null;
            }

            SyncKit syncKit = null;
            var currentHighestInFolder = folder.ImapUidNext - 1;
            var compareTime = DateTime.UtcNow.AddSeconds (-NoIdlePollTime ());
            if (UserRequested ||
                0 == folder.ImapUidNext ||
                folder.ImapLastExamine < compareTime) // perhaps this should be passed in by the caller?
            {
                Log.Info (Log.LOG_IMAP, "folder.ImapLastExamine {0} < {1}", folder.ImapLastExamine, compareTime);
                // We really need to do an Open/SELECT to get UidNext before we can sync this folder.
                syncKit = new SyncKit () {
                    Method = SyncKit.MethodEnum.OpenOnly,
                    Folder = folder,
                    Flags = SummaryFlags,
                    Span = 0,
                    Start = 0,
                };
            } else if (currentHighestInFolder > folder.ImapUidHighestUidSynced) {
                // This case covers the initial sync, and any syncs where new mail hs arrived.
                if (UInt32.MinValue == folder.ImapUidHighestUidSynced) {
                    // This is the first sync. Start at the top, and work your way down.
                    uint span = 10;  // use a very small window for this sync, so we quickly get stuff back to display
                    syncKit = new SyncKit () {
                        Method = SyncKit.MethodEnum.Range,
                        Folder = folder,
                        Flags = SummaryFlags,
                        Span = span,
                        Start = Math.Max (folder.ImapUidHighestUidSynced + 1, 
                            span + 1 > currentHighestInFolder ? 1 : currentHighestInFolder - span + 1),
                    };
                } else {
                    // this is the 'new mail has arrived' case. Start from ImapUidHighestUidSynced
                    // and work your way up.
                    //uint span = SpanSizeWithCommStatus ();
                    uint span = 10;
                    syncKit = new SyncKit () {
                        Method = SyncKit.MethodEnum.Range,
                        Folder = folder,
                        Flags = SummaryFlags,
                        Span = span,
                        Start = folder.ImapUidHighestUidSynced + 1,
                    };
                }
                // adjust span, to make sure it doesn't over- or under-run.
                syncKit.Span =
                    Math.Min (syncKit.Span, 
                        ((folder.ImapUidHighestUidSynced + 1) >= currentHighestInFolder) ? 1 :
                        currentHighestInFolder - folder.ImapUidHighestUidSynced);
                
            } else if (currentHighestInFolder > 0 && // are there any messages at all?
                       folder.ImapUidLowestUidSynced > 1)
            {
                // If there is nothing new to grab, then pull down older mail.
                uint span = SpanSizeWithCommStatus ();
                syncKit = new SyncKit () {
                    Method = SyncKit.MethodEnum.Range,
                    Folder = folder,
                    Flags = SummaryFlags,
                    Span = span,
                    Start = (span >= folder.ImapUidLowestUidSynced) ? 1 : 
                        folder.ImapUidLowestUidSynced - span,
                };
                // adjust span, to make sure it doesn't over- or under-run.
                syncKit.Span = 
                    (syncKit.Start >= folder.ImapUidLowestUidSynced) ? 1 : 
                    Math.Min (syncKit.Span, folder.ImapUidLowestUidSynced - syncKit.Start);
            }
            if (null != syncKit && SyncKit.MethodEnum.Range == syncKit.Method) {
                syncKit.UidList = new UniqueIdRange (
                    new UniqueId(folder.ImapUidValidity, syncKit.Start),
                    new UniqueId(folder.ImapUidValidity, syncKit.Start + syncKit.Span - 1));
            }
            return syncKit;
        }


        public Tuple<PickActionEnum, ImapCommand> PickUserDemand ()
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
                        new ImapSearchCommand (BEContext, search));
                }
                // (FG) If the user has initiated a body Fetch, we do that.
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailBodyDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailBodyDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchBodyCommand (BEContext, fetch));
                }
                // (FG) If the user has initiated an attachment Fetch, we do that.
                fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.AttachmentDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:AttachmentDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchAttachmentCommand (BEContext, fetch));
                }
            }
            return null;
        }

        public Tuple<PickActionEnum, ImapCommand> Pick ()
        {
            var accountId = BEContext.Account.Id;
            var protocolState = BEContext.ProtocolState;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Initializing == exeCtxt) {
                // ExecutionContext is not set until after BE is started.
                exeCtxt = NcApplication.Instance.PlatformIndication;
            }
            var userDemand = PickUserDemand ();
            if (null != userDemand) {
                return userDemand;
            }

            // TODO move user-directed Sync up to this priority level in FG.
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
                        cmd = new ImapFolderCreateCommand (BEContext, next);
                        break;
                    case McPending.Operations.FolderUpdate:
                        cmd = new ImapFolderUpdateCommand (BEContext, next);
                        break;
                    case McPending.Operations.FolderDelete:
                        cmd = new ImapFolderDeleteCommand (BEContext, next);
                        break;
                    case McPending.Operations.EmailDelete:
                        cmd = new ImapEmailDeleteCommand (BEContext, next);
                        break;
                    case McPending.Operations.EmailMove:
                        cmd = new ImapEmailMoveCommand (BEContext, next);
                        break;
                    case McPending.Operations.EmailMarkRead:
                        cmd = new ImapEmailMarkReadCommand (BEContext, next);
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
                        cmd = new ImapSearchCommand (BEContext, next);
                        break;
                    case McPending.Operations.EmailBodyDownload:
                        cmd = new ImapFetchBodyCommand (BEContext, next);
                        break;
                    case McPending.Operations.AttachmentDownload:
                        cmd = new ImapFetchAttachmentCommand (BEContext, next);
                        break;
                    case McPending.Operations.Sync:
                        var uSyncKit = GenSyncKit (accountId, protocolState, next);
                        if (null != uSyncKit) {
                            cmd = new ImapSyncCommand (BEContext, uSyncKit);
                            action = PickActionEnum.Sync;
                        } else {
                            // This should not happen, so just do a folder-sync because we always can.
                            Log.Error (Log.LOG_IMAP, "Strategy:FG/BG:QOp: null SyncKit");
                            cmd = new ImapFolderSyncCommand (BEContext);
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
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddMinutes (-5)) {
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.FSync, new ImapFolderSyncCommand (BEContext));
                }
                // (FG, BG) if the IMAP server doesn't support IDLE, we need to poll
                if (!BEContext.ProtocolState.ImapServerCapabilities.HasFlag (McProtocolState.NcImapCapabilities.Idle)) {
                    var defInbox = McFolder.GetDefaultInboxFolder (accountId);
                    if (defInbox.ImapLastExamine < DateTime.UtcNow.AddSeconds (-NoIdlePollTime())) {
                        SyncKit syncKit = GenSyncKit (accountId, protocolState, defInbox);
                        if (null != syncKit) {
                            Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:PollSync {0}", defInbox.ServerId);
                            return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                                new ImapSyncCommand (BEContext, syncKit));
                        }
                    }
                }
                // (FG, BG) Choose eligible option by priority, split tie randomly...
                if (PowerPermitsSpeculation () ||
                    NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                    // FIXME JAN once ImapXxxDownloadCommand can handle a FetchKit", lift logic from EAS 
                    // for speculatively pre-fetching bodies and attachments.
                    SyncKit syncKit;
                    // Always make sure Inbox is checked first.
                    McFolder defInbox = McFolder.GetDefaultInboxFolder (BEContext.Account.Id);
                    syncKit = GenSyncKit (accountId, protocolState, defInbox);
                    if (null != syncKit) {
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, syncKit));
                    }
                    foreach (var folder in McFolder.QueryByIsClientOwned (accountId, false)) {
                        if (defInbox.Id == folder.Id) {
                            continue;
                        }
                        syncKit = GenSyncKit (accountId, protocolState, folder);
                        if (null != syncKit) {
                            Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync {0}", folder.ServerId);
                            return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                                new ImapSyncCommand (BEContext, syncKit));
                        }
                    }
                }
                if (BEContext.ProtocolState.ImapServerCapabilities.HasFlag (McProtocolState.NcImapCapabilities.Idle)) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Ping");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Ping,
                        new ImapIdleCommand (BEContext));
                } else {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:WaitPing");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Ping,
                        new ImapWaitCommand (BEContext, NoIdlePollTime(), true));
                }
            }
            // (QS) Wait.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                Log.Info (Log.LOG_IMAP, "Strategy:QS:Wait");
                return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Wait,
                    new ImapWaitCommand (BEContext, 120, true));
            }
            NcAssert.True (false);
            return null;
        }
    }
}

