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
        bool ENABLED = true;

        public ImapIdleCommand (IBEContext beContext, NcImapClient imap, McFolder folder) : base (beContext, imap)
        {
            // TODO Look at https://github.com/jstedfast/MailKit/commit/0ec1a1c26c96193384f4c3aa4a6ce2275bbb2533
            // for more inspiration
            IdleFolder = folder;
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
            if (ENABLED && Client.Capabilities.HasFlag (ImapCapabilities.Idle) && !IsComcast (BEContext.Server)) {
                IdleIdle(mailKitFolder, done);
            } else {
                NoopIdle(mailKitFolder, done);
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
                GetFolderMetaData (ref IdleFolder, mailKitFolder, BEContext.Account.DaysSyncEmailSpan ());
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
        /// <param name="done">Done cancellation, i.e. if cancelled, will send a 'Done'
        /// command to the server to terminate the Idle gracefully.</param>
        private void IdleIdle (IMailFolder mailKitFolder, CancellationTokenSource done)
        {
            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                mailArrived = true;
                done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: Message ID {1} expunged", IdleFolder.ImapFolderNameRedacted (), e.Index);
                mailDeleted = true;
                done.Cancel ();
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

                TimeSpan timeout;
                if (BEContext.ProtocolState.ImapServiceType == McAccount.AccountServiceEnum.GoogleDefault) {
                    // https://github.com/jstedfast/MailKit/issues/276#issuecomment-168759657
                    // IMAP servers are supposed to keep the connection open for at least 30 minutes with no activity from the client, 
                    // but I've found that Google Mail will drop connections after a little under 10, so my recommendation is that you
                    // cancel the doneToken within roughly 9-10 minutes and then loop back to calling Idle() again.
                    //var timeout = new TimeSpan(0, 9, 0);
                    timeout = new TimeSpan(0, 9, 0);
                } else {
                    timeout = new TimeSpan(0, 30, 0);
                }
                Log.Info (Log.LOG_IMAP, "Setting IDLE timeout to {0} on folder {1}", timeout, IdleFolder.ImapFolderNameRedacted ());
                done.CancelAfter (timeout);
                NcTimeStamp.Add ("Before Idle");
                Client.Idle (done.Token, CancellationToken.None);
                NcTimeStamp.Add ("After Idle");
                NcTimeStamp.Dump ();
                Cts.Token.ThrowIfCancellationRequested ();
            } finally {
                mailKitFolder.MessagesArrived -= MessagesArrivedHandler;
                mailKitFolder.MessageFlagsChanged -= MessageFlagsChangedHandler;
                mailKitFolder.MessageExpunged -= MessageExpungedHandler;
            }
        }

        /// <summary>
        /// Number of seconds to sleep between NOOP calls in Foreground
        /// </summary>
        const uint kNoopSleepTimeFG = 20;

        /// <summary>
        /// Number of seconds to sleep between NOOP calls in Background
        /// </summary>
        const uint kNoopSleepTimeBG = 300;

        /// <summary>
        /// Get the Noop Sleep time, depending on ExecutionContext
        /// </summary>
        /// <value>The k noop sleep time.</value>
        uint kNoopSleepTime {
            get {
                return NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Foreground ? kNoopSleepTimeFG : kNoopSleepTimeBG;
            }
        }

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
        /// <param name="done">Done.</param>
        private void NoopIdle (IMailFolder mailKitFolder, CancellationTokenSource done)
        {
            EventHandler<MessagesArrivedEventArgs> MessagesArrivedHandler = (sender, e) => {
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.ProtocolState.ImapServiceType) {
                    mailArrived = true;
                } else {
                    needResync = true;
                }
                done.Cancel ();
            };
            EventHandler<MessageEventArgs> MessageExpungedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: Message ID {1} expunged", IdleFolder.ImapFolderNameRedacted (), e.Index);
                // Yahoo doesn't send EXPUNGED untagged responses, so we can't trust anything. Just go back and resync.
                if (McAccount.AccountServiceEnum.Yahoo != BEContext.ProtocolState.ImapServiceType) {
                    mailDeleted = true;
                } else {
                    needResync = true;
                }
                done.Cancel ();
            };

            EventHandler<EventArgs> MessageCountChangedHandler = (sender, e) => {
                Log.Info (Log.LOG_IMAP, "{0}: message count changed", IdleFolder.ImapFolderNameRedacted ());
                needResync = true;
                done.Cancel ();
            };

            try {
                mailKitFolder.MessagesArrived += MessagesArrivedHandler;
                mailKitFolder.MessageExpunged += MessageExpungedHandler;
                mailKitFolder.CountChanged += MessageCountChangedHandler;
                while (!Cts.Token.IsCancellationRequested) {
                    Log.Info (Log.LOG_IMAP, "ImapIdleCommand: waiting {0}s to call Noop on folder {1}", kNoopSleepTime, IdleFolder.ImapFolderNameRedacted ());
                    var cancelled = done.Token.WaitHandle.WaitOne(TimeSpan.FromSeconds(kNoopSleepTime));
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
