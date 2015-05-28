//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.IMAP
{
    public class ImapAuthenticateCommand : ImapCommand
    {
        public ImapAuthenticateCommand (IBEContext beContext, ImapClient imap) : base (beContext, imap)
        {
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                try {
                    lock (Client.SyncRoot) {
                        if (Client.IsConnected) {
                            Client.Disconnect (false, Cts.Token);
                        }
                        Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                        // FIXME - add support for OAUTH2.
                        Client.AuthenticationMechanisms.Remove ("XOAUTH");
                        Client.AuthenticationMechanisms.Remove ("XOAUTH2");
                        Client.Authenticate (BEContext.Cred.Username, BEContext.Cred.GetPassword (), Cts.Token);
                    }
                    sm.PostEvent ((uint)SmEvt.E.Success, "IMAPAUTHSUC");
                } catch (InvalidOperationException ex) {
                    Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: InvalidOperationException: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPAUTHTEMP0");
                } catch (IOException) {
                    sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPAUTHTEMP1");
                } catch (AuthenticationException) {
                    sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL");
                } catch (NotSupportedException ex) {
                    Log.Info (Log.LOG_IMAP, "ImapAuthenticateCommand: NotSupportedException: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPAUTHHARD0");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ImapAuthenticateCommand: Unexpected exception: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPAUTHHARDX");
                }
            }, "ImapAuthenticateCommand");
        }
    }
}
