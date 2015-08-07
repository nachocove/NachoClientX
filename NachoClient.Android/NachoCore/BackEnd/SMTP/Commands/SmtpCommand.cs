//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Security;
using MailKit;
using System.IO;
using System.Net.Sockets;
using MailKit.Net.Smtp;

namespace NachoCore.SMTP
{
    public abstract class SmtpCommand : NcCommand
    {
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
            lock (Client.SyncRoot) {
            }
        }

        public virtual Event ExecuteConnectAndAuthEvent ()
        {
            lock (Client.SyncRoot) {
                try {
                    if (null != Client.MailKitProtocolLogger && null != RedactProtocolLogFunc) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        var authy = new SmtpAuthenticateCommand (BEContext, Client);
                        authy.ConnectAndAuthenticate ();
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
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            var cmdname = this.GetType ().Name;
            NcTask.Run (() => {
                Log.Info (Log.LOG_SMTP, "{0}({1}): Started", cmdname, BEContext.Account.Id);
                try {
                    Event evt = ExecuteConnectAndAuthEvent ();
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    sm.PostEvent (evt);
                } catch (SocketException ex) {
                    Log.Error (Log.LOG_IMAP, "{0}: SocketException: {1}", cmdname, ex.Message);
                    ResolveAllFailed (NcResult.WhyEnum.InvalidDest);
                    var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
                    errResult.Message = ex.Message;
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", AutoDFailureReason.CannotFindServer);
                } catch (OperationCanceledException) {
                    Log.Info (Log.LOG_SMTP, "{0}: OperationCanceledException", cmdname);
                    ResolveAllDeferred ();
                    // No event posted to SM if cancelled.
                } catch (ServiceNotConnectedException) {
                    // FIXME - this needs to feed into NcCommStatus, not loop forever.
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotConnectedException", cmdname);
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.ReDisc, "SMTPCONN");
                } catch (AuthenticationException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: AuthenticationException: {1}", cmdname, ex.Message);
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH1");
                } catch (ServiceNotAuthenticatedException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: ServiceNotAuthenticatedException: {1}", cmdname, ex.Message);
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTH2");
                } catch (SmtpProtocolException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpProtocolException: {1}", cmdname, ex.Message);
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "SMTPPROTOEX");
                } catch (SmtpCommandException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: SmtpCommandException: {1}", cmdname, ex.Message);
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "SMTPCMDEX");
                } catch (InvalidOperationException ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: InvalidOperationException: {1}", cmdname, ex.Message);
                    ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPHARD1");
                } catch (FormatException ex) {
                    Log.Error (Log.LOG_SMTP, "FormatException: {0}", ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPFORMATHARD");
                } catch (IOException ex) {
                    Log.Info (Log.LOG_SMTP, "{0}: IOException: {1}", cmdname, ex.ToString ());
                    ResolveAllDeferred ();
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "SMTPIO");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SMTP, "{0}: Exception : {1}", cmdname, ex.ToString ());
                    ResolveAllFailed (NcResult.WhyEnum.Unknown);
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "SMTPHARD2");
                } finally {
                    Log.Info (Log.LOG_SMTP, "{0}({1}): Finished", cmdname, BEContext.Account.Id);
                }
            }, cmdname);
        }

        protected void ProtocolLoggerStopAndLog ()
        {
            //string ClassName = this.GetType ().Name + " ";
            //Log.Info (Log.LOG_SMTP, "{0}SMTP exchange\n{1}", ClassName, Encoding.UTF8.GetString (Client.MailKitProtocolLogger.GetCombinedBuffer ()));
            Client.MailKitProtocolLogger.Stop ();
        }
    }
}
