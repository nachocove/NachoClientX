//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Security;
using MailKit;
using System.IO;
using System.Net.Sockets;

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

        public Event ExecuteConnectAndAuthEvent ()
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
            NcTask.Run (() => {
                try {
                    Event evt = ExecuteConnectAndAuthEvent ();
                    // In the no-exception case, ExecuteCommand is resolving McPending.
                    sm.PostEvent (evt);
                } catch (SocketException ex) {
                    Log.Error (Log.LOG_IMAP, "SocketException: {0}", ex.Message);
                    ResolveAllFailed (NcResult.WhyEnum.InvalidDest);
                    var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
                    errResult.Message = ex.Message;
                    sm.PostEvent ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", AutoDFailureReason.CannotFindServer);
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

        protected void ProtocolLoggerStopAndLog ()
        {
            //string ClassName = this.GetType ().Name + " ";
            //Log.Info (Log.LOG_SMTP, "{0}SMTP exchange\n{1}", ClassName, Encoding.UTF8.GetString (Client.MailKitProtocolLogger.GetCombinedBuffer ()));
            Client.MailKitProtocolLogger.Stop ();
        }
    }
}
