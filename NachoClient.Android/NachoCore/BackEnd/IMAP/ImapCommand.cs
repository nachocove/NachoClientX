//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;
using MailKit.Net.Imap;
using NachoCore.Model;
using MailKit.Security;

namespace NachoCore.IMAP
{
    public class ImapCommand : IImapCommand
    {
        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
        }
    }

    public class ImapAuthenticateCommand : ImapCommand
    {
        public CancellationTokenSource cToken { get; protected set; }

        ImapClient client { get; set; }
        McServer Server { get; set; }
        McCred Creds { get; set; }
        public ImapAuthenticateCommand(McServer server, McCred creds, ImapClient smtp) : base()  // TODO Do I need the base here to get the base initializer to run?
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
                sm.PostEvent ((uint)SmEvt.E.Success, "IMAPCONNSUC");
            }
            catch (ImapProtocolException e) {
                Log.Error (Log.LOG_IMAP, "Could not set up authenticated client: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPPROTOFAIL");
            }
            catch (AuthenticationException e) {
                Log.Error (Log.LOG_IMAP, "Authentication failed: {0}", e);
                sm.PostEvent ((uint)NachoCore.IMAP.ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL");
            }
        }

        async public override void Cancel()
        {
            cToken.Cancel ();
            await client.DisconnectAsync (true).ConfigureAwait (false);
        }
    }
}

