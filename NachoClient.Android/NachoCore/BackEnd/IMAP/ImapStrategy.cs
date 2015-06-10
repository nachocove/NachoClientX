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

        private bool folderNeedsResync(McFolder folder)
        {
            //return (ulong)folder.CurImapHighestModSeq != (ulong)folder.LastImapHighestModSeq;
            // TODO Need to understand HighestModSeq better! seems to return the same for every folder. Is it global?
            return false;

        }
        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder, bool UserRequested = false)
        {
            if (folder.ImapNoSelect) {
                return null;
            }

            MessageSummaryItems flags = MessageSummaryItems.BodyStructure
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.Flags
                | MessageSummaryItems.InternalDate
                | MessageSummaryItems.MessageSize
                | MessageSummaryItems.UniqueId
                | MessageSummaryItems.GMailMessageId
                | MessageSummaryItems.GMailThreadId;

            var syncKit = new SyncKit () {
                Method = SyncKit.MethodEnum.Range,
                Folder = folder,
                Flags = flags,
                Span = SpanSizeWithCommStatus(),
            };
            if (null == folder ||
                0 == folder.ImapUidNext ||
                UserRequested ||
                folder.ImapLastExamine < DateTime.UtcNow.AddSeconds(-NoIdlePollTime())) // perhaps this should be passed in by the caller?
            {
                // We really need to do an Open/SELECT to get UidNext before we can sync this folder.
                syncKit.Method = SyncKit.MethodEnum.OpenOnly;
                return syncKit;
            }
            if (folderNeedsResync (folder) || UserRequested) {
                // HACK ALERT -- reset the lowest and highest to resync everything.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapUidHighestUidSynced = UInt32.MinValue;
                    target.ImapUidLowestUidSynced = UInt32.MaxValue;
                    return true;
                });
            }
            var currentHighestInFolder = folder.ImapUidNext - 1;
            if (currentHighestInFolder > folder.ImapUidHighestUidSynced) {
                // Prefer to sync from latest toward oldest.
                // Start as high as we can, guard against the scenario where Span > UidNext.
                syncKit.Span = 10; // use a very small window for the first sync, so we quickly get stuff back to display
                syncKit.Start =
                    Math.Max (folder.ImapUidHighestUidSynced + 1, 
                        (syncKit.Span + 1) >= folder.ImapUidNext ? 1 : 
                        currentHighestInFolder - syncKit.Span);
                syncKit.Span =
                    Math.Min (syncKit.Span, 
                        (folder.ImapUidHighestUidSynced >= folder.ImapUidNext) ? 1 :
                        currentHighestInFolder - folder.ImapUidHighestUidSynced);
                return syncKit;
            }
            if (currentHighestInFolder > 0 && // are there any messages at all?
                folder.ImapLowestUid < folder.ImapUidLowestUidSynced)
            {
                // If there is nothing new to grab, then pull down older mail.
                syncKit.Start = 
                    (syncKit.Span + 1 >= folder.ImapUidLowestUidSynced) ? 1 : 
                    folder.ImapUidLowestUidSynced - syncKit.Span - 1;
                syncKit.Span = 
                    (syncKit.Start >= folder.ImapUidLowestUidSynced) ? 1 : 
                    Math.Min (syncKit.Span, folder.ImapUidLowestUidSynced - syncKit.Start);
                return syncKit;
            }

            return null;
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
                    foreach (var folder in McFolder.QueryByIsClientOwned (accountId, false)) {
                        SyncKit syncKit = GenSyncKit (accountId, protocolState, folder);
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

