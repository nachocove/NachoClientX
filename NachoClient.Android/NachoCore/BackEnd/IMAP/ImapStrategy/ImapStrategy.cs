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
using System.Threading;

namespace NachoCore.IMAP
{
    public partial class ImapStrategy : NcStrategy
    {
        /// <summary>
        /// The base sync-window size
        /// </summary>
        const uint KBaseOverallWindowSize = 10;

        /// <summary>
        /// The Inbox message count after which we'll transition out of Stage/Rung 0
        /// </summary>
        const int KImapSyncRung0InboxCount = 200;

        /// <summary>
        /// The size of the initial (rung 0) sync window size. It's also the base-number for other
        /// window size calculations, i.e. multiplied by a certain number for CellFast and another
        /// number for Wifi, etc.
        /// </summary>
        const uint KRung0SyncWindowSize = 3;

        private static uint[] KRungSyncWindowSize = new uint[] { KRung0SyncWindowSize, KBaseOverallWindowSize, KBaseOverallWindowSize };

        private Random CoinToss;

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
            CoinToss = new Random ();
        }


        #region Pick

        public Tuple<PickActionEnum, ImapCommand> PickUserDemand (NcImapClient Client, CancellationToken Token)
        {
            var protocolState = BEContext.ProtocolState;

            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                // (FG) If the user has initiated a Search command, we do that.
                var search = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailSearch == x.Operation).FirstOrDefault ();
                if (null != search) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailSearch");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp, 
                        new ImapSearchCommand (BEContext, Client, search));
                }
                // (FG) If the user has initiated a Sync, we do that.
                var sync = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
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
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailBodyDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailBodyDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchCommand (BEContext, Client, fetch));
                }
                // (FG) If the user has initiated an attachment Fetch, we do that.
                fetch = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.AttachmentDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:AttachmentDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchCommand (BEContext, Client, fetch));
                }
            }
            return null;
        }

        public Tuple<PickActionEnum, ImapCommand> Pick (NcImapClient Client, CancellationToken Token)
        {
            var protocolState = BEContext.ProtocolState;
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            if (NcApplication.ExecutionContextEnum.Initializing == exeCtxt) {
                // ExecutionContext is not set until after BE is started.
                exeCtxt = NcApplication.Instance.PlatformIndication;
            }
            var userDemand = PickUserDemand (Client, Token);
            if (null != userDemand) {
                return userDemand;
            }

            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                SyncKit syncKit = GenSyncKit (ref protocolState, exeCtxt, null);
                if (null != syncKit) {
                    Log.Info (Log.LOG_IMAP, "Strategy:QS:Sync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                        new ImapSyncCommand (BEContext, Client, syncKit));
                }
            }

            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                // (FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities).FirstOrDefault ();
                if (null != next) {
                    NcAssert.True (McPending.Operations.Last == McPending.Operations.EmailSearch);
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:{0}:{1}", next.DelayNotAllowed ? "HotQOp" : "QOp", next.Operation.ToString ());
                    ImapCommand cmd = null;
                    var action = next.DelayNotAllowed ? PickActionEnum.HotQOp : PickActionEnum.QOop;
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
                        // see if there's more than one we can process for the same folder
                        var deletes = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities)
                            .Where (x => x.Operation == next.Operation &&
                                      x.ParentId == next.ParentId &&
                                      x.DelayNotAllowed == next.DelayNotAllowed);
                        cmd = new ImapEmailDeleteCommand (BEContext, Client, deletes.ToList ());
                        break;
                    case McPending.Operations.EmailMove:
                        // see if there's more than one we can process for the same folder
                        var moves = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities)
                            .Where (x => x.Operation == next.Operation &&
                                    x.ParentId == next.ParentId &&
                                    x.DestParentId == next.DestParentId &&
                                    x.DelayNotAllowed == next.DelayNotAllowed);
                        cmd = new ImapEmailMoveCommand (BEContext, Client, moves.ToList ());
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
                    case McPending.Operations.AttachmentDownload:
                        cmd = new ImapFetchCommand (BEContext, Client, next);
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
                // (FG, BG) If it has been more than FolderExamineInterval (depends on exeCtxt) since last FolderSync, do a FolderSync.
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddSeconds (-FolderExamineInterval)) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Fsync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.FSync, new ImapFolderSyncCommand (BEContext, Client));
                }

                FetchKit fetchKit;
                // (FG) See if there's bodies to download
                if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                    fetchKit = GenFetchKitHints ();
                    if (null != fetchKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Fetch(Hints {0})", fetchKit.FetchBodies.Count);
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Fetch, 
                            new ImapFetchCommand (BEContext, Client, fetchKit));
                    }
                }

                // (FG, BG) Choose eligible option by priority, split tie randomly...
                fetchKit = null;
                if (protocolState.ImapSyncRung >= 2 &&
                    NetStatusSpeedEnum.WiFi_0 == NcCommStatus.Instance.Speed &&
                    PowerPermitsSpeculation ()) {
                    fetchKit = GenFetchKit ();
                }
                SyncKit syncKit = GenSyncKit (ref protocolState, exeCtxt, null);
                if (null != fetchKit && (null == syncKit || 0.7 < CoinToss.NextDouble ())) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Fetch");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Fetch, 
                        new ImapFetchCommand (BEContext, Client, fetchKit));
                }
                if (null != syncKit) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                        new ImapSyncCommand (BEContext, Client, syncKit));
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


        private static uint MaybeAdvanceSyncStage (ref McProtocolState protocolState)
        {
            McFolder defInbox = McFolder.GetDefaultInboxFolder (protocolState.AccountId);
            uint rung = protocolState.ImapSyncRung;
            switch (protocolState.ImapSyncRung) {
            case 0:
                var syncInstList = SyncInstructions (defInbox, ref protocolState);
                var uidSet = new UniqueIdSet ();
                foreach (var inst in syncInstList) {
                    uidSet.AddRange (inst.UidSet);
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

