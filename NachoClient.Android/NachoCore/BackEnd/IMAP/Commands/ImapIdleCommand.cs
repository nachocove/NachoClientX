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

        private bool mailArrived = false;
        private bool mailDeleted = false;
        private bool needResync = false; // used if something happened, but we don't know what exactly.

        protected override Event ExecuteCommand ()
        {
            var done = CancellationTokenSource.CreateLinkedTokenSource (new [] { Cts.Token });
            var mailKitFolder = GetOpenMailkitFolder (IdleFolder);
            if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == IdleFolder.Type) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_InboxPingStarted));
            }
            if (Client.Capabilities.HasFlag (ImapCapabilities.Idle)) {
                IdleIdle(mailKitFolder, done);
            } else {
                NoopIdle(mailKitFolder, done);
            }
            Cts.Token.ThrowIfCancellationRequested ();
            mailKitFolder.Close (false, Cts.Token);
            StatusItems statusItems =
                StatusItems.UidNext |
                StatusItems.UidValidity |
                StatusItems.HighestModSeq;
            mailKitFolder.Status (statusItems, Cts.Token);
            UpdateImapSetting (mailKitFolder, ref IdleFolder);

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
            if (mailArrived || mailDeleted || needResync) {
                mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
                if (!ImapSyncCommand.GetFolderMetaData (ref IdleFolder, mailKitFolder, BEContext.Account.DaysSyncEmailSpan ())) {
                    Log.Error (Log.LOG_IMAP, "Could not refresh folder metadata");
                }
            }
            return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLEDONE");
        }

        private void IdleIdle (IMailFolder mailKitFolder, CancellationTokenSource done)
        {
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

            try {
                mailKitFolder.MessagesArrived += MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged += MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged += MessageExpungedHandler;

                Client.Idle (done.Token, CancellationToken.None);
                Cts.Token.ThrowIfCancellationRequested ();
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged -= MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
            }
        }

        // TODO: Should be tied into power-state
        uint kNoopSleepTime = 20;

        private void NoopIdle (IMailFolder mailKitFolder, CancellationTokenSource done)
        {
            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.Account.AccountService) {
                    mailArrived = true;
                } else {
                    needResync = true;
                }
                done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.Account.AccountService) {
                    mailDeleted = true;
                } else {
                    needResync = true;
                }
                done.Cancel ();
            };

            try {
                mailKitFolder.MessagesArrived += MessagesArrivedHandler;
                mailKitFolder.MessageExpunged += MessageExpungedHandler;

                while (!Cts.Token.IsCancellationRequested) {
                    var cancelled = done.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(kNoopSleepTime));
                    if (cancelled) {
                        break;
                    }
                    Client.NoOp (Cts.Token);
                }
                Cts.Token.ThrowIfCancellationRequested ();
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
            }
        }
    }
}
