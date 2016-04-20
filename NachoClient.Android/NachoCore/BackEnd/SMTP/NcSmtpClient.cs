//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit;

namespace NachoCore.SMTP
{
    public class NcSmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public bool DOA { get; set; }

        public NcSmtpClient () : base(getLogger())
        {
        }

        private static IProtocolLogger getLogger ()
        {
            //return new NcDebugProtocolLogger (Log.LOG_SMTP);
            return new NullProtocolLogger ();
        }
    }
}

