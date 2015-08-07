//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using System;

namespace NachoCore.SMTP
{
    public class SmtpDisconnectCommand : SmtpCommand
    {
        public SmtpDisconnectCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext, smtp)
        {
        }
        public override Event ExecuteConnectAndAuthEvent ()
        {
            // For this we don't want to connect-and-auth. Just override it and disconnect.
            lock (Client.SyncRoot) {
                try {
                    if (null != Client.MailKitProtocolLogger && null != RedactProtocolLogFunc) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (Client.IsConnected) {
                        try {
                            Client.Disconnect (true, Cts.Token);
                        } catch (Exception ex) {
                            Log.Warn (Log.LOG_SMTP, "SmtpDisconnectCommand: Exception (ignoring): {0}", ex);
                            if (Client.IsConnected) {
                                Log.Error (Log.LOG_SMTP, "SmtpDisconnectCommand: Disconnect failed.");
                            }
                        }
                    }
                    return Event.Create ((uint)SmEvt.E.Success, "SMTPDISCSUC");
                } finally {
                    if (null != Client.MailKitProtocolLogger && Client.MailKitProtocolLogger.Enabled ()) {
                        ProtocolLoggerStopAndLog ();
                    }
                }
            }
        }
    }
}

