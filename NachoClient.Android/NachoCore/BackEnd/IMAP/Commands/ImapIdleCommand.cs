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
        public ImapIdleCommand (IBEContext beContext) : base (beContext)
        {
        }

        protected override Event ExecuteCommand ()
        {
            IMailFolder mailKitFolder = Client.Inbox;

            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });
            EventHandler<MessagesArrivedEventArgs> messageHandler = (sender, maea) => {
                done.Cancel ();
            };
            try {
                if (!mailKitFolder.IsOpen) {
                    FolderAccess access;
                    lock (Client.SyncRoot) {
                        access = mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
                    }
                    if (FolderAccess.None == access) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                    }
                }
                mailKitFolder.MessagesArrived += messageHandler;
                lock (Client.SyncRoot) {
                    Client.Idle (done.Token, CancellationToken.None);
                    if (!Cts.IsCancellationRequested) {
                        mailKitFolder.Status (
                            StatusItems.UidNext |
                            StatusItems.UidValidity, Cts.Token);
                    }
                }
                var protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.LastPing = DateTime.UtcNow;
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLENEWMAIL");
            } catch {
                throw;
            } finally {
                mailKitFolder.MessagesArrived -= messageHandler;
                done.Dispose ();
            }
        }
    }
}
