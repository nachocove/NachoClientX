//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.IMAP
{
    public class NcImapClient : MailKit.Net.Imap.ImapClient
    {
        public MailKitProtocolLogger ProtocolLogger { get; set; }

        public NcImapClient () : base(new MailKitProtocolLogger ("IMAP"))
        {
            ProtocolLogger = Logger() as MailKitProtocolLogger;
        }
    }
}
