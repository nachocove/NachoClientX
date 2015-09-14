//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapCommand
    {
        SyncKit Synckit;
        private const int PreviewSizeBytes = 500;
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

        public ImapSyncCommand (IBEContext beContext, NcImapClient imap, SyncKit syncKit) : base (beContext, imap)
        {
            Synckit = syncKit;
            PendingSingle = Synckit.PendingSingle;
            if (null != PendingSingle) {
                PendingSingle.MarkDispached ();
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
            UpdateImapSetting (mailKitFolder, ref Synckit.Folder);

            if (UInt32.MinValue != Synckit.Folder.ImapUidValidity &&
                Synckit.Folder.ImapUidValidity != mailKitFolder.UidValidity) {
                return Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPSYNCUIDINVAL");
            }
            Event evt;
            NcCapture cap;
            switch (Synckit.Method) {
            case SyncKit.MethodEnum.OpenOnly:
                if (null != Synckit.PendingSingle) {
                    Log.Error (Log.LOG_IMAP, "OpenOnly SyncKit with a pending is not allowed");
                }
                NcCapture.AddKind (KImapSyncOpenTiming);
                cap = NcCapture.CreateAndStart (KImapSyncOpenTiming);
                evt = getFolderMetaDataInternal (mailKitFolder, timespan);
                break;

            case SyncKit.MethodEnum.Sync:
                NcCapture.AddKind (KImapSyncTiming);
                cap = NcCapture.CreateAndStart (KImapSyncTiming);
                evt = syncFolder (mailKitFolder);
                ImapStrategy.ResolveOneSync (BEContext, Synckit);
                break;

            case SyncKit.MethodEnum.QuickSync:
                NcCapture.AddKind (KImapQuickSyncTiming);
                cap = NcCapture.CreateAndStart (KImapQuickSyncTiming);
                evt = QuickSync (mailKitFolder, Synckit.Span, timespan);
                ImapStrategy.ResolveOneSync (BEContext, Synckit);
                break;

            default:
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDCASE");
            }
            cap.Dispose ();
            return evt;
        }

        /// <summary>
        /// A sort of 'macro' method. This assumes that the Folder Metadata is out of date (or has never been retrieved)
        /// so we do that first. Then we call back into strategy to let it decide what it wants us to do, and finally
        /// we do it (if there's something to do).
        /// </summary>
        /// <returns>The Event to post</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="span">Span.</param>
        /// <param name="timespan">Timespan.</param>
        private Event QuickSync (NcImapFolder mailKitFolder, uint span, TimeSpan timespan)
        {
            if (!GetFolderMetaData (ref Synckit.Folder, mailKitFolder, timespan)) {
                Log.Warn (Log.LOG_IMAP, "Could not get folder metadata");
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCMETAFAIL1");
            }
            bool changed = false;
            Event evt;
            if (ImapStrategy.FillInQuickSyncKit (ref Synckit, AccountId, span)) {
                evt = syncFolder (mailKitFolder);
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
            Finish (changed);
            return evt;
        }

        private Event getFolderMetaDataInternal (NcImapFolder mailKitFolder, TimeSpan timespan)
        {
            if (!GetFolderMetaData (ref Synckit.Folder, mailKitFolder, timespan)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCMETAFAIL");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCOPENSUC");
            }
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
        private Event syncFolder (NcImapFolder mailKitFolder)
        {
            bool changed = false;
            // start by uploading messages
            if (null != Synckit.UploadMessages && Synckit.UploadMessages.Any ()) {
                Synckit.SyncSet = SyncKit.MustUniqueIdSet (Synckit.SyncSet);
                mailKitFolder = GetOpenMailkitFolder (Synckit.Folder, FolderAccess.ReadWrite);
                foreach (var messageId in Synckit.UploadMessages) {
                    Cts.Token.ThrowIfCancellationRequested ();
                    var emailMessage = AppendMessage (mailKitFolder, Synckit.Folder, messageId.Id);
                    // add the uploaded email to the syncSet, so that we immedaitely sync it back down.
                    Synckit.SyncSet.Add (new UniqueId (emailMessage.ImapUid));
                    changed = true;
                }
            }
            if (null != Synckit.SyncSet && Synckit.SyncSet.Any ()) {
                mailKitFolder = GetOpenMailkitFolder (Synckit.Folder, FolderAccess.ReadOnly);
                // First find all messages marked as /Deleted
                UniqueIdSet toDelete = FindDeletedUids (mailKitFolder, Synckit.SyncSet);

                Cts.Token.ThrowIfCancellationRequested ();

                // Process any new or changed messages. This will also tell us any messages that vanished.
                UniqueIdSet vanished;
                UniqueIdSet newOrChanged = GetNewOrChangedMessages (mailKitFolder, Synckit.SyncSet, out vanished);

                Cts.Token.ThrowIfCancellationRequested ();

                // add the vanished emails to the toDelete list (it's a set, so duplicates will be handled), then delete them.
                toDelete.AddRange (vanished);
                var deleted = deleteEmails (toDelete);

                Cts.Token.ThrowIfCancellationRequested ();
                changed |= deleted.Any () || newOrChanged.Any ();
            }

            Finish (changed);
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
        }

        private void Finish (bool emailSetChanged)
        {
            Cts.Token.ThrowIfCancellationRequested ();
            if (emailSetChanged) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            // remember where we sync'd
            uint MaxSynced = 0;
            uint MinSynced = 0;
            if (null != Synckit.SyncSet && Synckit.SyncSet.Any ()) {
                MaxSynced = Math.Max (Synckit.SyncSet.Max ().Id, Synckit.Folder.ImapUidHighestUidSynced);
                MinSynced = Math.Min (Synckit.SyncSet.Min ().Id, Synckit.Folder.ImapUidLowestUidSynced);
                if (MaxSynced != 0 && MaxSynced != Synckit.Folder.ImapUidHighestUidSynced ||
                    MinSynced != 0 && MinSynced != Synckit.Folder.ImapUidLowestUidSynced) {
                }
            }
            // Update the sync count and last attempt and set the Highest and lowest sync'd
            var exeCtxt = NcApplication.Instance.ExecutionContext;
            Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                if (MaxSynced != 0) {
                    target.ImapUidHighestUidSynced = MaxSynced;
                }
                if (MinSynced != 0) {
                    target.ImapUidLowestUidSynced = MinSynced;
                }
                if (Synckit.SyncSet != null && Synckit.SyncSet.Any ()) {
                    target.ImapLastUidSynced = Synckit.SyncSet.Min ().Id;
                }
                target.SyncAttemptCount += 1;
                target.LastSyncAttempt = DateTime.UtcNow;
                if (Synckit.Method == SyncKit.MethodEnum.QuickSync && exeCtxt == NcApplication.ExecutionContextEnum.Foreground) {
                    // After a quick sync we really need to do a full sync to capture deleted and changed messages
                    target.ImapNeedFullSync = true;
                }
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

        private UniqueIdSet GetNewOrChangedMessages (NcImapFolder mailKitFolder, IList<UniqueId> uidset, out UniqueIdSet vanished)
        {
            UniqueIdSet newOrChanged = new UniqueIdSet ();
            bool createdUnread = false;
            UniqueIdSet summaryUids = new UniqueIdSet ();
            IList<IMessageSummary> imapSummaries = getMessageSummaries (mailKitFolder, uidset);
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
                        if (Synckit.GetPreviews && string.IsNullOrEmpty (emailMessage.BodyPreview)) {
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
                        if (Synckit.GetHeaders && string.IsNullOrEmpty (emailMessage.Headers)) {
                            NcCapture.AddKind (KImapFetchHeaders);
                            using (var cap3 = NcCapture.CreateAndStart (KImapFetchHeaders)) {
                                var headers = FetchHeaders (mailKitFolder, summ);
                                if (!string.IsNullOrEmpty (headers)) {
                                    emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                                        var target = (McEmailMessage)record;
                                        target.Headers = headers;
                                        return true;
                                    });
                                }
                            }
                        }
                        summaryUids.Add (imapSummary.UniqueId);
                    }
                }
            }
            vanished = SyncKit.MustUniqueIdSet (uidset.Except (summaryUids).ToList ());
            if (createdUnread && Synckit.Folder.IsDistinguished && Synckit.Folder.Type == NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            return newOrChanged;
        }

        private IList<IMessageSummary> getMessageSummaries (IMailFolder mailKitFolder, IList<UniqueId> uidset)
        {
            NcCapture.AddKind (KImapFetchTiming);
            IList<IMessageSummary> imapSummaries = null;
            try {
                using (var cap = NcCapture.CreateAndStart (KImapFetchTiming)) {
                    if (Synckit.Headers.Any ()) {
                        imapSummaries = mailKitFolder.Fetch (uidset, Synckit.Flags, Synckit.Headers, Cts.Token);
                    } else {
                        imapSummaries = mailKitFolder.Fetch (uidset, Synckit.Flags, Cts.Token);
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
                foreach (var uid in uidset) {
                    try {
                        var s = mailKitFolder.Fetch (new List<UniqueId>{ uid }, Synckit.Flags, Cts.Token);
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
            if (null != emailMessage) {
                try {
                    changed = UpdateEmailMetaData (emailMessage, imapSummary);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception updating: {0}", ex.ToString ());
                }
            } else {
                try {
                    emailMessage = ParseEmail (accountId, McEmailMessageServerId, imapSummary);
                    updateFlags (emailMessage, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                    changed = true;
                    justCreated = true;
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                    return null;
                }
            }

            if (changed) {
                // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
                NcModel.Instance.RunInTransaction (() => {
                    if (justCreated) {
                        emailMessage.Insert ();
                        folder.Link (emailMessage);
                        InsertAttachments (emailMessage, imapSummary as MessageSummary);
                        NcContactGleaner.GleanContactsHeaderPart1 (emailMessage, folder.IsJunkFolder ());
                    } else {
                        emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            updateFlags (target, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                            return true;
                        });
                        if (emailMessage.ScoreStates.IsRead != emailMessage.IsRead) {
                            // Another client has remotely read / unread this email.
                            // TODO - Should be the average of now and last sync time. But last sync time does not exist yet
                            NcBrain.MessageReadStatusUpdated (emailMessage, DateTime.UtcNow, 60.0);
                        }
                    }
                });
            }

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
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
            var attachments = McAttachment.QueryByItemId (EmailMessage);
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
                EmailMessage = FixupFromInfo (EmailMessage, true);
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
            MessageFlags Flags = summary.Flags.GetValueOrDefault ();
            HashSet<string> UserFlags = summary.UserFlags;
            bool changed = false;

            if (updateFlags (emailMessage, Flags, UserFlags)) {
                changed = true;
            }
            if (string.IsNullOrEmpty (emailMessage.ConversationId)) {
                // this can happen for emails we've sent out.
                if (SetConversationId (emailMessage, summary)) {
                    changed = true;
                }
            }
            bool ch;
            FixupFromInfo (emailMessage, false, out ch);
            if (ch) {
                changed = true;
            }
            return changed;
        }

        private static bool updateFlags (McEmailMessage emailMessage, MessageFlags Flags, HashSet<string> UserFlags)
        {
            bool changed = false;
            bool before = emailMessage.IsRead;
            emailMessage.IsRead = ((Flags & MessageFlags.Seen) == MessageFlags.Seen);
            if (emailMessage.IsRead != before) {
                changed = true;
            }
            // FIXME Where do we set these flags?
            if ((Flags & MessageFlags.Answered) == MessageFlags.Answered) {
            }
            if ((Flags & MessageFlags.Flagged) == MessageFlags.Flagged) {
                //emailMessage.UserAction = 1;
            }
            if ((Flags & MessageFlags.Deleted) == MessageFlags.Deleted) {
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

        public static McEmailMessage FixupFromInfo (McEmailMessage emailMessage, bool updateDb)
        {
            bool changed;
            return FixupFromInfo (emailMessage, updateDb, out changed);
        }

        public static McEmailMessage FixupFromInfo (McEmailMessage emailMessage, bool updateDb, out bool changed)
        {
            changed = false;
            if (!string.IsNullOrEmpty (emailMessage.From)) {
                string cachedFromLetters = string.Empty;
                int cachedFromColor = 1;
                int fromEmailAddressId = 0;
                try {
                    cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "Could not get Initials from email. Ignoring Initials. {0}", ex);
                }

                McEmailAddress fromEmailAddress;
                if (McEmailAddress.Get (emailMessage.AccountId, emailMessage.From, out fromEmailAddress)) {
                    fromEmailAddressId = fromEmailAddress.Id;
                    cachedFromColor = fromEmailAddress.ColorIndex;
                }
                if (emailMessage.cachedFromLetters != cachedFromLetters ||
                    emailMessage.cachedFromColor != cachedFromColor ||
                    emailMessage.FromEmailAddressId != fromEmailAddressId) {
                    if (updateDb) {
                        emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                            var target = (McEmailMessage)record;
                            target.cachedFromLetters = cachedFromLetters;
                            target.cachedFromColor = cachedFromColor;
                            target.FromEmailAddressId = fromEmailAddressId;
                            return true;
                        });
                    } else {
                        emailMessage.cachedFromLetters = cachedFromLetters;
                        emailMessage.cachedFromColor = cachedFromColor;
                        emailMessage.FromEmailAddressId = fromEmailAddressId;
                    }
                }
                changed = true;
            }
            return emailMessage;
        }

        public static McEmailMessage ParseEmail (int accountId, string ServerId, MessageSummary summary)
        {
            NcAssert.NotNull (summary.Envelope);

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
                cachedHasAttachments = summary.Attachments.Any (),
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
                if (summary.Envelope.ReplyTo.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} ReplyTo entries in message.", summary.Envelope.ReplyTo.Count);
                }
                emailMessage.ReplyTo = summary.Envelope.ReplyTo [0].ToString ();
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                emailMessage.Sender = summary.Envelope.Sender [0].ToString ();
                McEmailAddress fromEmailAddress;
                if (McEmailAddress.Get (accountId, summary.Envelope.Sender [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.SenderEmailAddressId = fromEmailAddress.Id;
                }
            }
            if (null != summary.References && summary.References.Count > 0) {
                if (summary.References.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} References entries in message.", summary.References.Count);
                }
                emailMessage.References = summary.References [0];
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
            emailMessage.IsIncomplete = false;

            return FixupFromInfo (emailMessage, false);
        }

        private static bool SetConversationId (McEmailMessage emailMessage, MessageSummary summary)
        {
            bool changed = false;
            if (summary.GMailThreadId.HasValue) {
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
                changed = true;
            }
            if (string.IsNullOrEmpty (emailMessage.ConversationId)) {
                emailMessage.ConversationId = Guid.NewGuid ().ToString ();
                changed = true;
            }
            return changed;
        }

        private string FetchHeaders (NcImapFolder mailKitFolder, MessageSummary summary)
        {
            var stream = mailKitFolder.GetStream (summary.UniqueId, "HEADER", Cts.Token);
            using (var decoded = new MemoryStream ()) {
                stream.CopyTo (decoded);
                var buffer = decoded.GetBuffer ();
                var length = (int)decoded.Length;
                return Encoding.UTF8.GetString (buffer, 0, length);
            }
        }

        public static void InsertAttachments (McEmailMessage msg, MessageSummary imapSummary)
        {
            if (imapSummary.Attachments.Any ()) {
                foreach (var att in imapSummary.Attachments) {
                    // Create & save the attachment record.
                    var attachment = new McAttachment {
                        AccountId = msg.AccountId,
                        ItemId = msg.Id,
                        ClassCode = msg.GetClassCode (),
                        FileSize = att.Octets,
                        FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Actual,
                        FileReference = att.PartSpecifier, // not sure what to put here
                        //Method = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value),
                    };
                    attachment.SetDisplayName (att.FileName); // FileName looks at ContentDisposition
                    if (null != att.ContentLocation) {
                        attachment.ContentLocation = att.ContentLocation.ToString ();
                    }
                    attachment.ContentId = att.ContentId;
                    attachment.IsInline = !att.IsAttachment;
                    //attachment.VoiceSeconds = uint.Parse (xmlUmAttDuration.Value);
                    //attachment.VoiceOrder = int.Parse (xmlUmAttOrder.Value);
                    attachment.Insert ();
                }
            }
        }

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
                var preview = getPreviewFromBodyPart (summary.UniqueId, part, mailKitFolder);
                Log.Info (Log.LOG_IMAP, "getPreviewFromSummary ({0}): part {1}:{2}", summary.UniqueId, part.PartSpecifier, preview);
                if (!string.IsNullOrEmpty (preview)) {
                    return preview;
                }
            }

            // if we got here, there's no preview we were able to make
            // This can happen if there's only attachments in the message.
            Log.Info (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId);
            return string.Empty;
        }

        private string getPreviewFromBodyPart (UniqueId uid, BodyPartBasic part, IMailFolder mailKitFolder)
        {
            uint previewBytes = Math.Min (PreviewSizeBytes, part.Octets);
            if (previewBytes == 0) {
                return string.Empty;
            }
            string partSpecifier = string.IsNullOrEmpty (part.PartSpecifier) ? "TEXT" : part.PartSpecifier;
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

            BodyPartText t = part as BodyPartText;
            bool needHtmlDecode = (null != t && t.IsPlain) ? false : true;
            return needHtmlDecode ? Html2Text (preview) : preview;
        }

        private void CopyDataAction (Stream inStream, Stream outStream)
        {
            inStream.CopyTo (outStream);
        }

    }
}
