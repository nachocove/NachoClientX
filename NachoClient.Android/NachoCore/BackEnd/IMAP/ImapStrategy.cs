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

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending)
        {
            NcAssert.True (McPending.Operations.Sync == pending.Operation);
            var folder = McFolder.QueryByServerId<McFolder> (accountId, pending.ServerId);
            var syncKit = GenSyncKit (accountId, protocolState, folder);
            if (null != syncKit) {
                syncKit.PendingSingle = pending;
            }
            return syncKit;
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder)
        {
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
            };
            uint overallWindowSize = KBaseOverallWindowSize;
            switch (NcCommStatus.Instance.Speed) {
            case NetStatusSpeedEnum.CellFast_1:
                overallWindowSize *= 2;
                break;
            case NetStatusSpeedEnum.WiFi_0:
                overallWindowSize *= 3;
                break;
            }
            syncKit.Span = overallWindowSize;

            if (null == folder || 0 == folder.ImapUidNext) {
                // We really need to do an Open/SELECT to get UidNext before we can sync this folder.
                syncKit.Method = SyncKit.MethodEnum.OpenOnly;
                return syncKit;
            }
            if (folder.ImapUidNext - 1 > folder.ImapUidHighestUidSynced) {
                // Prefer to sync from latest toward oldest.
                // Start as high as we can, guard against the scenario where Span > UidNext.
                syncKit.Start =
                    Math.Max (folder.ImapUidHighestUidSynced, 
                        (syncKit.Span + 1) >= folder.ImapUidNext ? 1 : 
                        folder.ImapUidNext - 1 - syncKit.Span);
                syncKit.Span =
                    Math.Min (syncKit.Span, 
                        (folder.ImapUidHighestUidSynced >= folder.ImapUidNext) ? 1 :
                        folder.ImapUidNext - folder.ImapUidHighestUidSynced);
                return syncKit;
            }
            if (1 < folder.ImapUidLowestUidSynced) {
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

        public override bool ANarrowFolderHasToClientExpected (int accountId)
        {
            var defInbox = McFolder.GetDefaultInboxFolder (accountId);
            if (defInbox.ImapUidLowestUidSynced > 1 ||
                defInbox.ImapUidHighestUidSynced + 1 < defInbox.ImapUidNext) {
                return true;
            }
            return false;
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

            // FIXME Investigate removing the narrow-sync stuff.

            // (QS) If a narrow Sync hasn’t successfully completed in the last N seconds, 
            // perform a narrow Sync Command.
            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                if (protocolState.LastNarrowSync < DateTime.UtcNow.AddSeconds (-60)) {
                    var nSyncKit = GenSyncKit (accountId, protocolState, McFolder.GetDefaultInboxFolder (accountId));
                    Log.Info (Log.LOG_IMAP, "Strategy:QS:Inbox...");
                    if (null != nSyncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:QS:...SyncKit");
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, nSyncKit));
                    }
                }
            }
            // TODO move user-directed Sync up to this priority level in FG.
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                // (FG, BG) Unless one of these conditions are met, perform a narrow Sync Command...
                // The goal here is to ensure a narrow Sync periodically so that new Inbox/default cal aren't crowded out.
                var needNarrowSyncMarker = DateTime.UtcNow.AddSeconds (-300);
                if (protocolState.LastNarrowSync < needNarrowSyncMarker &&
                    (protocolState.LastPing < needNarrowSyncMarker || ANarrowFolderHasToClientExpected (accountId))) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Narrow Sync...");
                    var nSyncKit = GenSyncKit (accountId, protocolState, McFolder.GetDefaultInboxFolder (accountId));
                    if (null != nSyncKit) {
                        nSyncKit.isNarrow = true;
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:...SyncKit");
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, nSyncKit));
                    }
                }
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
                // (FG, BG) Choose eligible option by priority, split tie randomly...
                if (PowerPermitsSpeculation () ||
                    NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                    SyncKit syncKit = null;
                    // FIXME JAN once ImapSyncCommand can do other folders, we need to sync all the folders.
                    // FIXME JAN once ImapXxxDownloadCommand can handle a FetchKit", lift logic from EAS 
                    // for speculatively pre-fetching bodies and attachments.
                    syncKit = GenSyncKit (accountId, protocolState, McFolder.GetDefaultInboxFolder (accountId));
                    if (null != syncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync");
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                            new ImapSyncCommand (BEContext, syncKit));
                    }
                }
                if (!ANarrowFolderHasToClientExpected (accountId)) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Ping");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Ping,
                        new ImapIdleCommand (BEContext));
                }
                Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Wait");
                return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Wait,
                    new ImapWaitCommand (BEContext, 120, false));
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

