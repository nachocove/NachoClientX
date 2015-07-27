//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.SMTP
{
    public class NcSmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public NcSmtpClient () : base(getLogger())
        {
            MailKitProtocolLogger = ProtocolLogger as NcMailKitProtocolLogger;
        }

        private static IProtocolLogger getLogger ()
        {
            //return new NcMailKitProtocolLogger("SMTP");
            return new NullProtocolLogger ();
        }
    }
}

