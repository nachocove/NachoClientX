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
using NachoCore.ActiveSync;

namespace NachoCore.IMAP
{
    public class ImapIdleCommand : ImapCommand
    {
        McFolder IdleFolder;

        public ImapIdleCommand (IBEContext beContext) : base (beContext)
        {
            IdleFolder = McFolder.GetDefaultInboxFolder(BEContext.Account.Id);
            NcAssert.NotNull (IdleFolder);
        }

        protected override Event ExecuteCommand ()
        {
            IMailFolder mailKitFolder;
            bool mailArrived = false;
            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });
            EventHandler<MessagesArrivedEventArgs> messageHandler = (sender, maea) => {
                mailArrived = true;
                done.Cancel ();
            };
            lock (Client.SyncRoot) {
                mailKitFolder = Client.GetFolder (IdleFolder.ServerId, Cts.Token);
                NcAssert.NotNull (mailKitFolder);
            }
            try {
                mailKitFolder.MessagesArrived += messageHandler;
                lock (Client.SyncRoot) {
                    if (FolderAccess.None == mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token)) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                    }
                    if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == IdleFolder.Type) {
                        BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_InboxPingStarted));
                    }
                    Client.Idle (done.Token, CancellationToken.None);
                    Cts.Token.ThrowIfCancellationRequested ();
                    mailKitFolder.Close (false, Cts.Token);
                    StatusItems statusItems =
                        StatusItems.UidNext |
                        StatusItems.UidValidity |
                        StatusItems.HighestModSeq;
                    mailKitFolder.Status (statusItems, Cts.Token);
                }
                UpdateImapSetting (mailKitFolder, IdleFolder);

                var protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.LastPing = DateTime.UtcNow;
                    return true;
                });
                if (mailArrived) {
                    Log.Info (Log.LOG_IMAP, "New mail arrived during idle");
                }
                return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLEDONE");
            } catch {
                throw;
            } finally {
                mailKitFolder.MessagesArrived -= messageHandler;
                done.Dispose ();
            }
        }
    }
}
