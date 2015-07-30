//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using MailKit.Security;
using NachoClient.Build;

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
            // Redaction is done in the base class, since it's more complicated than just string replacement
            return logData;
        }

        public void ConnectAndAuthenticate ()
        {
            if (!Client.IsConnected) {
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                Log.Info (Log.LOG_IMAP, "IMAP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
                var capUnauth = McProtocolState.FromImapCapabilities (Client.Capabilities);

                if (capUnauth != BEContext.ProtocolState.ImapServerCapabilities) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServerCapabilitiesUnAuth = capUnauth;
                        return true;
                    });
                }
            }
            if (!Client.IsAuthenticated) {
                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    // FIXME - be exhaustive w/Remove when we know we MUST use an auth mechanism.
                    Client.AuthenticationMechanisms.Remove ("LOGIN");
                    Client.AuthenticationMechanisms.Remove ("PLAIN");
                    Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetAccessToken (), Cts.Token);
                } else {
                    Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                    try {
                        Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                    } catch (ImapProtocolException ex) {
                        Log.Info (Log.LOG_IMAP, "Protocol Error during auth: {0}", ex);
                        // some servers (icloud.com) seem to close the connection on a bad password/username.
                        throw new AuthenticationException ();
                    }
                }
                Log.Info (Log.LOG_IMAP, "IMAP Server capabilities: {0}", Client.Capabilities.ToString ());
                var capAuth = McProtocolState.FromImapCapabilities (Client.Capabilities);
                if (capAuth != BEContext.ProtocolState.ImapServerCapabilities) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServerCapabilities = capAuth;
                        return true;
                    });
                }

                ImapImplementation ourId = new ImapImplementation () {
                    Name = "Nacho Mail",
                    Version = string.Format ("{0}:{1}", BuildInfo.Version, BuildInfo.BuildNumber),
                    ReleaseDate = BuildInfo.Time,
                    SupportUrl = "https://support.nachocove.com/",
                    Vendor = "Nacho Cove, Inc",
                    OS = NachoPlatform.Device.Instance.BaseOs ().ToString (),
                    OSVersion = NachoPlatform.Device.Instance.Os (),
                };
                Log.Info (Log.LOG_IMAP, "Our Id: {0}", dumpImapImplementation(ourId));
                var serverId = Client.Identify (ourId, Cts.Token);
                Log.Info (Log.LOG_IMAP, "Server ID: {0}", dumpImapImplementation (serverId));
            }
        }

        private string dumpImapImplementation (ImapImplementation imapId)
        {
            return string.Join (", ", imapId.Properties);
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
