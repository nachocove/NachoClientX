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
            bool mailDeleted = false;
            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });

            // TODO Add handlers for folder deletion, folder rename, etc.

            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                mailArrived = true;
                done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                mailDeleted = true;
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

            mailKitFolder = Client.GetFolder (IdleFolder.ServerId, Cts.Token);
            try {
                mailKitFolder.MessagesArrived += MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged += MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged += MessageExpungedHandler;

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
                            StatusItems.Count |
                            StatusItems.Recent |
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
                if (mailDeleted) {
                    Log.Info (Log.LOG_IMAP, "Mail Deleted during idle");
                }
                return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLEDONE");
            } catch {
                throw;
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged -= MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
                done.Dispose ();
            }
        }
    }
}
