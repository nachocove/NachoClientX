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

            // TODO Add handlers for folder deletion, folder rename, etc.

            EventHandler<MessagesArrivedEventArgs> CountChangedMessageHandler = (sender, e) => {
                mailArrived = true;
                done.Cancel ();
            };
<<<<<<< HEAD
            EventHandler<MessageEventArgs> ExpungedMessageHandler = (sender, e) => {
                // Sadly, the Expunged message only passes an ID, not a UID. In the absence of keeping the ID in our DB, we just
                // have to do a sync.
                Log.Info (Log.LOG_IMAP, "Message expunged");
                done.Cancel ();
            };
            EventHandler<MessageFlagsChangedEventArgs> MessageFlagsChangedHandler = (sender, e) => {
                var mkFolder = (ImapFolder) sender;

                McFolder folder = McFolder.QueryByServerId<McFolder> (BEContext.Account.Id, mkFolder.FullName);
                if (!e.UniqueId.HasValue) {
                    Log.Warn (Log.LOG_IMAP, "{0}: flags for message Index {1} have changed to: {2}. No UID passed.", folder.ImapFolderNameRedacted (), e.Index, e.Flags);
                } else {
                    Log.Info (Log.LOG_IMAP, "{0}: flags for message {1} have changed to: {2}.", folder.ImapFolderNameRedacted (), e.UniqueId, e.Flags);
                    McEmailMessage emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, ImapProtoControl.MessageServerId (folder, e.UniqueId.Value));
                    if (null != emailMessage) {
                        if (emailMessage.IsRead != e.Flags.HasFlag (MessageFlags.Seen)) {
                            emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                                var target = (McEmailMessage)record;
                                target.IsRead = e.Flags.HasFlag (MessageFlags.Seen);
                                return true;
                            });
                            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
                        }
                    }
                }
            };

            lock (Client.SyncRoot) {
                mailKitFolder = Client.GetFolder (IdleFolder.ServerId, Cts.Token);
                try {
                    mailKitFolder.MessagesArrived += CountChangedMessageHandler;
                    mailKitFolder.MessageFlagsChanged += MessageFlagsChangedHandler;
                    mailKitFolder.MessageExpunged += ExpungedMessageHandler;

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
=======
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
>>>>>>> master

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
                    mailKitFolder.MessagesArrived -= CountChangedMessageHandler;
                    mailKitFolder.MessageFlagsChanged -= MessageFlagsChangedHandler;
                    mailKitFolder.MessageExpunged -= ExpungedMessageHandler;
                    done.Dispose ();
                }
<<<<<<< HEAD
=======
                return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLEDONE");
            } finally {
                mailKitFolder.MessagesArrived -= messageHandler;
                done.Dispose ();
>>>>>>> master
            }
        }
    }
}
