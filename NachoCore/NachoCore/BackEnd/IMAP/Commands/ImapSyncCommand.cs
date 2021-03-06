﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using NachoCore.Utils;
using MimeKit;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using System.Text;
using MailKit.Search;
using System.Threading;

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapCommand
    {
        SyncKit Synckit;
        private const int PreviewSizeBytes = 500;
        private const int PreviewHtmlMultiplier = 4;
        private const string KImapSyncOpenTiming = "ImapSyncCommand.OpenOnly";
        private const string KImapQuickSyncTiming = "ImapSyncCommand.QuickSync";
        private const string KImapSyncTiming = "ImapSyncCommand.Sync";
        private const string KImapFetchTiming = "ImapSyncCommand.Summary";
        private const string KImapPreviewGeneration = "ImapSyncCommand.Preview";
        private const string KImapFetchPartialBody = "ImapSyncCommand.PartialBody";
        private const string KImapFetchHeaders = "ImapSyncCommand.Fetchheaders";

        public class MailSummary
        {
            public MessageSummary imapSummary { get; set; }

            public string preview { get; set; }
        }

        public ImapSyncCommand (IBEContext beContext, SyncKit syncKit) : base (beContext)
        {
            Synckit = syncKit;
            PendingSingle = Synckit.PendingSingle;
            Synckit.PendingSingle = null;
            if (null != PendingSingle) {
                PendingSingle.MarkDispatched ();
            }
        }

        protected override Event ExecuteCommand ()
        {
            NcImapFolder mailKitFolder;

            Log.Info (Log.LOG_IMAP, "Processing {0}", Synckit.ToString ());
            Cts.Token.ThrowIfCancellationRequested ();

            var timespan = BEContext.Account.DaysSyncEmailSpan ();
            mailKitFolder = GetOpenMailkitFolder (Synckit.Folder);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN2");
            }
            if (UInt32.MinValue != Synckit.Folder.ImapUidValidity &&
                Synckit.Folder.ImapUidValidity != mailKitFolder.UidValidity) {
                return Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPSYNCUIDINVAL");
            }

            var changed = UpdateImapSetting (mailKitFolder, ref Synckit.Folder);
            if (changed) {
                // HACK: Ignore strategy and do a FastSync.
                Synckit = new SyncKit (Synckit.Folder, Synckit.PendingSingle);
            }

            Event evt;
            NcCapture cap;
            changed = false;
            switch (Synckit.Method) {
            case SyncKit.MethodEnum.Sync:
                NcCapture.AddKind (KImapSyncTiming);
                cap = NcCapture.CreateAndStart (KImapSyncTiming);
                evt = RegularSync (mailKitFolder, out changed);
                break;

            case SyncKit.MethodEnum.FastSync:
                NcCapture.AddKind (KImapQuickSyncTiming);
                cap = NcCapture.CreateAndStart (KImapQuickSyncTiming);
                evt = FastSync (mailKitFolder, timespan, out changed);
                break;

            default:
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDCASE");
            }

            Finish (changed);
            imapProtoControl.Strategy.ResolveOneSync (PendingSingle, Synckit.Folder);
            PendingSingle = null; // we resolved it.
            cap.Dispose ();
            return evt;
        }

        /// <summary>
        /// Regular sync.
        /// </summary>
        /// <returns>The sync.</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="changed">Changed.</param>
        Event RegularSync (NcImapFolder mailKitFolder, out bool changed)
        {
            changed = false;
            return syncFolder (mailKitFolder, ref changed);
        }

        /// <summary>
        /// A sort of 'macro' method. This assumes that the Folder Metadata is out of date (or has never been retrieved)
        /// so we do that first. Then we call back into strategy to let it decide what it wants us to do, and finally
        /// we do it (if there's something to do).
        /// </summary>
        /// <returns>The Event to post</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="timespan">Timespan.</param>
        /// <param name="changed">Has anything changed?</param>
        Event FastSync (NcImapFolder mailKitFolder, TimeSpan timespan, out bool changed)
        {
            changed = GetFolderMetaData (ref Synckit.Folder, mailKitFolder, timespan);
            Event evt;
            var protocolState = BEContext.ProtocolState;
            if (imapProtoControl.Strategy.FillInFastSyncKit (ref protocolState, ref Synckit, AccountId)) {
                evt = syncFolder (mailKitFolder, ref changed);
                changed = true;
            } else {
                // TODO: Need to figure out how strategy knows when to stop trying quicksync.
                // Currently, all Strategy does is create the QuickSync SyncKit and throws it over the fence.
                // It doesn't know if there is in fact anything to do. At this point only the Sync command,
                // i.e. this code you're looking at, knows we're done. Needs more thought, so leaving the hack
                // in here for now.
                if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.QuickSync) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.Wait, "IMAPSYNCQKNONE", 60);
                } else {
                    evt = Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCQKNONE");
                }
            }
            return evt;
        }

        /// <summary>
        /// Syncs a folder.
        /// </summary>
        /// <description>
        /// it is important that we upload messages first, because we want to immediately sync them back down
        /// to get any server-side values that might have gotten set (ConversationId, for example, if any).
        /// </description>
        /// <returns>The folder.</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="changed">Has anything changed?</param>
        private Event syncFolder (NcImapFolder mailKitFolder, ref bool changed)
        {
            // start by uploading messages
            if (null != Synckit.UploadMessages && Synckit.UploadMessages.Any ()) {
                var uidSet = new UniqueIdSet ();
                // need to reopen the folder read-write so we can upload the message
                mailKitFolder = GetOpenMailkitFolder (Synckit.Folder, FolderAccess.ReadWrite);
                foreach (var messageId in Synckit.UploadMessages) {
                    Cts.Token.ThrowIfCancellationRequested ();
                    var emailMessage = AppendMessage (mailKitFolder, Synckit.Folder, messageId.Id);
                    // add the uploaded email to the syncSet, so that we immedaitely sync it back down.
                    uidSet.Add (new UniqueId (emailMessage.ImapUid));
                    changed = true;
                }
                var protocolState = BEContext.ProtocolState;
                Synckit.SyncInstructions.Add (ImapStrategy.SyncInstructionForNewMails (ref protocolState, SyncKit.MustUniqueIdSet (uidSet)));
            }
            foreach (var syncInst in Synckit.SyncInstructions) {
                if (null != syncInst.UidSet && syncInst.UidSet.Any ()) {
                    var sw = new PlatformStopwatch ();
                    sw.Start ();
                    try {
                        // we might have re-opened the folder 'read-write' above. re-open as read-only.
                        // Note if we didn't reopen the folder, this call will do nothing.
                        mailKitFolder = GetOpenMailkitFolder (Synckit.Folder, FolderAccess.ReadOnly);
                        // First find all messages marked as /Deleted
                        UniqueIdSet toDelete = FindDeletedUids (mailKitFolder, syncInst.UidSet);

                        Cts.Token.ThrowIfCancellationRequested ();

                        // Process any new or changed messages. This will also tell us any messages that vanished.
                        UniqueIdSet vanished;
                        UniqueIdSet newOrChanged = GetNewOrChangedMessages (mailKitFolder, syncInst, out vanished);

                        Cts.Token.ThrowIfCancellationRequested ();

                        // add the vanished emails to the toDelete list (it's a set, so duplicates will be handled), then delete them.
                        toDelete.AddRange (vanished);
                        deleteEmails (toDelete);

                        Cts.Token.ThrowIfCancellationRequested ();
                        changed |= toDelete.Any () || newOrChanged.Any ();
                    } finally {
                        sw.Stop ();
                        Log.Info (Log.LOG_IMAP, "{0}: Processing {1} took {2}ms ({3} per uid)", Synckit.Folder.ImapFolderNameRedacted (), syncInst, sw.ElapsedMilliseconds, sw.ElapsedMilliseconds / syncInst.UidSet.Count);
                    }
                }
            }

            // THis section is only for deleting LOCAL emails, i.e. ones that strategy tells us to
            // (probably old ones we no longer need). It is NOT meant to delete messages on the server.
            if (null != Synckit.DeleteEmailIds && Synckit.DeleteEmailIds.Count > 0) {
                changed = true;
                var sw = new PlatformStopwatch ();
                sw.Start ();
                int deleted = 0;
                try {
                    foreach (var emailId in Synckit.DeleteEmailIds) {
                        Cts.Token.ThrowIfCancellationRequested ();
                        var email = emailId.GetMessage ();
                        if (null != email) {
                            email.Delete ();
                            deleted++;
                        }
                    }
                } finally {
                    sw.Stop ();
                    Log.Info (Log.LOG_IMAP, "{0}: removed {1} old emails (took {2}ms)", Synckit.Folder.ImapFolderNameRedacted (), deleted, sw.ElapsedMilliseconds);
                }
            }
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
        }

        public static Tuple<uint, uint> MaxMinOfUidSets (List<SyncInstruction> syncInstructions, McFolder folder)
        {
            uint MaxSynced = 0;
            uint MinSynced = 0;
            var SyncedUidSet = new UniqueIdSet ();
            foreach (var syncInst in syncInstructions) {
                if (null != syncInst.UidSet && syncInst.UidSet.Any ()) {
                    SyncedUidSet.AddRange (syncInst.UidSet);
                    MaxSynced = Math.Max (syncInst.UidSet.Max ().Id, folder.ImapUidHighestUidSynced);
                    MinSynced = Math.Min (syncInst.UidSet.Min ().Id, folder.ImapUidLowestUidSynced);
                }
            }
            return new Tuple<uint, uint> (MaxSynced, MinSynced);
        }

        private void Finish (bool emailSetChanged)
        {
            Cts.Token.ThrowIfCancellationRequested ();
            if (emailSetChanged) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }

            // Update the sync count and last attempt and set the Highest and lowest sync'd
            Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                if (Synckit.MaxSynced.HasValue && Synckit.MaxSynced.Value > target.ImapUidHighestUidSynced) {
                    target.ImapUidHighestUidSynced = Synckit.MaxSynced.Value;
                }
                if (Synckit.MinSynced.HasValue && Synckit.MinSynced.Value < target.ImapUidLowestUidSynced) {
                    target.ImapUidLowestUidSynced = Synckit.MinSynced.Value;
                }
                if (Synckit.CombinedUidSet.Any ()) {
                    target.ImapLastUidSynced = Synckit.CombinedUidSet.Min ().Id;
                }
                target.SyncAttemptCount += 1;
                target.LastSyncAttempt = DateTime.UtcNow;
                return true;
            });

            Cts.Token.ThrowIfCancellationRequested ();

            // update the protocol state
            var protocolState = BEContext.ProtocolState;
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == Synckit.Folder.Type && !protocolState.HasSyncedInbox) {
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.HasSyncedInbox = true;
                    return true;
                });
            }
        }

        private UniqueIdSet GetNewOrChangedMessages (NcImapFolder mailKitFolder, SyncInstruction syncInst, out UniqueIdSet vanished)
        {
            UniqueIdSet newOrChanged = new UniqueIdSet ();
            bool createdUnread = false;
            UniqueIdSet summaryUids = new UniqueIdSet ();
            IList<IMessageSummary> imapSummaries = getMessageSummaries (mailKitFolder, syncInst);
            if (imapSummaries.Any ()) {
                NcCapture.AddKind (KImapPreviewGeneration);
                using (var cap = NcCapture.CreateAndStart (KImapPreviewGeneration)) {
                    foreach (var imapSummary in imapSummaries) {
                        if (imapSummary.Flags.Value.HasFlag (MessageFlags.Deleted)) {
                            continue;
                        }
                        bool changed1;
                        bool created1;
                        MessageSummary summ = imapSummary as MessageSummary;
                        var emailMessage = ServerSaysAddOrChangeEmail (AccountId, summ, Synckit.Folder, out changed1, out created1);
                        if (null == emailMessage) {
                            // something went wrong in the call, but it was logged there, too.
                            continue;
                        }
                        if (changed1) {
                            newOrChanged.Add (summ.UniqueId);
                        }
                        if (created1 && false == emailMessage.IsRead) {
                            createdUnread = true;
                        }
                        if (syncInst.GetPreviews && string.IsNullOrEmpty (emailMessage.BodyPreview)) {
                            NcCapture.AddKind (KImapFetchPartialBody);
                            using (var cap2 = NcCapture.CreateAndStart (KImapFetchPartialBody)) {
                                var preview = getPreviewFromSummary (imapSummary as MessageSummary, mailKitFolder);
                                if (!string.IsNullOrEmpty (preview)) {
                                    emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                                        var target = (McEmailMessage)record;
                                        target.BodyPreview = preview;
                                        target.IsIncomplete = false;
                                        return true;
                                    });
                                }
                                cap2.Stop ();
                            }
                        }
                        if (syncInst.GetHeaders && string.IsNullOrEmpty (emailMessage.Headers)) {
                            NcCapture.AddKind (KImapFetchHeaders);
                            using (var cap3 = NcCapture.CreateAndStart (KImapFetchHeaders)) {
                                emailMessage = FetchHeaders (emailMessage, mailKitFolder, Cts.Token);
                            }
                        }
                        summaryUids.Add (imapSummary.UniqueId);
                    }
                }
            }
            vanished = SyncKit.MustUniqueIdSet (syncInst.UidSet.Except (summaryUids).ToList ());
            if (createdUnread && Synckit.Folder.IsDistinguished && Synckit.Folder.Type == NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            return newOrChanged;
        }

        private IList<IMessageSummary> getMessageSummaries (IMailFolder mailKitFolder, SyncInstruction syncInst)
        {
            NcCapture.AddKind (KImapFetchTiming);
            IList<IMessageSummary> imapSummaries = null;
            try {
                using (var cap = NcCapture.CreateAndStart (KImapFetchTiming)) {
                    if (syncInst.Headers.Any ()) {
                        imapSummaries = mailKitFolder.Fetch (syncInst.UidSet, syncInst.Flags, syncInst.Headers, Cts.Token);
                    } else {
                        imapSummaries = mailKitFolder.Fetch (syncInst.UidSet, syncInst.Flags, Cts.Token);
                    }
                }
            } catch (ImapProtocolException) {
                // try one-by-one so we can at least get a few.
                Log.Warn (Log.LOG_IMAP, "Could not retrieve summaries in batch. Trying individually");
                if (!Client.IsConnected || !Client.IsAuthenticated) {
                    ConnectAndAuthenticate ();
                }
                mailKitFolder = GetOpenMailkitFolder (Synckit.Folder);
                imapSummaries = new List<IMessageSummary> ();
                foreach (var uid in syncInst.UidSet) {
                    try {
                        var s = mailKitFolder.Fetch (new List<UniqueId> { uid }, syncInst.Flags, Cts.Token);
                        if (1 == s.Count) {
                            imapSummaries.Add (s [0]);
                        } else if (s.Count > 0) {
                            Log.Error (Log.LOG_IMAP, "Got {0} summaries but was expecting 1", s.Count);
                        }
                    } catch (ImapProtocolException ex1) {
                        // FIXME In our current scheme we can not handle a 'lost' message like this, as we only know Min and Max UID. Need a better Sync scheme.
                        Log.Error (Log.LOG_IMAP, "Could not fetch item uid {0}\n{1}", uid, ex1);
                        if (!Client.IsConnected || !Client.IsAuthenticated) {
                            ConnectAndAuthenticate ();
                        }
                        mailKitFolder = GetOpenMailkitFolder (Synckit.Folder);
                    }
                }
            }
            return imapSummaries;
        }

        public static McEmailMessage ServerSaysAddOrChangeEmail (int accountId, MessageSummary imapSummary, McFolder folder, out bool changed, out bool created)
        {
            changed = false;
            created = false;
            bool justCreated = false;
            if (string.Empty == imapSummary.UniqueId.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No Summary ServerId present.");
                return null;
            }

            string McEmailMessageServerId = ImapProtoControl.MessageServerId (folder, imapSummary.UniqueId);
            McEmailMessage emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, McEmailMessageServerId);
            var attachments = SummaryAttachmentCollector.AttachmentsForSummary (imapSummary);
            if (null != emailMessage) {
                try {
                    changed = UpdateEmailMetaData (emailMessage, imapSummary);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception updating: {0}", ex.ToString ());
                }
            } else if (imapSummary.Envelope != null) {
                try {
                    emailMessage = ParseEmail (accountId, McEmailMessageServerId, imapSummary, attachments);
                    emailMessage.DetermineIfIsAction (folder);
                    updateFlags (emailMessage, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                    changed = true;
                    justCreated = true;
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                    return null;
                }
            } else {
                // We don't have an emailMessage, but we didn't fetch the Envelope. This means we got here
                // for an email we don't have in our DB, but for which a 'flag-update' was issued. Perhaps 
                // the email was deleted locally after the SyncKit was created.
                Log.Info (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Flag-only-update for unknown email message (imap UID {0}) in folder {1}", imapSummary.UniqueId, folder.ImapFolderNameRedacted ());
                created = false;
                return null;
            }

            if (changed) {
                // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
                NcModel.Instance.RunInTransaction (() => {
                    if (justCreated) {
                        emailMessage.IsJunk = folder.IsJunkFolder ();
                        emailMessage.Insert ();
                        folder.Link (emailMessage);
                        InsertAttachments (emailMessage, attachments);
                        if (emailMessage.IsChat) {
                            var result = BackEnd.Instance.DnldEmailBodyCmd (emailMessage.AccountId, emailMessage.Id, false);
                            if (result.isError ()) {
                                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: could not start download for chat message: {0}", result);
                            }
                        }
                        if (emailMessage.IsAction) {
                            McAction.RunCreateActionFromMessageTask (emailMessage.Id);
                        }
                    } else {
                        emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            updateFlags (target, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                            return true;
                        });
                    }
                });
            }

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
                if (justCreated) {
                    emailMessage.ProcessAfterReceipt ();
                }
                if (emailMessage.ScoreStates.IsRead != emailMessage.IsRead) {
                    // Another client has remotely read / unread this email.
                    // TODO - Should be the average of now and last sync time. But last sync time does not exist yet
                    NcBrain.MessageReadStatusUpdated (emailMessage, DateTime.UtcNow, 60.0);
                }
            }
            created = justCreated;
            return emailMessage;
        }

        /// <summary>
        /// Adds the message to the given IMAP folder
        /// </summary>
        /// <returns>McEmailMessage</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="folder">Folder.</param>
        /// <param name="EmailMessageId">Email message Id.</param>
        private McEmailMessage AppendMessage (IMailFolder mailKitFolder, McFolder folder, int EmailMessageId)
        {
            McEmailMessage EmailMessage = McEmailMessage.QueryById<McEmailMessage> (EmailMessageId);
            McBody body = McBody.QueryById<McBody> (EmailMessage.BodyId);
            MimeMessage mimeMessage = MimeHelpers.LoadMessage (body);
            var attachments = McAttachment.QueryByItem (EmailMessage);
            if (attachments.Count > 0) {
                MimeHelpers.AddAttachments (mimeMessage, attachments);
            }
            MessageFlags flags = MessageFlags.None;
            // If we uploaded a message to the sent folder, mark it as read.
            var defSent = McFolder.GetDefaultSentFolder (AccountId);
            if (null != defSent && defSent.Id == folder.Id) {
                flags |= MessageFlags.Seen;
            }
            var uid = mailKitFolder.Append (mimeMessage, flags, Cts.Token);
            if (uid.HasValue) {
                EmailMessage = EmailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                    var target = (McEmailMessage)record;
                    target.ImapUid = uid.Value.Id;
                    target.ServerId = ImapProtoControl.MessageServerId (Synckit.Folder, uid.Value);
                    return true;
                });
                EmailMessage = FixupFromInfo (EmailMessage);
            } else {
                Log.Error (Log.LOG_IMAP, "Append to Folder did not return a uid!");
            }
            return EmailMessage;
        }

        private UniqueIdSet FindDeletedUids (IMailFolder mailKitFolder, IList<UniqueId> uids)
        {
            // Check for deleted messages
            SearchQuery query = SearchQuery.Deleted;
            UniqueIdSet messagesDeleted = SyncKit.MustUniqueIdSet (mailKitFolder.Search (uids, query, Cts.Token));
            return messagesDeleted;
        }

        private UniqueIdSet deleteEmails (UniqueIdSet uids)
        {
            // TODO Convert some of this to queries instead of loops
            UniqueIdSet messagesDeleted = new UniqueIdSet ();
            foreach (var uid in uids) {
                var email = McEmailMessage.QueryByServerId<McEmailMessage> (AccountId, ImapProtoControl.MessageServerId (Synckit.Folder, uid));
                if (null != email) {
                    email.Delete ();
                    messagesDeleted.Add (uid);
                }
            }
            return messagesDeleted;
        }

        public static bool UpdateEmailMetaData (McEmailMessage emailMessage, MessageSummary summary)
        {
            if (!summary.Flags.HasValue) {
                Log.Error (Log.LOG_IMAP, "Trying to update email message without any flags");
                return false;
            }
            MessageFlags flags = AdjustFlagsFromPendings (emailMessage.AccountId, emailMessage.Id, summary.Flags.GetValueOrDefault ());
            HashSet<string> UserFlags = summary.UserFlags;
            bool changed = false;

            if (updateFlags (emailMessage, flags, UserFlags)) {
                changed = true;
            }
            if (string.IsNullOrEmpty (emailMessage.ConversationId)) {
                // this can happen for emails we've sent out.
                if (SetConversationId (emailMessage, summary)) {
                    changed = true;
                }
            }
            if (emailMessage.PopulateCachedFields ()) {
                changed = true;
            }
            return changed;
        }

        static MessageFlags AdjustFlagsFromPendings (int accountId, int emailId, MessageFlags flags)
        {
            var pendings = NcModel.Instance.Db.Query<McPending> (
                string.Format ("SELECT * FROM McPending WHERE AccountId=? AND ServerId=? AND State NOT IN ('{0}') AND Operation IN ('{1}')",
                    string.Join ("','", new List<McPending.StateEnum> () {
                        McPending.StateEnum.Failed,
                        McPending.StateEnum.Deleted,
                    }),
                    string.Join ("','", new List<McPending.Operations> () {
                        McPending.Operations.EmailMarkRead,
                    })),
                accountId,
                emailId);

            foreach (McPending pending in pendings) {
                switch (pending.Operation) {
                case McPending.Operations.EmailMarkRead:
                    // We have a pending that is supposed to un/set the Seen flag, but we have an incoming sync with
                    // a value that differs. Override the incoming value, so we don't set the DB to the 'old' (on-server)
                    // value that we're going to set when the outgoing change gets processed.
                    Log.Warn (Log.LOG_IMAP, "ImapSyncCommand{0}: Overriding incoming IsRead={1} due to pending IsRead={2}", accountId,
                        ((flags & MessageFlags.Seen) == MessageFlags.Seen),
                        (pending.EmailSetFlag_FlagType == McPending.MarkReadFlag));
                    if (pending.EmailSetFlag_FlagType == McPending.MarkReadFlag) {
                        flags |= MessageFlags.Seen;
                    } else {
                        flags &= ~MessageFlags.Seen;
                    }
                    break;
                }
            }
            return flags;
        }

        private static bool updateFlags (McEmailMessage emailMessage, MessageFlags Flags, HashSet<string> UserFlags)
        {
            bool changed = false;
            bool before = emailMessage.IsRead;
            emailMessage.IsRead = ((Flags & MessageFlags.Seen) == MessageFlags.Seen);
            if (emailMessage.IsRead != before) {
                changed = true;
            }
            if ((Flags & MessageFlags.Answered) == MessageFlags.Answered) {
                // we don't really know if this was ReplyAll or ReplyToSender. So just assume ReplyToSender,
                // but don't overwrite any REPLYTO<something> value we might have set previously.
                if (emailMessage.LastVerbExecuted != (int)AsLastVerbExecutedType.REPLYTOALL &&
                    emailMessage.LastVerbExecuted != (int)AsLastVerbExecutedType.REPLYTOSENDER) {
                    emailMessage.LastVerbExecuted = (int)AsLastVerbExecutedType.REPLYTOSENDER;
                    emailMessage.LastVerbExecutionTime = DateTime.UtcNow;
                }
            }
            if ((Flags & MessageFlags.Flagged) == MessageFlags.Flagged) {
                //emailMessage.UserAction = 1;
            }
            if ((Flags & MessageFlags.Deleted) == MessageFlags.Deleted) {
                // deleted by not yet expunged. Should we set the AwaitingDelete flag?
            }
            if ((Flags & MessageFlags.Draft) == MessageFlags.Draft) {
            }
            if ((Flags & MessageFlags.Recent) == MessageFlags.Recent) {
            }
            if ((Flags & MessageFlags.UserDefined) == MessageFlags.UserDefined) {
                // FIXME See if these are handled by the summary.UserFlags
            }
            if (null != UserFlags && UserFlags.Count > 0) {
                // FIXME Where do we set these flags?
            }
            return changed;
        }

        public static McEmailMessage FixupFromInfo (McEmailMessage emailMessage)
        {
            emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                var target = (McEmailMessage)record;
                target.PopulateCachedFields ();
                return true;
            });
            return emailMessage;
        }

        static Dictionary<string, string> KnownMalformedSenders = new Dictionary<string, string> () {
            {"\"Calendar <calendar-notification@google.com>\" <Google>", "Google Calendar <calendar-notification@google.com>"}
        };

        public static McEmailMessage ParseEmail (int accountId, string ServerId, MessageSummary summary, List<BodyPartBasic> attachments)
        {
            NcAssert.NotNull (summary.Envelope, "Message Envelope is null!");

            var emailMessage = new McEmailMessage () {
                ServerId = ServerId,
                ImapUid = summary.UniqueId.Id,
                AccountId = accountId,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = string.Empty,
                cachedFromColor = 1,
                cachedHasAttachments = attachments != null && attachments.Count > 0,
                ImapBodyStructure = summary.Body != null ? summary.Body.ToString () : null,
            };

            emailMessage.To = summary.Envelope.To.ToString ();
            emailMessage.Cc = summary.Envelope.Cc.ToString ();
            emailMessage.Bcc = summary.Envelope.Bcc.ToString ();

            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                var fromAddr = summary.Envelope.From [0] as MailboxAddress;
                if (null == fromAddr) {
                    Log.Warn (Log.LOG_IMAP, "envelope from is not MailboxAddress: {0}", summary.Envelope.From [0].GetType ().Name);
                }
                // get the address via ToString from the parent class.
                // This handles both MailboxAddress, and InternetAddress
                // see MimeKit docs for details on what each are.
                emailMessage.From = summary.Envelope.From [0].ToString ();
                if (string.IsNullOrEmpty (emailMessage.From)) {
                    if (null != fromAddr) {
                        emailMessage.From = fromAddr.Address;
                        if (string.IsNullOrEmpty (emailMessage.From)) {
                            Log.Info (Log.LOG_IMAP, "No emailMessage.From Address: {0}", summary.UniqueId);
                            emailMessage.From = string.Empty; // make sure it's at least empty, not null.
                        }
                    }
                }
            }
            if (summary.Envelope.ReplyTo.Count > 0) {
                emailMessage.ReplyTo = string.Join (";", summary.Envelope.ReplyTo);
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                var sender = summary.Envelope.Sender [0].ToString ();
                if (KnownMalformedSenders.ContainsKey (sender)) {
                    sender = KnownMalformedSenders [sender];
                }
                emailMessage.Sender = sender;
            }
            if (null != summary.References && summary.References.Any ()) {
                emailMessage.References = string.Join ("\n", summary.References);
            }

            if (null != summary.Headers) {
                bool haveImportance = false;
                NcImportance importance;
                foreach (var header in summary.Headers) {
                    switch (header.Id) {
                    case HeaderId.ContentClass:
                        emailMessage.ContentClass = header.Value;
                        break;

                    case HeaderId.Importance:
                        // The importance header takes priority (hah) over everything else.
                        if (McEmailMessage.TryImportanceFromString (header.Value, out importance)) {
                            emailMessage.Importance = importance;
                            haveImportance = true;
                        }
                        break;

                    case HeaderId.XMSMailPriority:
                    case HeaderId.Priority:
                    case HeaderId.XPriority:
                        // take the first we come across.
                        if (!haveImportance) {
                            if (McEmailMessage.TryImportanceFromString (header.Value, out importance)) {
                                emailMessage.Importance = importance;
                                haveImportance = true;
                            }
                        }
                        break;

                    case HeaderId.DkimSignature:
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty (emailMessage.MessageID) && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }
            SetConversationId (emailMessage, summary);
            emailMessage.ParseIntentFromSubject ();
            emailMessage.IsIncomplete = false;
            emailMessage.DetermineIfIsChat ();

            return emailMessage;
        }

        private static bool SetConversationId (McEmailMessage emailMessage, MessageSummary summary)
        {
            bool changed = false;
            if (summary.GMailThreadId.HasValue) {
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
                changed = true;
            }
            if (string.IsNullOrEmpty (emailMessage.ConversationId)) {
                var references = new List<string> ();
                if (!String.IsNullOrEmpty (emailMessage.InReplyTo)) {
                    references.AddRange (MimeKit.Utils.MimeUtils.EnumerateReferences (emailMessage.InReplyTo));
                }
                if (!String.IsNullOrEmpty (emailMessage.References)) {
                    references.AddRange (emailMessage.References.Split ('\n'));
                }
                foreach (var reference in references) {
                    var referencedMessage = McEmailMessage.QueryByMessageId (emailMessage.AccountId, reference);
                    if (referencedMessage != null) {
                        emailMessage.ConversationId = referencedMessage.ConversationId;
                        changed = true;
                        break;
                    }
                }
                if (String.IsNullOrEmpty (emailMessage.ConversationId)) {
                    emailMessage.ConversationId = Guid.NewGuid ().ToString ();
                    changed = true;
                }
            }
            return changed;
        }

        public static McEmailMessage FetchHeaders (McEmailMessage email, NcImapFolder mailKitFolder, CancellationToken Token)
        {
            var uid = new UniqueId (email.ImapUid);
            var stream = mailKitFolder.GetStream (uid, "HEADER", Token);
            using (var decoded = new MemoryStream ()) {
                stream.CopyTo (decoded);
                var buffer = decoded.GetBuffer ();
                var length = (int)decoded.Length;
                var headers = Encoding.UTF8.GetString (buffer, 0, length);
                if (!string.IsNullOrEmpty (headers)) {
                    email = email.UpdateWithOCApply<McEmailMessage> ((record) => {
                        var target = (McEmailMessage)record;
                        target.Headers = headers;
                        return true;
                    });
                }
            }
            return email;
        }

        public static void InsertAttachments (McEmailMessage msg, List<BodyPartBasic> attachments)
        {
            if (attachments != null) {
                foreach (var att in attachments) {
                    // Create & save the attachment record.
                    var attachment = new McAttachment {
                        AccountId = msg.AccountId,
                        FileSize = att.Octets,
                        FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Actual,
                        FileReference = att.PartSpecifier, // not sure what to put here
                        //Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                    };
                    attachment.SetDisplayName (att.FileName); // FileName looks at ContentDisposition
                    if (null != att.ContentLocation) {
                        attachment.ContentLocation = att.ContentLocation.ToString ();
                    }
                    if (null != att.ContentType) {
                        attachment.ContentType = att.ContentType.MimeType.ToLower ();
                    }
                    if (!String.IsNullOrEmpty (att.ContentId)) {
                        var contentId = att.ContentId.Trim ();
                        if (contentId.Length > 0) {
                            if (contentId [0] == '<') {
                                contentId = contentId.Substring (1);
                            }
                        }
                        if (contentId.Length > 0) {
                            if (contentId [contentId.Length - 1] == '>') {
                                contentId = contentId.Substring (0, contentId.Length - 1);
                            }
                        }
                        attachment.ContentId = contentId;
                    }
                    attachment.IsInline = !att.IsAttachment;
                    //attachment.VoiceSeconds = uint.Parse (xmlUmAttDuration.Value);
                    //attachment.VoiceOrder = int.Parse (xmlUmAttOrder.Value);
                    NcModel.Instance.RunInTransaction (() => {
                        attachment.Insert ();
                        attachment.Link (msg);
                    });
                }
            }
        }

        readonly string [] ValidTextParts = { "plain", "html" };

        /// <summary>
        /// Gets the preview from summary.
        /// Using the fact that summary.BodyParts is a list (enumerable) of BodyPartBasic, we can
        /// just loop over this list and try to find the first one that comes back with a preview.
        /// </summary>
        /// <returns>The preview from summary.</returns>
        /// <param name="summary">Summary.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        private string getPreviewFromSummary (MessageSummary summary, IMailFolder mailKitFolder)
        {
            foreach (var part in summary.BodyParts) {
                if (!part.ContentType.IsMimeType ("text", "*") ||
                    !ValidTextParts.Contains (part.ContentType.MediaSubtype.ToLowerInvariant ())) {
                    continue;
                }
                var preview = getPreviewFromBodyPart (summary.UniqueId, part, mailKitFolder);
                if (!string.IsNullOrEmpty (preview)) {
                    return preview;
                }
            }

            // if we got here, there's no preview we were able to make
            // This can happen if there's only attachments in the message.
            return string.Empty;
        }

        private string getPreviewFromBodyPart (UniqueId uid, BodyPartBasic part, IMailFolder mailKitFolder)
        {
            uint previewBytes;
            var textPart = part as BodyPartText;
            if (null != textPart && textPart.IsHtml) {
                previewBytes = PreviewSizeBytes * PreviewHtmlMultiplier;
            } else {
                previewBytes = PreviewSizeBytes;
            }
            previewBytes = Math.Min (previewBytes, part.Octets);
            if (previewBytes == 0) {
                return string.Empty;
            }

            string partSpecifier;
            if (string.IsNullOrEmpty (part.PartSpecifier)) {
                partSpecifier = "TEXT";
            } else {
                partSpecifier = part.PartSpecifier;
                if (part is BodyPartMessage) {
                    partSpecifier += ".TEXT";
                }
            }

            string preview = string.Empty;
            try {
                Stream stream = mailKitFolder.GetStream (uid, partSpecifier, 0, (int)previewBytes, Cts.Token);
                if (null != stream && stream.Length > 0) {
                    using (var decoded = new MemoryStream ()) {
                        // Note that the outCharSet ("utf-8") must match what we use in Encoding.<xxx>.GetString.
                        CopyFilteredStream (stream, decoded, part.ContentType.Charset, part.ContentTransferEncoding, CopyDataAction, "utf-8");
                        var buffer = decoded.GetBuffer ();
                        var length = (int)decoded.Length;
                        preview = Encoding.UTF8.GetString (buffer, 0, length);
                    }
                }
            } catch (ImapCommandException e) {
                // if this is a temporary error, we'll get the preview when we download the body.
                Log.Error (Log.LOG_IMAP, "Could not fetch stream: {0}", e);
                preview = string.Empty;
            }
            if (string.IsNullOrEmpty (preview)) {
                return preview; // empty
            }

            bool needHtmlDecode = (null != textPart && textPart.IsPlain) ? false : true;
            return needHtmlDecode ? Html2Text (preview) : preview;
        }

        private void CopyDataAction (Stream inStream, Stream outStream)
        {
            inStream.CopyTo (outStream);
        }

        private class SummaryAttachmentCollector : BodyPartVisitor
        {

            List<BodyPartBasic> Attachments;
            bool IsInAlternative;

            public SummaryAttachmentCollector ()
            {
                Attachments = new List<BodyPartBasic> ();
            }

            public static List<BodyPartBasic> AttachmentsForSummary (MessageSummary summary)
            {
                var collector = new SummaryAttachmentCollector ();
                collector.Visit (summary.Body);
                return collector.Attachments;
            }

            protected override void VisitBodyPartMultipart (BodyPartMultipart multipart)
            {
                if (multipart.ContentType != null && multipart.ContentType.IsMimeType ("multipart", "alternative")) {
                    VisitBodyPartAlternative (multipart);
                } else if (multipart.ContentType != null && multipart.ContentType.IsMimeType ("multipart", "related")) {
                    VisitBodyPartRelated (multipart);
                } else {
                    VisitChildren (multipart);
                }
            }

            void VisitBodyPartRelated (BodyPartMultipart multipart)
            {
                for (int i = 1; i < multipart.BodyParts.Count; ++i) {
                    multipart.BodyParts [i].Accept (this);
                }
            }

            void VisitBodyPartAlternative (BodyPartMultipart multipart)
            {
                if (IsInAlternative) {
                    VisitChildren (multipart);
                } else {
                    IsInAlternative = true;
                    VisitChildren (multipart);
                    IsInAlternative = false;
                }
            }

            protected override void VisitBodyPartBasic (BodyPartBasic entity)
            {
                Attachments.Add (entity);
            }

            protected override void VisitBodyPartMessage (BodyPartMessage entity)
            {
                Attachments.Add (entity);
            }

            protected override void VisitBodyPartText (BodyPartText entity)
            {
                bool isAttachment = entity.ContentDisposition != null && entity.ContentDisposition.IsAttachment;
                if (!isAttachment && !IsInAlternative) {
                    isAttachment = !entity.ContentType.IsMimeType ("text", "plain") && !entity.ContentType.IsMimeType ("text", "html") && !entity.ContentType.IsMimeType ("text", "rtf");
                }
                if (isAttachment && !string.IsNullOrEmpty (entity.FileName) && MimeHelpers.isExchangeATTFilename (entity.FileName)) {
                    isAttachment = false;
                }
                if (isAttachment) {
                    Attachments.Add (entity);
                }
            }
        }

    }
}
