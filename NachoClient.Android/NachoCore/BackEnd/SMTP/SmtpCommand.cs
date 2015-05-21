//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Net.Smtp;
using NachoCore.Model;
using System.Threading;
using MailKit.Security;
using MimeKit;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : ISmtpCommand
    {
        public CancellationTokenSource cToken { get; protected set; }
        public SmtpClient client { get; set; }

        public SmtpCommand(SmtpClient smtp)
        {
            cToken = new CancellationTokenSource ();
            client = smtp;
        }

        public virtual void Execute (NcStateMachine sm)
        {
        }

        public virtual void Cancel ()
        {
            cToken.Cancel ();
            lock (client.SyncRoot) {
                if (client.IsConnected) {
                    client.Disconnect (false);
                }
            }

        }
    }

    public class SmtpAuthenticateCommand : SmtpCommand
    {
        McServer Server { get; set; }
        McCred Creds { get; set; }

        public SmtpAuthenticateCommand(SmtpClient smtp, McServer server, McCred creds) : base(smtp)  // TODO Do I need the base here to get the base initializer to run?
        {
            Server = server;
            Creds = creds;
        }

        public override void Execute (NcStateMachine sm)
        {
            try {
                lock(client.SyncRoot) {
                    //client.ClientCertificates = new X509CertificateCollection ();
                    // TODO Try useSSL true and fix whatever is needed to get past the server cert warning.
                    client.Connect (Server.Host, Server.Port, false, cToken.Token);

                    // Note: since we don't have an OAuth2 token, disable
                    // the XOAUTH2 authentication mechanism.
                    client.AuthenticationMechanisms.Remove ("XOAUTH2");

                    client.Authenticate (Creds.Username, Creds.GetPassword (), cToken.Token);
                }
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
    }

    public class SmtpSendMailCommand : SmtpCommand
    {
        protected McPending Pending;

        public SmtpSendMailCommand(SmtpClient smtp, McPending pending) : base(smtp)  // TODO Do I need the base here to get the base initializer to run?
        {
            Pending = pending;
        }

        public override void Execute (NcStateMachine sm)
        {
            Pending.MarkDispached ();

            McEmailMessage EmailMessage = McAbstrObject.QueryById<McEmailMessage> (Pending.ItemId);
            McBody body = McBody.QueryById<McBody> (EmailMessage.BodyId);
            MimeMessage mimeMessage = MimeHelpers.LoadMessage (body);
            var attachments = McAttachment.QueryByItemId (EmailMessage);
            if (attachments.Count > 0) {
                MimeHelpers.AddAttachments (mimeMessage, attachments);
            }

            try {
                lock(client.SyncRoot) {
                    client.Send (mimeMessage, cToken.Token);
                }
                if (cToken.IsCancellationRequested) {
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "SMTPRETRYFAIL");
                } else {
                    sm.PostEvent ((uint)SmEvt.E.Success, "SMTPCONNSUC");
                }
            }
            catch (SmtpProtocolException e) {
                Log.Error (Log.LOG_SMTP, "Could not set up authenticated client: {0}", e);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPPROTOFAIL");
            }
        }
    }
}

