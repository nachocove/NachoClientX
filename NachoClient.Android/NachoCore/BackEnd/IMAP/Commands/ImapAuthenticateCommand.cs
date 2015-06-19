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
        public ImapAuthenticateCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
            RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
        }

        public void ConnectAndAuthenticate ()
        {
            if (!Client.IsConnected) {
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                Log.Info (Log.LOG_IMAP, "IMAP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
            }
//            RedactProtocolLogFuncDel RestartLog = null;
            if (!Client.IsAuthenticated) {
//                if (Client.MailKitProtocolLogger.Enabled ()) {
//                    RestartLog = Client.MailKitProtocolLogger.RedactProtocolLogFunc;
//                    ProtocolLoggerStopAndPostTelemetry ();
//                }
                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    // FIXME - be exhaustive w/Remove when we know we MUST use an auth mechanism.
                    Client.AuthenticationMechanisms.Remove ("LOGIN");
                    Client.AuthenticationMechanisms.Remove ("PLAIN");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetAccessToken (), Cts.Token);
                } else {
                    Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                }
                Log.Info (Log.LOG_IMAP, "IMAP Server capabilities: {0}", Client.Capabilities.ToString ());
            }
//            if (null != RestartLog) {
//                Client.MailKitProtocolLogger.Start (RestartLog);
//            }
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
                if (Client.IsConnected) {
                    Client.Disconnect (false, Cts.Token);
                }
                ConnectAndAuthenticate ();
                return Event.Create ((uint)SmEvt.E.Success, "IMAPAUTHSUC");
            } catch (NotSupportedException ex) {
                Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPAUTHHARD0");
            }
        }
    }
}
