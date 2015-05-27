//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
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
        private ImapClient ImapClient;

        public ImapStrategy (IBEContext becontext, ImapClient imapClient) : base (becontext)
        {
            ImapClient = imapClient;
        }

        public SyncKit GenSyncKit (int accountId, McProtocolState protocolState, ImapClient imapClient)
        {
            // Dumb code to get Inbox.
            MessageSummaryItems flags = MessageSummaryItems.BodyStructure
                | MessageSummaryItems.Envelope
                | MessageSummaryItems.Flags
                | MessageSummaryItems.InternalDate
                | MessageSummaryItems.MessageSize
                | MessageSummaryItems.UniqueId
                | MessageSummaryItems.GMailMessageId
                | MessageSummaryItems.GMailThreadId;

            var inbox = McFolder.GetDefaultInboxFolder (accountId);
            return new SyncKit () {
                Method = SyncKit.MethodEnum.Range,
                MailKitFolder = imapClient.Inbox,
                Folder = inbox,
                Flags = flags,
                Start = (0 == inbox.ImapLargestUid) ? 1 : inbox.ImapLargestUid,
                Span = 20,
            };
        }

        public Tuple<PickActionEnum, ImapCommand> Pick ()
        {
            return new Tuple<PickActionEnum, ImapCommand> (PickActionEnum.Sync, 
                new ImapSyncCommand (BEContext, ImapClient,
                    GenSyncKit (BEContext.Account.Id, BEContext.ProtocolState, ImapClient)));
        }
    }
}

