//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.IO;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using System.Net.Sockets;
using NachoCore.Model;

namespace NachoCore.SMTP
{
    public class SmtpDiscoveryCommand : SmtpCommand
    {
        const uint KDiscoveryConnectCount = 5;

        public SmtpDiscoveryCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext, smtp)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                Event evt = ExecuteCommand ();
                if (!Cts.Token.IsCancellationRequested) {
                    var protocolState = BEContext.ProtocolState;
                    if (!protocolState.SmtpDiscoveryDone) {
                        protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                            var target = (McProtocolState)record;
                            target.SmtpDiscoveryDone = true;
                            return true;
                        });
                    }
                    sm.PostEvent (evt);
                }
            }, "SmtpDiscoveryCommand");
        }

        protected override Event ExecuteCommand ()
        {
            bool Initial = BEContext.ProtocolState.SmtpDiscoveryDone;
            Log.Info (Log.LOG_SMTP, "{0}({1}): Started", this.GetType ().Name, BEContext.Account.Id);
            var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
            errResult.Message = "Unknown error"; // gets filled in by the various exceptions.
            Event evt;
            try {
                Event evt1 = TryLock (Client.SyncRoot, KLockTimeout, () => {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        ConnectAndAuthenticate ();
                        Cts.Token.ThrowIfCancellationRequested ();
                    }
                    return Event.Create ((uint)SmEvt.E.Success, "SMTPAUTHSUC");
                });
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
                return evt1;
            } catch (OperationCanceledException ex ) {
                ResolveAllDeferred ();
                evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPDISCOCANCEL"); // will be ignored by the caller
                errResult.Message = ex.Message;
            } catch (UriFormatException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: UriFormatException: {0}", ex.Message);
                if (Initial) {
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPURIFAIL", AutoDFailureReason.CannotFindServer);
                } else {
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPURIHARD");
                }
                errResult.Message = ex.Message;
            } catch (SocketException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: SocketException: {0}", ex.Message);
                if (Initial) {
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", AutoDFailureReason.CannotFindServer);
                } else {
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPCONNTEMP");
                }
                errResult.Message = ex.Message;
            } catch (AuthenticationException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: AuthenticationException: {0}", ex.Message);
                evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTHFAIL1");
                errResult.Message = ex.Message;
            } catch (ServiceNotAuthenticatedException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: ServiceNotAuthenticatedException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.AuthFail, "SMTPAUTHFAIL2");
                errResult.Message = ex.Message;
            } catch (InvalidOperationException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: InvalidOperationException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "SMTPINVOPTEMP");
                errResult.Message = ex.Message;
            } catch (SmtpProtocolException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: SmtpProtocolException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "SMTPPROTOEXTEMP");
                errResult.Message = ex.Message;
            } catch (SmtpCommandException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: SmtpCommandException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPCOMMEXTEMP");
                errResult.Message = ex.Message;
            } catch (IOException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: IOException: {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPIOEXTEMP");
                errResult.Message = ex.Message;
            } catch (Exception ex) {
                Log.Error (Log.LOG_SMTP, "SmtpDiscoveryCommand: {0}", ex);
                if (Initial) {
                    evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPSERVFAILUNDEF");
                } else {
                    evt = Event.Create ((uint)SmEvt.E.HardFail, "SMTPUNKHARD");
                }
                errResult.Message = ex.Message;
            } finally {
                Log.Info (Log.LOG_SMTP, "{0}({1}): Finished", this.GetType ().Name, BEContext.Account.Id);
            }
            if (Initial) {
                StatusInd (errResult);
            }
            return evt;
        }
    }
}

