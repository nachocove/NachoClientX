//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;

namespace NachoCore.IMAP
{
    public class ImapDisconnectCommand : ImapCommand
    {
        public ImapDisconnectCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
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
                return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOSUCC");
            });
        }
    }
}
