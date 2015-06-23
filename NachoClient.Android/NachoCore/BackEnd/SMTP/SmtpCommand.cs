//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Net.Smtp;
using NachoCore.Model;
using System.Threading;
using MailKit.Security;
using MimeKit;
using MailKit;
using System.IO;
using System.Text;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : NcCommand
    {
        public NcSmtpClient Client { get; set; }
        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;

        public class SmtpCommandFailure : Exception {
            public SmtpCommandFailure (string message) : base (message)
            {
            }
        }

        public SmtpCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext)
        {
            Client = smtp;
            RedactProtocolLogFunc = null;
        }

        // MUST be overridden by subclass.
        protected virtual Event ExecuteCommand ()
        {
            NcAssert.True (false);
            return null;
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                try {
                    Event evt;
                    lock (Client.SyncRoot) {
                        try {
                            if (null != RedactProtocolLogFunc) {
                                Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                            }
                            if (!Client.IsConnected || !Client.IsAuthenticated) {
                                var authy = new SmtpAuthenticateCommand(BEContext, Client);
                                authy.ConnectAndAuthenticate ();
                            }
                            Client.MailKitProtocolLogger.ResetBuffers ();
                            evt = ExecuteCommand ();
                        } finally {
                            if (Client.MailKitProtocolLogger.Enabled ()) {
                                ProtocolLoggerStopAndLog ();
                            }
                        }
                    }
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    sm.PostEvent (evt);
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_SMTP, "OperationCanceledException");
                    ResolveAllDeferred ();
                    // No event posted to SM if cancelled.
                } catch (ServiceNotConnectedException) {
                    // FIXME - this needs to feed into NcCommStatus, not loop forever.
                    Log.Info (Log.LOG_SMTP, "ServiceNotConnectedException");
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.ReDisc, "SMTPCONN");
                } catch (AuthenticationException) {
                    Log.Info (Log.LOG_SMTP, "AuthenticationException");
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH1");
                } catch (ServiceNotAuthenticatedException) {
                    Log.Info (Log.LOG_SMTP, "ServiceNotAuthenticatedException");
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH2");
                } catch (IOException ex) {
                    Log.Info (Log.LOG_SMTP, "IOException: {0}", ex.ToString ());
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "SMTPIO");
                } catch (InvalidOperationException ex) {
                    Log.Error (Log.LOG_SMTP, "InvalidOperationException: {0}", ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPHARD1");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SMTP, "Exception : {0}", ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.Unknown);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPHARD2");
                }
            }, this.GetType ().Name);
        }

        public override void Cancel ()
        {
            base.Cancel ();
            lock (Client.SyncRoot) {
                if (Client.IsConnected) {
                    Client.Disconnect (false);
                }
            }
        }

        protected void ProtocolLoggerStopAndLog ()
        {
            string ClassName = this.GetType ().Name + " ";
            Log.Info (Log.LOG_SMTP, "{0}SMTP exchange\n{1}", ClassName, Encoding.UTF8.GetString (Client.MailKitProtocolLogger.GetCombinedBuffer ()));
            Client.MailKitProtocolLogger.Stop ();
        }
    }

    public class SmtpAuthenticateCommand : SmtpCommand
    {
        public SmtpAuthenticateCommand(IBEContext beContext, NcSmtpClient smtp) : base(beContext, smtp)
        {
        }

        public void ConnectAndAuthenticate()
        {
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

    public class SmtpSendMailCommand : SmtpCommand
    {
        public SmtpSendMailCommand(IBEContext beContext, NcSmtpClient smtp, McPending pending) : base(beContext, smtp)
        {
            PendingSingle = pending;
            PendingSingle.MarkDispached ();
        }

        protected override Event ExecuteCommand ()
        {
            McEmailMessage EmailMessage = McAbstrObject.QueryById<McEmailMessage> (PendingSingle.ItemId);
            McBody body = McBody.QueryById<McBody> (EmailMessage.BodyId);
            MimeMessage mimeMessage = MimeHelpers.LoadMessage (body);
            var attachments = McAttachment.QueryByItemId (EmailMessage);
            if (attachments.Count > 0) {
                MimeHelpers.AddAttachments (mimeMessage, attachments);
            }

            Client.Send (mimeMessage, Cts.Token);
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (
                    BEContext.ProtoControl,
                    NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "SMTPCONNSUC");
        }
    }
}
