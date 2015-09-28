//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using System;
using System.Threading;

namespace NachoCore.SMTP
{
    public class SmtpDisconnectCommand : SmtpCommand
    {
        public SmtpDisconnectCommand (IBEContext beContext, NcSmtpClient smtp) : base (beContext, smtp)
        {
        }

        /// <summary>
        /// We don't need the normal 'ExecuteConnectAndAuthEvent()' functionality, since we don't care
        /// here if we're connected or auth'd, since we're disconnecting. So we override the ExecuteConnectAndAuthEvent
        /// method, and just disconnect.
        /// </summary>
        /// <returns>The connect and auth event.</returns>
        public override Event ExecuteConnectAndAuthEvent ()
        {
            Cts.Token.ThrowIfCancellationRequested ();
            return TryLock (Client.SyncRoot, KLockTimeout, () => {
                Client.Disconnect (false, Cts.Token);
                return Event.Create ((uint)SmEvt.E.Success, "SMTPDISCOSUCC");
            });
        }
    }
}

