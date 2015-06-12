//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class ImapAuthenticateCommand : ImapCommand
    {
        public ImapAuthenticateCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public void ConnectAndAuthenticate ()
        {
            if (!Client.IsConnected) {
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                Log.Info (Log.LOG_IMAP, "IMAP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
            }
            if (!Client.IsAuthenticated) {
                // FIXME - add support for OAUTH2.
                Client.AuthenticationMechanisms.Remove ("XOAUTH");
                Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                Log.Info (Log.LOG_IMAP, "IMAP Server capabilities: {0}", Client.Capabilities.ToString ());
            }
            var cap = McProtocolState.FromImapCapabilities (Client.Capabilities);
            if (cap != BEContext.ProtocolState.ImapServerCapabilities) {
                BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.ImapServerCapabilities = cap;
                    return true;
                });
            }
        }

        protected override Event ExecuteCommand ()
        {
            try {
                lock (Client.SyncRoot) {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    ConnectAndAuthenticate ();
                }
                return Event.Create ((uint)SmEvt.E.Success, "IMAPAUTHSUC");
            } catch (NotSupportedException ex) {
                Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPAUTHHARD0");
            }
        }
    }
}
