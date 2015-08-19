//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace NachoCore.IMAP
{
    public class ImapDisconnectCommand : ImapCommand
    {
        public ImapDisconnectCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            int timeout = BEContext.ProtoControl.ForceStopped ? 1000 : KLockTimeout;
            Event evt;
            try {
                evt = TryLock (Client.SyncRoot, timeout, () => {
                    Client.Disconnect (true);
                    return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOSUC");
                });
            } catch (ImapCommandLockTimeOutException ex) {
                Log.Error (Log.LOG_IMAP, "ImapDisconnectCommand({0}:ImapCommandLockTimeOutException: {1}", BEContext.Account.Id, ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPDISCOTEMP");
            }
            sm.PostEvent (evt);
        }
    }
}
