//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MailKit;
using MailKit.Net.Imap;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class SyncKit
    {
        public enum MethodEnum {
            Range,
            OpenOnly,
        };
        public MethodEnum Method;
        public IMailFolder MailKitFolder;
        public McFolder Folder;
        public MessageSummaryItems Flags;
        public uint Start;
        public uint Span;
    }

    public class PingKit
    {
        public IMailFolder MailKitFolder;
    }

    public interface IImapStrategy
    {
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, ImapClient imapClient);
        PingKit GenPingKit (ImapClient imapClient);
    }
}
