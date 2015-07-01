//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using MimeKit;

namespace NachoCore.IMAP
{
    public class ImapStrategy : NcStrategy
    {
        public const uint KBaseOverallWindowSize = 10;
        public const int KBaseNoIdlePollTime = 60;
        // seconds

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

        private int NoIdlePollTime ()
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
        /// </remarks>
        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder, bool UserRequested = false)
        {
            if (null == folder) {
                return null;
            }
            if (folder.ImapNoSelect) {
                return null;
            }
            SyncKit syncKit = null;
            var currentHighestInFolder = new UniqueId (folder.ImapUidNext - 1);
            UniqueIdSet UidSet;
            if (!string.IsNullOrEmpty (folder.ImapUidSet)) {
                if (!UniqueIdSet.TryParse (folder.ImapUidSet, folder.ImapUidValidity, out UidSet)) {
                    Log.Error (Log.LOG_IMAP, "Could not parse uid set");
                    return null;
                }
            } else {
                UidSet = new UniqueIdSet ();
            }

            if (UserRequested ||
                0 == folder.ImapUidNext ||
                null == folder.ImapUidSet ||
                folder.ImapLastExamine < DateTime.UtcNow.AddSeconds (-NoIdlePollTime ())) {
                // We really need to do an Open/SELECT to get UidNext, etc before we can sync this folder.
                syncKit = new SyncKit (folder);
            } else {
                uint span = UInt32.MinValue == folder.ImapUidHighestUidSynced || currentHighestInFolder.Id > folder.ImapUidHighestUidSynced ? KBaseOverallWindowSize : SpanSizeWithCommStatus ();

                UniqueIdSet syncSet;
                UniqueIdSet uids;

                if (HasNewMail (folder)) {
                    resetLastSyncPoint (ref folder);
                    syncSet = getFetchUIDs (folder, span);
                    var highestUid = new UniqueId (folder.ImapUidNext - 1);
                    if (syncSet.Any () && !syncSet.Contains(highestUid))
                    {
                        // need to artificially add this to the set, otherwise we'll loop forever if there's a hole at the top.
                        syncSet.Add (highestUid);
                        if (syncSet.Count > span) {
                            var lowest = syncSet.Min ();
                            syncSet.Remove (lowest);
                        }
                    }
                } else if (HasChangedMails (folder, out uids)) {
                    // FIXME This needs work. Need to figure out how to detect changed emails.
                    if (uids.Any ()) {
                        syncSet = uids;
                    } else {
                        resetLastSyncPoint (ref folder);
                        syncSet = getFetchUIDs (folder, span);
                    }
                } else if (HasDeletedMail (folder, out uids)) {
                    syncSet = uids;
                } else {
                    // continue fetching older mails
                    syncSet = getFetchUIDs (folder, span);
                }

                if (syncSet.Any ()) {
                    HashSet<HeaderId> headers = new HashSet<HeaderId> ();
                    headers.Add (HeaderId.Importance);
                    headers.Add (HeaderId.DkimSignature);
                    headers.Add (HeaderId.ContentClass);

                    syncKit = new SyncKit (folder, syncSet, SummaryFlags, headers);
                }
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
            return (0 != folder.ImapUidHighestUidSynced && folder.ImapUidHighestUidSynced < folder.ImapUidNext - 1);
        }

        private bool HasDeletedMail (McFolder folder, out UniqueIdSet uids)
        {
            uids = new UniqueIdSet ();
            if (!string.IsNullOrEmpty (folder.ImapUidSet)) {
                UniqueIdSet current;
                if (!(UniqueIdSet.TryParse (folder.ImapUidSet, out current))) {
                    Log.Error (Log.LOG_IMAP, "{0}: Could not parse ImapUidSet: {1}", folder.ImapFolderNameRedacted (), folder.ImapUidSet);
                    return false;
                }

                // get a list of all email UID's in the folder, and subtract out the current list as well.
                // this gives us a list of messages we still have, that are not on the server.
                UniqueIdSet currentMails = getCurrentEmailUids (folder);
                uids.AddRange (currentMails.Except (current));
            }
            return uids.Any ();
        }

        private bool HasChangedMails (McFolder folder, out UniqueIdSet uids)
        {
            uids = new UniqueIdSet ();
            // FIXME How to determine (ignoring ModSeq for now)?
            return false;
        }

        private UniqueIdSet getFetchUIDs (McFolder folder, uint span)
        {
            uint max = 0 != folder.ImapLastUidSynced ? folder.ImapLastUidSynced : folder.ImapUidNext;
            UniqueIdSet currentMails = getCurrentEmailUids (folder, 0, max, span);
            UniqueIdSet currentUids = getCurrentUIDSet (folder, 0, max, span);
            return SyncKit.MustUniqueIdSet (currentUids.Except (currentMails).OrderByDescending (x => x).Take ((int)span).ToList ());
        }

        private UniqueIdSet getCurrentEmailUids (McFolder folder)
        {
            return getCurrentEmailUids (folder, 0, 0, 0);
        }

        private UniqueIdSet getCurrentEmailUids (McFolder folder, uint min, uint max, uint span)
        {
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

        private UniqueIdSet getCurrentUIDSet (McFolder folder)
        {
            return getCurrentUIDSet (folder, 0, 0, 0);
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
                if (protocolState.AsLastFolderSync < DateTime.UtcNow.AddMinutes (-5)) {
                    return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.FSync, new ImapFolderSyncCommand (BEContext, Client));
                }
                // (FG, BG) if the IMAP server doesn't support IDLE, we need to poll
                if (!BEContext.ProtocolState.ImapServerCapabilities.HasFlag (McProtocolState.NcImapCapabilities.Idle)) {
                    var defInbox = McFolder.GetDefaultInboxFolder (accountId);
                    if (defInbox.ImapLastExamine < DateTime.UtcNow.AddSeconds (-NoIdlePollTime ())) {
                        SyncKit syncKit = GenSyncKit (accountId, protocolState, defInbox);
                        if (null != syncKit) {
                            Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:PollSync {0}", defInbox.ServerId);
                            return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                                new ImapSyncCommand (BEContext, Client, syncKit));
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
                            new ImapSyncCommand (BEContext, Client, syncKit));
                    }
                    foreach (var folder in McFolder.QueryByIsClientOwned (accountId, false)) {
                        if (defInbox.Id == folder.Id) {
                            continue;
                        }
                        syncKit = GenSyncKit (accountId, protocolState, folder);
                        if (null != syncKit) {
                            Log.Info (Log.LOG_IMAP, "Strategy:FG/BG:Sync {0}", folder.ImapFolderNameRedacted ());
                            return Tuple.Create<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                                new ImapSyncCommand (BEContext, Client, syncKit));
                        }
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
    }
}

