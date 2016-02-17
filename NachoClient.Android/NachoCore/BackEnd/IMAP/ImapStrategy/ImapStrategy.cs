//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Generic;

namespace NachoCore.IMAP
{
    public partial class ImapStrategy : NcStrategy
    {
        private Random CoinToss;

        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
            CoinToss = new Random ();
        }


        #region Pick

        public Tuple<PickActionEnum, ImapCommand> PickUserDemand ()
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
                        new ImapSearchCommand (BEContext, search));
                }
                // (FG) If the user has initiated a Sync, we do that.
                var sync = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.Sync == x.Operation).FirstOrDefault ();
                if (null != sync) {
                    SyncKit syncKit = GenSyncKit (ref protocolState, sync);
                    if (null != syncKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG:Sync");
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                            new ImapSyncCommand (BEContext,  syncKit));
                    }
                }
                // (FG) If the user has initiated a body Fetch, we do that.
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
                    FirstOrDefault (x => McPending.Operations.EmailBodyDownload == x.Operation);
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:EmailBodyDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchCommand (BEContext, fetch));
                }
                // (FG) If the user has initiated an attachment Fetch, we do that.
                fetch = McPending.QueryEligibleOrderByPriorityStamp (AccountId, McAccount.ImapCapabilities).
                    FirstOrDefault (x => McPending.Operations.AttachmentDownload == x.Operation);
                if (null != fetch) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG:AttachmentDownload");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchCommand (BEContext, fetch));
                }
            }
            return null;
        }

        public Tuple<PickActionEnum, ImapCommand> Pick ()
        {
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

            if (NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                SyncKit syncKit = GenSyncKit (ref protocolState, exeCtxt, null);
                if (null != syncKit) {
                    Log.Info (Log.LOG_IMAP, "Strategy:QS:Sync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                        new ImapSyncCommand (BEContext, syncKit));
                }
            }

            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt) {
                // (FG, BG) If there are entries in the pending queue, execute the oldest.
                var next = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities).FirstOrDefault ();
                if (null != next) {
                    // Analysis disable once ConditionIsAlwaysTrueOrFalse
                    NcAssert.True (McPending.Operations.Last == McPending.Operations.EmailSearch);
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:{0}:{1}", next.DelayNotAllowed ? "HotQOp" : "QOp", next.Operation.ToString ());
                    ImapCommand cmd = null;
                    var action = next.DelayNotAllowed ? PickActionEnum.HotQOp : PickActionEnum.QOop;
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
                        // see if there's more than one we can process for the same folder
                        var deletes = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities)
                            .Where (x => x.Operation == next.Operation &&
                                      x.ParentId == next.ParentId &&
                                      x.DelayNotAllowed == next.DelayNotAllowed);
                        cmd = new ImapEmailDeleteCommand (BEContext, deletes.ToList ());
                        break;
                    case McPending.Operations.EmailMove:
                        // see if there's more than one we can process for the same folder
                        var moves = McPending.QueryEligible (AccountId, McAccount.ImapCapabilities)
                            .Where (x => x.Operation == next.Operation &&
                                    x.ParentId == next.ParentId &&
                                    x.DestParentId == next.DestParentId &&
                                    x.DelayNotAllowed == next.DelayNotAllowed);
                        cmd = new ImapEmailMoveCommand (BEContext, moves.ToList ());
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
                    case McPending.Operations.AttachmentDownload:
                        cmd = new ImapFetchCommand (BEContext, next);
                        break;
                    case McPending.Operations.Sync:
                        var uSyncKit = GenSyncKit (ref protocolState, next);
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
                // (FG, BG) If it has been more than FolderExamineInterval (depends on exeCtxt) since last FolderSync, do a FolderSync.
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddSeconds (-FolderExamineInterval)) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Fsync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.FSync, new ImapFolderSyncCommand (BEContext));
                }

                FetchKit fetchKit;
                // (FG) See if there's bodies to download
                if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                    fetchKit = GenFetchKitHints ();
                    if (null != fetchKit) {
                        Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Fetch(Hints {0})", fetchKit.FetchBodies.Count);
                        return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Fetch, 
                            new ImapFetchCommand (BEContext, fetchKit));
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
                        new ImapFetchCommand (BEContext, fetchKit));
                }
                if (null != syncKit) {
                    Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync");
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                        new ImapSyncCommand (BEContext, syncKit));
                }

                Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Ping");
                return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Ping,
                    new ImapIdleCommand (BEContext, McFolder.GetDefaultInboxFolder (AccountId)));
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

        #endregion
    }
}

