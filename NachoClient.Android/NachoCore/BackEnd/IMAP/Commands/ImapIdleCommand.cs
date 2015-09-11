﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
        bool ENABLED = true;
        CancellationTokenSource Done { get; set; }

        public ImapIdleCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
            // TODO Look at https://github.com/jstedfast/MailKit/commit/0ec1a1c26c96193384f4c3aa4a6ce2275bbb2533
            // for more inspiration
            IdleFolder = McFolder.GetDefaultInboxFolder(AccountId);
            NcAssert.NotNull (IdleFolder);
            RedactProtocolLogFunc = RedactProtocolLog;
            Done = new CancellationTokenSource ();
            Cts.Token.Register (() => Done.Cancel ());
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return logData;
        }

        private bool mailArrived = false;
        private bool mailDeleted = false;
        private bool needResync = false; // used if something happened, but we don't know what exactly.

        public override void Cancel ()
        {
            Done.Cancel ();
            base.Cancel ();
        }

        protected override Event ExecuteCommand ()
        {
            var mailKitFolder = GetOpenMailkitFolder (IdleFolder);
            if (Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == IdleFolder.Type) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_InboxPingStarted));
            }
            if (ENABLED && Client.Capabilities.HasFlag (ImapCapabilities.Idle) && !IsComcast (BEContext.Server)) {
                IdleIdle(mailKitFolder);
            } else {
                NoopIdle(mailKitFolder);
            }
            Cts.Token.ThrowIfCancellationRequested ();
            if (mailArrived) {
                Log.Info (Log.LOG_IMAP, "{0}: New mail arrived during idle", IdleFolder.ImapFolderNameRedacted ());
            }
            if (mailDeleted) {
                Log.Info (Log.LOG_IMAP, "{0}: Mail Deleted during idle", IdleFolder.ImapFolderNameRedacted ());
            }
            mailKitFolder.Status (
                StatusItems.Count |
                StatusItems.Recent |
                StatusItems.UidValidity |
                StatusItems.UidNext |
                StatusItems.HighestModSeq |
                StatusItems.Unread);
            UpdateImapSetting (mailKitFolder, ref IdleFolder);
            if (mailArrived || mailDeleted || needResync) {
                if (!GetFolderMetaData (ref IdleFolder, mailKitFolder, BEContext.Account.DaysSyncEmailSpan ())) {
                    Log.Error (Log.LOG_IMAP, "{0}: Could not refresh folder metadata", IdleFolder.ImapFolderNameRedacted ());
                }
            }

            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.LastPing = DateTime.UtcNow;
                return true;
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPIDLEDONE");
        }

        /// <summary>
        /// Use the IMAP IDLE command to wait for new things to happen.
        /// </summary>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// command to the server to terminate the Idle gracefully.</param>
        private void IdleIdle (IMailFolder mailKitFolder)
        {
            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                mailArrived = true;
                Done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: Message ID {1} expunged", IdleFolder.ImapFolderNameRedacted (), e.Index);
                mailDeleted = true;
                Done.Cancel ();
            };
            EventHandler<MessageFlagsChangedEventArgs> MessageFlagsChangedHandler = (sender, e) => {
                if (!e.UniqueId.HasValue) {
                    Log.Warn (Log.LOG_IMAP, "{0}: flags for message Index {1} have changed to: {2}. No UID passed.",
                        IdleFolder.ImapFolderNameRedacted (), e.Index, e.Flags);
                } else {
                    Log.Info (Log.LOG_IMAP, "{0}: flags for message {1} have changed to: {2}.",
                        IdleFolder.ImapFolderNameRedacted (), e.UniqueId, e.Flags);
                    McEmailMessage emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (
                        AccountId,
                        ImapProtoControl.MessageServerId (IdleFolder, e.UniqueId.Value));
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

                Client.Idle (Done.Token, Cts.Token);
                Cts.Token.ThrowIfCancellationRequested ();
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged -= MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
            }
        }

        // TODO: Should be tied into power-state
        uint kNoopSleepTime = 20;

        /// <summary>
        /// Servers that do not support Idle need to be polled. We sleep for kNoopSleepTime seconds, and then
        /// Call the Noop command. From RFC 3501:
        ///       Since any command can return a status update as untagged data, the
        ///       NOOP command can be used as a periodic poll for new messages or
        ///       message status updates during a period of inactivity (this is the
        ///       preferred method to do this).  The NOOP command can also be used
        ///       to reset any inactivity autologout timer on the server.
        /// </summary>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        private void NoopIdle (IMailFolder mailKitFolder)
        {
            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.ProtocolState.ImapServiceType) {
                    mailArrived = true;
                } else {
                    needResync = true;
                }
                Done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: Message ID {1} expunged", IdleFolder.ImapFolderNameRedacted (), e.Index);
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.ProtocolState.ImapServiceType) {
                    mailDeleted = true;
                } else {
                    needResync = true;
                }
                Done.Cancel ();
            };

            EventHandler<EventArgs> MessageCountChangedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: message count changed", IdleFolder.ImapFolderNameRedacted ());
                needResync = true;
                Done.Cancel ();
            };

            try {
                mailKitFolder.MessagesArrived += MessagesArrivedHandler;
                mailKitFolder.MessageExpunged += MessageExpungedHandler;
                mailKitFolder.CountChanged += MessageCountChangedHandler;
                var timerSource = CancellationTokenSource.CreateLinkedTokenSource (Done.Token, Cts.Token);
                while (!Cts.Token.IsCancellationRequested) {
                    Log.Info (Log.LOG_IMAP, "ImapIdleCommand: waiting {0}s to call Noop", kNoopSleepTime);
                    var cancelled = timerSource.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(kNoopSleepTime));
                    if (cancelled) {
                        break;
                    }
                    Log.Info (Log.LOG_IMAP, "ImapIdleCommand: Calling Noop");
                    Client.NoOp (Cts.Token);
                }
                Cts.Token.ThrowIfCancellationRequested ();
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
                mailKitFolder.CountChanged -= MessageCountChangedHandler;
            }
        }
    }
}
