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
using NachoPlatform;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : NcCommand
    {
        public NcSmtpClient Client { get; set; }

        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;
        protected bool DontReportCommResult { get; set; }
        public INcCommStatus NcCommStatusSingleton { set; get; }

        public SmtpCommand (IBEContext beContext, NcSmtpClient smtpClient) : base (beContext)
        {
            Client = smtpClient;
            RedactProtocolLogFunc = null;
            NcCommStatusSingleton = NcCommStatus.Instance;
            DontReportCommResult = false;
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
            if (!BEContext.ProtoControl.Cts.IsCancellationRequested) {
                try {
                    TryLock (Client.SyncRoot, KLockTimeout);
                } catch (CommandLockTimeOutException ex) {
                    Log.Error (Log.LOG_IMAP, "{0}.Cancel({1}): {2}", this.GetType ().Name, AccountId, ex.Message);
                    Client.DOA = true;
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
                bool serverFailedGenerally = false;

                Log.Info (Log.LOG_SMTP, "{0}({1}): Started", cmdname, AccountId);
                try {
                    evt = ExecuteConnectAndAuthEvent ();
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    Cts.Token.ThrowIfCancellationRequested ();
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_SMTP, "{0}: OperationCanceledException", cmdname);
                    ResolveAllDeferred ();
                    // No event posted to SM if cancelled.
                    return;
                } catch (KeychainItemNotFoundException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: KeychainItemNotFoundException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPKEYCHFAIL");
                } catch (CommandLockTimeOutException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: CommandLockTimeOutException: {0}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPLOKTIME");
                    Client.DOA = true;
                } catch (SocketException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: SocketException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.InvalidDest);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", BackEnd.AutoDFailureReasonEnum.CannotFindServer);
                    serverFailedGenerally = true;
                } catch (ServiceNotConnectedException) {
                    // FIXME - this needs to feed into NcCommStatus, not loop forever.
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotConnectedException", cmdname);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.ReDisc, "SMTPCONN");
                } catch (AuthenticationException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: AuthenticationException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    if (!HasPasswordChanged ()) {
                        evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH1");
                    } else {
                        // credential was updated while we were running the command. Just try again.
                        evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPAUTH1TEMP");
                    }
                } catch (ServiceNotAuthenticatedException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotAuthenticatedException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    if (!HasPasswordChanged ()) {
                        evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH2");
                    } else {
                        // credential was updated while we were running the command. Just try again.
                        evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPAUTH2TEMP");
                    }
                } catch (SmtpProtocolException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpProtocolException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPPROTOEX");
                    serverFailedGenerally = true;
                } catch (SmtpCommandException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpCommandException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPCMDEX");
                    serverFailedGenerally = true;
                } catch (InvalidOperationException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: InvalidOperationException: {1}", cmdname, ex.Message);
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.ProtocolError);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPHARD1");
                    serverFailedGenerally = true;
                } catch (FormatException ex) {
                    Log.Error (Log.LOG_SMTP, "FormatException: {0}", ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.ProtocolError);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPFORMATHARD");
                } catch (IOException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: IOException: {1}", cmdname, ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPIO");
                    serverFailedGenerally = true;
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: Exception : {1}", cmdname, ex.ToString ());
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.Unknown);
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPHARD2");
                    serverFailedGenerally = true;
                } finally {
                    Log.Info (Log.LOG_SMTP, "{0}({1}): Finished (failed {2})", cmdname, AccountId, serverFailedGenerally);
                }

                if (Cts.Token.IsCancellationRequested) {
                    Log.Info (Log.LOG_SMTP, "{0}({1}): Cancelled", cmdname, AccountId);
                    return;
                }
                ReportCommResult (BEContext.Server.Host, serverFailedGenerally);
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

        protected void ReportCommResult (string host, bool didFailGenerally)
        {
            if (!DontReportCommResult) {
                NcCommStatusSingleton.ReportCommResult (BEContext.Account.Id, McAccount.AccountCapabilityEnum.EmailSender, didFailGenerally);
            }
        }

        public void ConnectAndAuthenticate ()
        {
            ImapDiscoverCommand.guessServiceType (BEContext);

            if (!Client.IsConnected) {
                //client.ClientCertificates = new X509CertificateCollection ();
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, false, Cts.Token);
                Cts.Token.ThrowIfCancellationRequested ();
            }
            if (!Client.IsAuthenticated) {
                ImapDiscoverCommand.possiblyFixUsername (BEContext);
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

                Cts.Token.ThrowIfCancellationRequested ();
                try {
                    BEContext.Account.LogHashedPassword (Log.LOG_SMTP, "ConnectAndAuthenticate", cred);
                    Client.Authenticate (username, cred, Cts.Token);
                } catch (SmtpProtocolException e) {
                    Log.Info (Log.LOG_SMTP, "Protocol Error during auth: {0}", e);
                    if (BEContext.ProtocolState.ImapServiceType == McAccount.AccountServiceEnum.iCloud) {
                        // some servers (icloud.com) seem to close the connection on a bad password/username.
                        throw new AuthenticationException (e.Message);
                    } else {
                        throw;
                    }
                }

                Log.Info (Log.LOG_SMTP, "SMTP Server {0}:{1} capabilities: {2}", BEContext.Server.Host, BEContext.Server.Port, Client.Capabilities.ToString ());
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
