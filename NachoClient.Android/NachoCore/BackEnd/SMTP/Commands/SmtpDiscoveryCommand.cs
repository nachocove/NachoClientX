//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.IO;
using MailKit.Net.Smtp;
using MailKit.Security;
using MailKit;
using System.Net.Sockets;

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
            Event evt = ExecuteCommand ();
            sm.PostEvent (evt);
        }

        protected override Event ExecuteCommand ()
        {
            var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
            errResult.Message = "Unknown error"; // gets filled in by the various exceptions.
            Event evt;
            try {
                lock (Client.SyncRoot) {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        var authy = new SmtpAuthenticateCommand (BEContext, Client);
                        authy.ConnectAndAuthenticate ();
                    }
                }
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
                return Event.Create ((uint)SmEvt.E.Success, "SMTPAUTHSUC");
            } catch (UriFormatException ex) {
                Log.Error (Log.LOG_SMTP, "SmtpDiscoveryCommand: UriFormatException: {0}", ex.Message);
                evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL2", AutoDFailureReason.CannotFindServer);
                errResult.Message = ex.Message;
            } catch (SocketException ex) {
                Log.Error (Log.LOG_SMTP, "SmtpDiscoveryCommand: SocketException: {0}", ex.Message);
                evt = Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPCONNFAIL", AutoDFailureReason.CannotFindServer);
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
                Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: InvalidOperationException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "SMTPINVOPTEMP");
                errResult.Message = ex.Message;
            } catch (SmtpProtocolException ex) {
                Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: SmtpProtocolException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "SMTPPROTOEXTEMP");
                errResult.Message = ex.Message;
            } catch (SmtpCommandException ex) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: SmtpCommandException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "SMTPCOMMEXTEMP");
                errResult.Message = ex.Message;
            } catch (IOException ex) {
                Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: IOException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "SMTPIOEXTEMP");
                errResult.Message = ex.Message;
            } catch (Exception ex) {
                Log.Error (Log.LOG_SMTP, "SmtpDiscoveryCommand: {0}", ex);
                evt =  Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.GetServConf, "SMTPSERVFAILUNDEF");
                errResult.Message = ex.Message;
            }
            StatusInd (errResult);
            return evt;
        }
    }
}

