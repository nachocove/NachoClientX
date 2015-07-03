//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.SMTP
{
    public class NcSmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public NcSmtpClient () : base(new NcMailKitProtocolLogger("SMTP"))
        {
            MailKitProtocolLogger = ProtocolLogger as NcMailKitProtocolLogger;
        }
    }
}

