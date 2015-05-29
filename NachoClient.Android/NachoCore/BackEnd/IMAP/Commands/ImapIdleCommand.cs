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

        public ImapIdleCommand (IBEContext beContext, ImapClient imap, PingKit pingKit) : base (beContext, imap)
        {
            PingKit = pingKit;
        }

        public override void Execute (NcStateMachine sm)
        {
            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });
            NcTask.Run (() => {
                EventHandler<MessagesArrivedEventArgs> messageHandler = (sender, maea) => {
                    done.Cancel ();
                };
                try {
                    if (!Client.IsConnected) {
                        sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReConn, "IMAPSYNCCONN");
                        return;
                    }
                    if (!PingKit.MailKitFolder.IsOpen) {
                        FolderAccess access;
                        lock (Client.SyncRoot) {
                            access = PingKit.MailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
                        }
                        if (FolderAccess.None == access) {
                            sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                            return;
                        }
                    }
                    PingKit.MailKitFolder.MessagesArrived += messageHandler;
                    lock (Client.SyncRoot) {
                        Client.Idle (done.Token, CancellationToken.None);
                        PingKit.MailKitFolder.Status (
                            StatusItems.UidNext |
                            StatusItems.UidValidity, Cts.Token);
                    }
                    sm.PostEvent ((uint)SmEvt.E.Success, "IMAPIDLENEWMAIL");
                } catch (OperationCanceledException) {
                    // Not going to happen until we nix CancellationToken.None.
                    Log.Info (Log.LOG_IMAP, "ImapIdleCommand: Cancelled");
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ImapIdleCommand: Unexpected exception: {0}", ex.ToString ());
                    sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPIDLEHARDX"); 
                } finally {
                    PingKit.MailKitFolder.MessagesArrived -= messageHandler;
                    done.Dispose ();
                }
            }, "ImapIdleCommand");
        }
    }
}
