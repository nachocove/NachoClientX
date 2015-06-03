//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
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
        public McFolder Folder;
        public MessageSummaryItems Flags;
        public uint Start;
        public uint Span;
        // PendingSingle is null if Strategy decided to Sync.
        public McPending PendingSingle;
        public bool isNarrow;
    }

    public interface IImapStrategy
    {
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending);
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder);
    }
}
