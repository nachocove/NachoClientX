//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.IMAP;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.SMTP
{
    public class SmtpAuthenticateCommand : SmtpCommand
    {
        public SmtpAuthenticateCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext, smtp)
        {
        }

        public void ConnectAndAuthenticate ()
        {
            ImapDiscoverCommand.guessServiceType (BEContext);

            if (!Client.IsConnected) {
                //client.ClientCertificates = new X509CertificateCollection ();
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, false, Cts.Token);
                Log.Info (Log.LOG_SMTP, "SMTP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
            }
            if (!Client.IsAuthenticated) {
                RedactProtocolLogFuncDel RestartLog = null;
                if (Client.MailKitProtocolLogger.Enabled ()) {
                    ProtocolLoggerStopAndLog ();
                    RestartLog = Client.MailKitProtocolLogger.RedactProtocolLogFunc;
                }

                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    // FIXME - be exhaustive w/Remove when we know we MUST use an auth mechanism.
                    Client.AuthenticationMechanisms.Remove ("LOGIN");
                    Client.AuthenticationMechanisms.Remove ("PLAIN");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetAccessToken (), Cts.Token);
                } else {
                    Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                }
                Log.Info (Log.LOG_SMTP, "SMTP Server capabilities: {0}", Client.Capabilities.ToString ());
                if (null != RestartLog) {
                    Client.MailKitProtocolLogger.Start (RestartLog);
                }
            }
        }

        protected override Event ExecuteCommand ()
        {
            try {
                if (Client.IsConnected) {
                    Client.Disconnect (false, Cts.Token);
                }
                ConnectAndAuthenticate ();
                return Event.Create ((uint)SmEvt.E.Success, "SMTPAUTHSUC");
            } catch (NotSupportedException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                return Event.Create ((uint)SmEvt.E.HardFail, "SMTPAUTHHARD0");
            }
        }
    }

}

