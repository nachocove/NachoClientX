﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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

        public override void Execute (NcStateMachine sm)
        {
            // Disconnect is different than the other IMAP commands.  It is run when parking
            // the ProtoControl, which usually happens when shutting down the app.  Because
            // it is run during the shutdown process, it can't block.  But it needs to wait
            // until any other command is done using the ImapClient.  If the ImapClient's lock
            // is available, disconnect it right away.  If the lock is not available, then
            // start a background task that will wait as long as necessary to get the lock.
            // The state machine is not waiting for the command to complete, so there is no
            // need to post an event when done.
            if (Monitor.TryEnter (Client.SyncRoot)) {
                try {
                    Client.Disconnect (true, Cts.Token);
                } finally {
                    Monitor.Exit (Client.SyncRoot);
                }
            } else {
                NcTask.Run (() => {
                    lock (Client.SyncRoot) {
                        Client.Disconnect (true, Cts.Token);
                    }
                }, "ImapDisconnectCommand");
            }
        }
    }
}
