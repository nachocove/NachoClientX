//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using System.Threading;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoCore.IMAP
{
    public class ImapIdleCommand : ImapCommand
    {
        McFolder IdleFolder;

        public ImapIdleCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
            // TODO Look at https://github.com/jstedfast/MailKit/commit/0ec1a1c26c96193384f4c3aa4a6ce2275bbb2533
            // for more inspiration
            IdleFolder = McFolder.GetDefaultInboxFolder(BEContext.Account.Id);
            NcAssert.NotNull (IdleFolder);
            RedactProtocolLogFunc = RedactProtocolLog;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
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
            mailKitFolder = Client.GetFolder (IdleFolder.ServerId, Cts.Token);
            NcAssert.NotNull (mailKitFolder);
            try {
                mailKitFolder.MessagesArrived += messageHandler;
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
            } finally {
                mailKitFolder.MessagesArrived -= messageHandler;
                done.Dispose ();
            }
        }
    }
}
