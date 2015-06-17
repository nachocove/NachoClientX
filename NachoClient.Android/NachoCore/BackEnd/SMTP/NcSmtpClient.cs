//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.SMTP
{
    public class NcSmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public readonly MailKitProtocolLogger ProtocolLogger;
        public NcSmtpClient () : base(new MailKitProtocolLogger("SMTP"))
        {
            ProtocolLogger = Logger () as MailKitProtocolLogger;
        }
    }
}

