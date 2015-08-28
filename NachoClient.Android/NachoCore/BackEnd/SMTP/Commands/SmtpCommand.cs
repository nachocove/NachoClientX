//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Security;
using MailKit;
using System.IO;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using NachoCore.IMAP;
using NachoCore.Model;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : NcCommand
    {
        const int KAuthRetries = 2;

        public NcSmtpClient Client { get; set; }

        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;

        public SmtpCommand (IBEContext beContext, NcSmtpClient smtpClient) : base (beContext)
        {
            Client = smtpClient;
            RedactProtocolLogFunc = null;
        }

        // MUST be overridden by subclass.
        protected virtual Event ExecuteCommand ()
        {
            NcAssert.True (false);
            return null;
        }

        public override void Cancel ()
        {
            base.Cancel ();
            if (!BEContext.ProtoControl.ForceStopped) {
                try {
                    TryLock (Client.SyncRoot, KLockTimeout);
                } catch (CommandLockTimeOutException ex) {
                    Log.Error (Log.LOG_IMAP, "{0}.Cancel({1}): {2}", this.GetType ().Name, BEContext.Account.Id, ex.Message);
                }
            }
        }

        public virtual Event ExecuteConnectAndAuthEvent ()
        {
            Cts.Token.ThrowIfCancellationRequested ();
            return TryLock (Client.SyncRoot, KLockTimeout, () => {
                try {
                    if (null != Client.MailKitProtocolLogger && null != RedactProtocolLogFunc) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        ConnectAndAuthenticate ();
                    }
                    if (null != Client.MailKitProtocolLogger) {
                        Client.MailKitProtocolLogger.ResetBuffers ();
                    }
                    return ExecuteCommand ();
                } finally {
                    if (null != Client.MailKitProtocolLogger && Client.MailKitProtocolLogger.Enabled ()) {
                        ProtocolLoggerStopAndLog ();
                    }
                }
            });
        }

        public override void Execute (NcStateMachine sm)
        {
            var cmdname = this.GetType ().Name;
            NcTask.Run (() => {
                Event evt;
                Tuple<ResolveAction, NcResult.WhyEnum> action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.None, NcResult.WhyEnum.Unknown);

                Log.Info (Log.LOG_SMTP, "{0}({1}): Started", cmdname, BEContext.Account.Id);
                try {
                    evt = ExecuteConnectAndAuthEvent ();
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    Cts.Token.ThrowIfCancellationRequested ();
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_SMTP, "{0}: OperationCanceledException", cmdname);
                    ResolveAllDeferred ();
                    // No event posted to SM if cancelled.
                    return;
                } catch (SocketException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: SocketException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.InvalidDest);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", AutoDFailureReason.CannotFindServer);
                } catch (ServiceNotConnectedException) {
                    // FIXME - this needs to feed into NcCommStatus, not loop forever.
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotConnectedException", cmdname);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.ReDisc, "SMTPCONN");
                } catch (AuthenticationException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: AuthenticationException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH1");
                } catch (ServiceNotAuthenticatedException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotAuthenticatedException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH2");
                } catch (SmtpProtocolException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpProtocolException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPPROTOEX");
                } catch (SmtpCommandException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpCommandException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPCMDEX");
                } catch (InvalidOperationException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: InvalidOperationException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.ProtocolError);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPHARD1");
                } catch (FormatException ex) {
                    Log.Error (Log.LOG_SMTP, "FormatException: {0}", ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.ProtocolError);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPFORMATHARD");
                } catch (IOException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: IOException: {1}", cmdname, ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPIO");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: Exception : {1}", cmdname, ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPHARD2");
                }

                if (Cts.Token.IsCancellationRequested) {
                    Log.Info (Log.LOG_SMTP, "{0}({1}): Cancelled", cmdname, BEContext.Account.Id);
                    return;
                }
                Log.Info (Log.LOG_SMTP, "{0}({1}): Finished", cmdname, BEContext.Account.Id);
                switch (action.Item1) {
                case ResolveAction.None:
                    break;
                case ResolveAction.DeferAll:
                    ResolveAllDeferred ();
                    break;
                case ResolveAction.FailAll:
                    ResolveAllFailed (action.Item2);
                    break;
                }
                sm.PostEvent (evt);
            }, cmdname);
        }

        public void ConnectAndAuthenticate ()
        {
            ImapDiscoverCommand.guessServiceType (BEContext);

            if (!Client.IsConnected) {
                //client.ClientCertificates = new X509CertificateCollection ();
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, false, Cts.Token);
                Log.Info (Log.LOG_SMTP, "SMTP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
                Cts.Token.ThrowIfCancellationRequested ();
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
                    Cts.Token.ThrowIfCancellationRequested ();
                    try {
                        try {
                            ex = null;
                            Client.Authenticate (username, cred, Cts.Token);
                            break;
                        } catch (SmtpProtocolException e) {
                            Log.Info (Log.LOG_SMTP, "Protocol Error during auth: {0}", e);
                            if (BEContext.ProtocolState.ImapServiceType == McAccount.AccountServiceEnum.iCloud) {
                                // some servers (icloud.com) seem to close the connection on a bad password/username.
                                throw new AuthenticationException (e.Message);
                            } else {
                                throw;
                            }
                        }
                    } catch (AuthenticationException e) {
                        ex = e;
                        Log.Info (Log.LOG_SMTP, "ConnectAndAuthenticate: AuthenticationException: (i={0}) {1}", i, ex.Message);
                        continue;
                    } catch (ServiceNotAuthenticatedException e) {
                        ex = e;
                        Log.Info (Log.LOG_SMTP, "ConnectAndAuthenticate: ServiceNotAuthenticatedException: (i={0}) {1}", i, ex.Message);
                        continue;
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

        protected void ProtocolLoggerStopAndLog ()
        {
            //string ClassName = this.GetType ().Name + " ";
            //Log.Info (Log.LOG_SMTP, "{0}SMTP exchange\n{1}", ClassName, Encoding.UTF8.GetString (Client.MailKitProtocolLogger.GetCombinedBuffer ()));
            Client.MailKitProtocolLogger.Stop ();
        }
    }

    public class SmtpWaitCommand : SmtpCommand
    {
        NcCommand WaitCommand;
        public SmtpWaitCommand (IBEContext dataSource, NcSmtpClient imap, int duration, bool earlyOnECChange) : base (dataSource, imap)
        {
            WaitCommand = new NcWaitCommand (dataSource, duration, earlyOnECChange);
        }
        public override void Execute (NcStateMachine sm)
        {
            WaitCommand.Execute (sm);
        }
        public override void Cancel ()
        {
            WaitCommand.Cancel ();
        }
    }

}
