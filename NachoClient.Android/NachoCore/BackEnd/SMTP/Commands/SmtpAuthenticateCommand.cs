//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.IMAP;
using NachoCore.Utils;
using NachoCore.Model;
using MailKit.Security;
using MailKit;
using MailKit.Net.Smtp;

namespace NachoCore.SMTP
{
    public class SmtpAuthenticateCommand : SmtpCommand
    {
        const int KAuthRetries = 2;

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
                if (null != Client.MailKitProtocolLogger && Client.MailKitProtocolLogger.Enabled ()) {
                    ProtocolLoggerStopAndLog ();
                    RestartLog = Client.MailKitProtocolLogger.RedactProtocolLogFunc;
                }

                string username = BEContext.Cred.Username;
                string cred;
                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => !m.Contains ("XOAUTH2"));
                    cred = BEContext.Cred.GetAccessToken ();
                } else {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => m.Contains ("XOAUTH"));
                    cred = BEContext.Cred.GetPassword ();
                }

                Exception ex = null;
                for (var i = 0; i++ < KAuthRetries; ) {
                    try {
                        try {
                            ex = null;
                            Client.Authenticate (username, cred, Cts.Token);
                            break;
                        } catch (SmtpProtocolException e) {
                            Log.Info (Log.LOG_SMTP, "Protocol Error during auth: {0}", e);
                            // some servers (icloud.com) seem to close the connection on a bad password/username.
                            throw new AuthenticationException (e.Message);
                        }
                    } catch (AuthenticationException e) {
                        ex = e;
                        Log.Warn (Log.LOG_SMTP, "AuthenticationException: {0}", ex.Message);
                        continue;
                    } catch (ServiceNotAuthenticatedException e) {
                        ex = e;
                        Log.Warn (Log.LOG_SMTP, "ServiceNotAuthenticatedException: {0}", e.Message);
                    }
                }
                if (null != ex) {
                    throw ex;
                }

                Log.Info (Log.LOG_SMTP, "SMTP Server capabilities: {0}", Client.Capabilities.ToString ());
                if (null != Client.MailKitProtocolLogger && null != RestartLog) {
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

