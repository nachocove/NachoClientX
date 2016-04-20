//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.SMTP
{
    public class SmtpAuthenticateCommand : SmtpCommand
    {
        public SmtpAuthenticateCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext, smtp)
        {
        }

        public override Event ExecuteConnectAndAuthEvent ()
        {
            return TryLock (Client.SyncRoot, KLockTimeout, () => {
                try {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    return base.ExecuteConnectAndAuthEvent ();
                } catch (NotSupportedException ex) {
                    Log.Info (Log.LOG_SMTP, "SmtpAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                    return Event.Create ((uint)SmEvt.E.HardFail, "SMTPAUTHHARD0");
                }
            });
        }

        protected override Event ExecuteCommand ()
        {
            return Event.Create ((uint)SmEvt.E.Success, "SMTPAUTHSUC");
        }
    }

}

