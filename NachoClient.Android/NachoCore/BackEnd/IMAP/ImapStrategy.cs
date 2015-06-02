//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
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
        public ImapStrategy (IBEContext becontext) : base (becontext)
        {
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState)
        {
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.Flags
                | MessageSummaryItems.InternalDate
                | MessageSummaryItems.MessageSize
                | MessageSummaryItems.UniqueId
                | MessageSummaryItems.GMailMessageId
                | MessageSummaryItems.GMailThreadId;

            var inbox = McFolder.GetDefaultInboxFolder (accountId);
            var syncKit = new SyncKit () {
                Method = SyncKit.MethodEnum.Range,
                Folder = inbox,
                Flags = flags,
                // Span value here indicates preferred chunk size.
                // FIXME - dynamic size.
                Span = 5,
            };
            if (null == inbox || 0 == inbox.ImapUidNext) {
                // We really need to do an Open/SELECT to get UidNext before we start.
                syncKit.Method = SyncKit.MethodEnum.OpenOnly;
                return syncKit;
            }
            if (inbox.ImapUidNext - 1 > inbox.ImapUidHighestUidSynced) {
                // Prefer to sync from latest toward oldest.
                // Start as high as we can, guard against the scenario where Span > UidNext.
                syncKit.Start =
                    Math.Max (inbox.ImapUidHighestUidSynced, 
                        (syncKit.Span + 1) >= inbox.ImapUidNext ? 1 : 
                        inbox.ImapUidNext - 1 - syncKit.Span);
                syncKit.Span =
                    Math.Min (syncKit.Span, 
                        (inbox.ImapUidHighestUidSynced >= inbox.ImapUidNext) ? 1 :
                        inbox.ImapUidNext - inbox.ImapUidHighestUidSynced);
                return syncKit;
            }
            if (1 < inbox.ImapUidLowestUidSynced) {
                // If there is nothing new to grab, then pull down older mail.
                syncKit.Start = Math.Max (1,
                    (syncKit.Span >= inbox.ImapUidLowestUidSynced) ? 1 : 
                    inbox.ImapUidLowestUidSynced - syncKit.Span - 1);
                syncKit.Span = 
                    (syncKit.Start >= inbox.ImapUidLowestUidSynced) ? 1 : 
                    Math.Min (syncKit.Span, inbox.ImapUidLowestUidSynced - syncKit.Start);
                return syncKit;
            }
            return null;
        }

        public PingKit GenPingKit ()
        {
            return new PingKit () {
            };
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
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt) {
                var fetch = McPending.QueryEligibleOrderByPriorityStamp (accountId, McAccount.ImapCapabilities).
                    Where (x => McPending.Operations.EmailBodyDownload == x.Operation).FirstOrDefault ();
                if (null != fetch) {
                    return new Tuple<PickActionEnum, ImapCommand> (PickActionEnum.HotQOp,
                        new ImapFetchBodyCommand (BEContext, fetch));
                }
            }
            // TODO add McPending operations, non-Inbox folders, etc.
            if (NcApplication.ExecutionContextEnum.Foreground == exeCtxt ||
                NcApplication.ExecutionContextEnum.Background == exeCtxt ||
                NcApplication.ExecutionContextEnum.QuickSync == exeCtxt) {
                // (FG,BG,QS) if there is syncing to do, then get it done.
                var syncKit = GenSyncKit (BEContext.Account.Id, protocolState);
                if (null != syncKit) {
                    return new Tuple<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                        new ImapSyncCommand (BEContext, syncKit));
                }
            }
            // TODO FG/BG only.
            return new Tuple<PickActionEnum, ImapCommand> (PickActionEnum.Ping, 
                new ImapIdleCommand (BEContext, GenPingKit ()));
        }
    }
}

