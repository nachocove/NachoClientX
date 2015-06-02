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
    public class ImapIdleCommand : ImapCommand
    {
        PingKit PingKit;

        public ImapIdleCommand (IBEContext beContext, PingKit pingKit) : base (beContext)
        {
            PingKit = pingKit;
        }

        protected override Event ExecuteCommand ()
        {
            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });
            EventHandler<MessagesArrivedEventArgs> messageHandler = (sender, maea) => {
                done.Cancel ();
            };
            try {
                // FIXME - need map from McFolder to MailKit folder.
                if (!Client.Inbox.IsOpen) {
                    FolderAccess access;
                    lock (Client.SyncRoot) {
                        access = Client.Inbox.Open (FolderAccess.ReadOnly, Cts.Token);
                    }
                    if (FolderAccess.None == access) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                    }
                }
                Client.Inbox.MessagesArrived += messageHandler;
                lock (Client.SyncRoot) {
                    Client.Idle (done.Token, CancellationToken.None);
                    Client.Inbox.Status (
                        StatusItems.UidNext |
                        StatusItems.UidValidity, Cts.Token);
                }
                return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLENEWMAIL");
            } catch {
                throw;
            } finally {
                Client.Inbox.MessagesArrived -= messageHandler;
                done.Dispose ();
            }
        }
    }
}
