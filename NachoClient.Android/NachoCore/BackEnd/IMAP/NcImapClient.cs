//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class NcImapClient : MailKit.Net.Imap.ImapClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public NcImapClient () : base(new NcMailKitProtocolLogger ("IMAP"))
        {
            MailKitProtocolLogger = ProtocolLogger as NcMailKitProtocolLogger;
        }
    }
}
