//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit;
using NachoCore.Model;
using System.Threading;
using MailKit.Security;
using System.Net.Sockets;

namespace NachoCore.SMTP
{
    public class NcSmtpClient : MailKit.Net.Smtp.SmtpClient
    {
        public NcMailKitProtocolLogger MailKitProtocolLogger { get; private set; }

        public bool DOA { get; set; }

        public NcSmtpClient () : base(getLogger())
        {
            MailKitProtocolLogger = ProtocolLogger as NcMailKitProtocolLogger;
        }

        private static IProtocolLogger getLogger ()
        {
            //return new NcMailKitProtocolLogger("SMTP");
            //return new NcDebugProtocolLogger (Log.LOG_SMTP);
            return new NullProtocolLogger ();
        }

        public void Connect (McServer server, int connectTimeout, int timeout, CancellationToken cancellationToken)
        {
            var socket = NachoCore.IMAP.NcImapClient.GetSocket (server, connectTimeout, timeout, cancellationToken);
            if (null == socket) {
                Log.Error (Log.LOG_SMTP, "Could not open socket to {0}:{1}", server.Host, server.Port);
                throw new SocketException ();
            }
            // the parent class now owns the socket, so it's responsible for closing, freeing, etc.
            Connect (socket, server.Host, server.Port, SecureSocketOptions.StartTlsWhenAvailable, cancellationToken);
        }
    }
}

