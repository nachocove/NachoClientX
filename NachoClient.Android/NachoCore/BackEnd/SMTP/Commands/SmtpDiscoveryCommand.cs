//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.IO;
using MailKit.Net.Smtp;

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
            int retryCount = 0;
            while (retryCount++ < KDiscoveryConnectCount) {
                Log.Info (Log.LOG_SMTP, "SmtpDiscoveryCommand: Attempt {0}", retryCount);
                try {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        var authy = new SmtpAuthenticateCommand (BEContext, Client);
                        authy.ConnectAndAuthenticate ();
                    }
                    BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
                    return Event.Create ((uint)SmEvt.E.Success, "SMTPAUTHSUC");
                } catch (NotSupportedException ex) {
                    Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: NotSupportedException: {0}", ex.ToString ());
                    return Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.UiSetServConf, "SMTPDISCCONF");
                } catch (InvalidOperationException ex) {
                    Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: InvalidOperationException: {0}", ex.ToString ());
                    // try again.
                } catch (SmtpProtocolException ex) {
                    Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: SmtpProtocolException: {0}", ex.ToString ());
                    // try again.
                } catch (IOException ex) {
                    Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: IOException: {0}", ex.ToString ());
                    // try again.
                } catch (Exception ex) {
                    Log.Error (Log.LOG_SMTP, "SmtpDiscoveryCommand: {0}", ex);
                    return Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.UiSetServConf, "SMTPAUTHFAILUNDEF");
                }
            }
            if (Client.IsConnected) {
                Client.Disconnect (false, Cts.Token);
            }
            Log.Warn (Log.LOG_SMTP, "SmtpDiscoveryCommand: No more attempts allowed. Fail.", retryCount);
            // if we got here, we can't continue.
            return Event.Create ((uint)SmtpProtoControl.SmtpEvt.E.UiSetServConf, "SMTPDISCHARDFINAL");
        }
    }
}

