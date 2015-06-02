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
    public abstract class SmtpCommand : NcCommand
    {
        public SmtpClient client { get; set; }

        public class SmtpCommandFailure : Exception {
            public SmtpCommandFailure (string message) : base (message)
            {
            }

        }

        public SmtpCommand (IBEContext beContext, SmtpClient smtp, bool checkConnected = true) : base (beContext)
        {
            if (null == smtp) {
                throw new SmtpCommandFailure("No client passed in");
            }
            if (checkConnected) {
                if (!smtp.IsConnected) {
                    throw new SmtpCommandFailure ("SmtpCommand: Client is not connected");
                }
            }
            client = smtp;
        }

        public override void Cancel ()
        {
            base.Cancel ();
            lock (client.SyncRoot) {
                if (client.IsConnected) {
                    client.Disconnect (false);
                }
            }

        }
    }

    public class SmtpAuthenticateCommand : SmtpCommand
    {
        public SmtpAuthenticateCommand(IBEContext beContext, SmtpClient smtp) : base(beContext, smtp, false)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            try {
                lock(client.SyncRoot) {
                    //client.ClientCertificates = new X509CertificateCollection ();
                    // TODO Try useSSL true and fix whatever is needed to get past the server cert warning.
                    client.Connect (BEContext.Server.Host, BEContext.Server.Port, false, Cts.Token);

                    // Note: since we don't have an OAuth2 token, disable
                    // the XOAUTH2 authentication mechanism.
                    client.AuthenticationMechanisms.Remove ("XOAUTH2");

                    client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
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
        public SmtpSendMailCommand(IBEContext beContext, SmtpClient smtp, McPending pending) : base(beContext, smtp)  // TODO Do I need the base here to get the base initializer to run?
        {
            PendingSingle = pending;
        }

        public override void Execute (NcStateMachine sm)
        {
            // FIXME JAN - PendingSingle needs to be resolved when handling success/failure.
            PendingSingle.MarkDispached ();

            McEmailMessage EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
            McBody body = McBody.QueryById<McBody> (EmailMessage.BodyId);
            MimeMessage mimeMessage = MimeHelpers.LoadMessage (body);
            var attachments = McAttachment.QueryByItemId (EmailMessage);
            if (attachments.Count > 0) {
                MimeHelpers.AddAttachments (mimeMessage, attachments);
            }

            try {
                lock(client.SyncRoot) {
                    client.Send (mimeMessage, Cts.Token);
                }
                if (Cts.IsCancellationRequested) {
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

