﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Net.Smtp;
using NachoCore.Model;
using System.Threading;
using MailKit.Security;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : ISmtpCommand
    {
        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
        }
    }

    public class SmtpAuthenticateCommand : SmtpCommand
    {
        public CancellationTokenSource cToken { get; protected set; }

        SmtpClient client { get; set; }
        McServer Server { get; set; }
        McCred Creds { get; set; }
        public SmtpAuthenticateCommand(McServer server, McCred creds, SmtpClient smtp) : base()  // TODO Do I need the base here to get the base initializer to run?
        {
            cToken = new CancellationTokenSource ();
            client = smtp;
            Server = server;
            Creds = creds;
        }

        async public override void Execute (NcStateMachine sm)
        {
            try {
                //client.ClientCertificates = new X509CertificateCollection ();
                await client.ConnectAsync (Server.Host, Server.Port, false, cToken.Token).ConfigureAwait (false);

                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                client.AuthenticationMechanisms.Remove ("XOAUTH2");

                await client.AuthenticateAsync (Creds.Username, Creds.GetPassword (), cToken.Token).ConfigureAwait (false);
                sm.PostEvent ((uint)SmEvt.E.Success, "SMTPCONNSUC");
            }
            catch (SmtpProtocolException e) {
                Log.Error (Log.LOG_SMTP, "Could not set up authenticated client: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPPROTOFAIL");
            }
            catch (AuthenticationException e) {
                Log.Error (Log.LOG_SMTP, "Authentication failed: {0}", e);
                sm.PostEvent ((uint)NachoCore.SMTP.SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTHFAIL");
            }
        }

        async public override void Cancel()
        {
            cToken.Cancel ();
            await client.DisconnectAsync (true).ConfigureAwait (false);
        }
    }
}

