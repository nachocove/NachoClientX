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
using MimeKit.IO;
using MimeKit.IO.Filters;
using MailKit.Search;

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapFetchCommand
    {
        SyncKit Synckit;
        private const int PreviewSizeBytes = 500;
        private const string KImapSyncOpenTiming = "IMAP Sync/Open";
        private const string KImapSyncTiming = "IMAP Sync/Fetch";
        private const string KImapFetchTiming = "IMAP Summary Fetch";
        private const string KImapPreviewGeneration = "IMAP Preview Generation";

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

            NcCapture.AddKind (KImapSyncOpenTiming);
            NcCapture.AddKind (KImapSyncTiming);
            NcCapture.AddKind (KImapFetchTiming);
            NcCapture.AddKind (KImapPreviewGeneration);
        }

        protected override Event ExecuteCommand ()
        {
            IMailFolder mailKitFolder;

            Log.Info (Log.LOG_IMAP, "{0}: Processing {1}", Synckit.Folder.ImapFolderNameRedacted (), Synckit.ToString ());

            var timespan = BEContext.Account.DaysSyncEmailSpan ();
            mailKitFolder = GetOpenMailkitFolder (Synckit.Folder);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN2");
            }
            if (UInt32.MinValue != Synckit.Folder.ImapUidValidity &&
                Synckit.Folder.ImapUidValidity != mailKitFolder.UidValidity) {
                return Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPSYNCUIDINVAL");
            }
            Event result;
            NcCapture cap;
            switch (Synckit.Method) {
            case SyncKit.MethodEnum.OpenOnly:
                cap = NcCapture.CreateAndStart (KImapSyncOpenTiming);
                result = getFolderMetaDataInternal (mailKitFolder, timespan);
                break;

            case SyncKit.MethodEnum.Sync:
                cap = NcCapture.CreateAndStart (KImapSyncTiming);
                result = syncFolder (mailKitFolder, timespan);
                break;

            default:
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDCASE");
            }
            if (PendingList.Any () || null != PendingSingle) {
                PendingResolveApply ((pending) => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl, NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
                });
            } else {
                StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
            }
            cap.Pause ();
            Log.Info (Log.LOG_IMAP, "{0} Sync took {1}ms", Synckit.Folder.ImapFolderNameRedacted (), cap.ElapsedMilliseconds);
            cap.Stop ();
            cap.Dispose ();
            return result;
        }

        private Event getFolderMetaDataInternal (IMailFolder mailKitFolder, TimeSpan timespan)
        {
            if (!GetFolderMetaData (ref Synckit.Folder, mailKitFolder, timespan)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCMETAFAIL");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCOPENSUC");
            }
        }

        public static bool GetFolderMetaData (ref McFolder folder, IMailFolder mailKitFolder, TimeSpan timespan)
        {
            // Just load UID with SELECT.
            Log.Info (Log.LOG_IMAP, "ImapSyncCommand {0}: Getting Folderstate", folder.ImapFolderNameRedacted ());

            var query = SearchQuery.NotDeleted;
            if (TimeSpan.Zero != timespan) {
                query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
            }
            UniqueIdSet uids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query));
            Log.Info (Log.LOG_IMAP, "{1}: Uids from last {2} days: {0}",
                uids.ToString (),
                folder.ImapFolderNameRedacted (), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());

            query = SearchQuery.Deleted;
            if (TimeSpan.Zero != timespan) {
                query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
            }
            var deletedUids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query));
            Log.Info (Log.LOG_IMAP, "{1}: DeletedUids from last {2} days: {0}",
                deletedUids.ToString (),
                folder.ImapFolderNameRedacted (), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());
            
            UpdateImapSetting (mailKitFolder, ref folder);

            if (!string.IsNullOrEmpty (folder.ImapUidSet)) {
                UniqueIdSet current;
                if (UniqueIdSet.TryParse (folder.ImapUidSet, out current)) {
                    var added = new UniqueIdSet (uids.Except (current));
                    var removed = new UniqueIdSet (current.Except (uids));
                    if (added.Any ()) {
                        Log.Info (Log.LOG_IMAP, "{0}: Added UIDs: {1}", folder.ImapFolderNameRedacted (), added.ToString ());
                    }
                    if (removed.Any ()) {
                        Log.Info (Log.LOG_IMAP, "{0}: Removed UIDs: {1}", folder.ImapFolderNameRedacted (), removed.ToString ());
                    }
                }
            }

            var uidstring = uids.ToString ();
            folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                if (uidstring != target.ImapUidSet) {
                    Log.Info (Log.LOG_IMAP, "Updating ImapUidSet");
                    target.ImapUidSet = uidstring;
                }
                target.ImapLastExamine = DateTime.UtcNow;
                return true;
            });
            return true;
        }

        private Event syncFolder (IMailFolder mailKitFolder, TimeSpan timespan)
        {
            NcAssert.True (SyncKit.MethodEnum.Sync == Synckit.Method);

            // First find all messages marked as /Deleted
            UniqueIdSet toDelete = FindDeletedUids (mailKitFolder, Synckit.SyncSet);

            // Process any new or changed messages. This will also tell us any messages that vanished.
            UniqueIdSet vanished;
            UniqueIdSet newOrChanged = GetNewOrChangedMessages (mailKitFolder, Synckit.SyncSet, out vanished);

            // add the vanished emails to the toDelete list (it's a set, so duplicates will be handled), then delete them.
            toDelete.AddRange (vanished);
            var deleted = deleteEmails (toDelete);
            if (deleted.Any () || newOrChanged.Any ()) {
                var messages = McEmailMessage.QueryNeedQuickScoring (BEContext.Account.Id, 100).Count;
                Log.Info (Log.LOG_IMAP, "Sending Info_EmailMessageSetChanged. Brain should see {0} messages", messages);
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            // remember where we sync'd
            uint MaxSynced = Math.Max (Synckit.SyncSet.Max ().Id, Synckit.Folder.ImapUidHighestUidSynced);
            uint MinSynced = Math.Min (Synckit.SyncSet.Min ().Id, Synckit.Folder.ImapUidLowestUidSynced);
            if (MaxSynced != Synckit.Folder.ImapUidHighestUidSynced ||
                MinSynced != Synckit.Folder.ImapUidLowestUidSynced) {
                Log.Info (Log.LOG_IMAP, "{0}: Set ImapUidHighestUidSynced {1} ImapUidLowestUidSynced {2}",
                    Synckit.Folder.ImapFolderNameRedacted (), MaxSynced, MinSynced);
            }

            Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = MaxSynced;
                target.ImapUidLowestUidSynced = MinSynced;
                target.ImapLastUidSynced = Synckit.SyncSet.Min ().Id;
                return true;
            });

            // update the protocol state
            var protocolState = BEContext.ProtocolState;
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == Synckit.Folder.Type) {
                if (!protocolState.HasSyncedInbox) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.HasSyncedInbox = true;
                        return true;
                    });
                }
            }

            // Update the sync count and last attempt
            Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.SyncAttemptCount += 1;
                target.LastSyncAttempt = DateTime.UtcNow;
                return true;
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
        }

        private UniqueIdSet GetNewOrChangedMessages (IMailFolder mailKitFolder, UniqueIdSet uidset, out UniqueIdSet vanished)
        {
            UniqueIdSet newOrChanged = new UniqueIdSet ();
            bool createdUnread = false;
            UniqueIdSet summaryUids = new UniqueIdSet ();
            IList<IMessageSummary> imapSummaries = getMessageSummaries (mailKitFolder, uidset);
            if (imapSummaries.Any ()) {
                using (var cap = NcCapture.CreateAndStart (KImapPreviewGeneration)) {
                    foreach (var imapSummary in imapSummaries) {
                        if (imapSummary.Flags.Value.HasFlag (MessageFlags.Deleted)) {
                            continue;
                        }
                        bool changed1;
                        bool created1;
                        MessageSummary summ = imapSummary as MessageSummary;
                        var emailMessage = ServerSaysAddOrChangeEmail (BEContext.Account.Id, summ, Synckit.Folder, out changed1, out created1);
                        if (changed1) {
                            newOrChanged.Add (summ.UniqueId.Value);
                        }
                        if (created1 && false == emailMessage.IsRead) {
                            createdUnread = true;
                        }
                        if (Synckit.GetPreviews && string.IsNullOrEmpty (emailMessage.BodyPreview)) {
                            var preview = getPreviewFromSummary (imapSummary as MessageSummary, mailKitFolder);
                            if (!string.IsNullOrEmpty (preview)) {
                                emailMessage = emailMessage.UpdateWithOCApply<McEmailMessage> ((record) => {
                                    var target = (McEmailMessage)record;
                                    target.BodyPreview = preview;
                                    target.IsIncomplete = false;
                                    return true;
                                });
                            }
                        }
                        summaryUids.Add (imapSummary.UniqueId.Value);
                    }
                    cap.Pause ();
                    Log.Info (Log.LOG_IMAP, "ImapSyncCommand {0}: Processed {1} message summaries in {2}ms ({3} new or changed)", Synckit.Folder.ImapFolderNameRedacted (), imapSummaries.Count, cap.ElapsedMilliseconds, newOrChanged.Count);
                    cap.Stop ();
                }
            }
            vanished = SyncKit.MustUniqueIdSet (uidset.Except (summaryUids).ToList ());
            if (createdUnread && Synckit.Folder.IsDistinguished && Synckit.Folder.Type == NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox));
            }
            return newOrChanged;
        }

        private IList<IMessageSummary> getMessageSummaries (IMailFolder mailKitFolder, UniqueIdSet uidset)
        {
            IList<IMessageSummary> imapSummaries = null;
            try {
                using (var cap = NcCapture.CreateAndStart (KImapFetchTiming)) {
                    if (Synckit.Headers.Any ()) {
                        imapSummaries = mailKitFolder.Fetch (uidset, Synckit.Flags, Synckit.Headers, Cts.Token);
                    } else {
                        imapSummaries = mailKitFolder.Fetch (uidset, Synckit.Flags, Cts.Token);
                    }
                    cap.Pause ();
                    Log.Info (Log.LOG_IMAP, "Retrieved {0} summaries in {1}ms", imapSummaries.Count, cap.ElapsedMilliseconds);
                    cap.Stop ();
                }
            } catch (ImapProtocolException) {
                // try one-by-one so we can at least get a few.
                Log.Warn (Log.LOG_IMAP, "Could not retrieve summaries in batch. Trying individually");
                if (!Client.IsConnected || !Client.IsAuthenticated) {
                    var authy = new ImapAuthenticateCommand (BEContext, Client);
                    authy.ConnectAndAuthenticate ();
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
                            var authy = new ImapAuthenticateCommand (BEContext, Client);
                            authy.ConnectAndAuthenticate ();
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
            if (null == imapSummary.UniqueId || string.Empty == imapSummary.UniqueId.Value.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No Summary ServerId present.");
                return null;
            }

            string McEmailMessageServerId = ImapProtoControl.MessageServerId (folder, imapSummary.UniqueId.Value);
            McEmailMessage emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, McEmailMessageServerId);
            if (null != emailMessage) {
                try {
                    changed = updateFlags (emailMessage, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception updating: {0}", ex.ToString ());
                }
            } else {
                changed = true;
                try {
                    emailMessage = ParseEmail (accountId, McEmailMessageServerId, imapSummary as MessageSummary);
                    updateFlags (emailMessage, imapSummary.Flags.GetValueOrDefault (), imapSummary.UserFlags);
                    if (null == emailMessage.BodyPreview) {
                        emailMessage.IsIncomplete = true;
                    }
                    justCreated = true;
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                    if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                        emailMessage = new McEmailMessage () {
                            ServerId = McEmailMessageServerId,
                        };
                    }
                    emailMessage.IsIncomplete = true;
                }
            }

            if (changed) {
                // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
                NcModel.Instance.RunInTransaction (() => {
                    if ((0 != emailMessage.FromEmailAddressId) || !String.IsNullOrEmpty (emailMessage.To)) {
                        if (!folder.IsJunkFolder ()) {
                            NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
                        }
                    }
                    if (justCreated) {
                        emailMessage.Insert ();
                        folder.Link (emailMessage);
                        InsertAttachments (emailMessage, imapSummary as MessageSummary);
                    } else {
                        emailMessage.AccountId = folder.AccountId;
                        emailMessage.Update ();
                    }
                });
            }

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
            }
            created = justCreated;
            return emailMessage;
        }

        private UniqueIdSet FindDeletedUids (IMailFolder mailKitFolder, UniqueIdSet uids)
        {
            // Check for deleted messages
            SearchQuery query = SearchQuery.Deleted;
            UniqueIdSet messagesDeleted = SyncKit.MustUniqueIdSet (mailKitFolder.Search (uids, query));
            return messagesDeleted;
        }

        private UniqueIdSet deleteEmails (UniqueIdSet uids)
        {
            // TODO Convert some of this to queries instead of loops
            UniqueIdSet messagesDeleted = new UniqueIdSet ();
            foreach (var uid in uids) {
                var email = McEmailMessage.QueryByServerId<McEmailMessage> (BEContext.Account.Id, ImapProtoControl.MessageServerId (Synckit.Folder, uid));
                if (null != email) {
                    Log.Info (Log.LOG_IMAP, "Deleting: {0}:{1}", Synckit.Folder.ImapFolderNameRedacted (), email.ServerId);
                    email.Delete ();
                    messagesDeleted.Add (uid);
                }
            }
            return messagesDeleted;
        }

        public static bool UpdateEmailMetaData (McEmailMessage emailMessage, IMessageSummary summary)
        {
            if (!summary.Flags.HasValue) {
                Log.Error (Log.LOG_IMAP, "Trying to update email message without any flags");
                return false;
            }
            return UpdateEmailMetaData (emailMessage, summary.Flags.Value, summary.UserFlags);
        }

        public static bool UpdateEmailMetaData (McEmailMessage emailMessage, MessageFlags Flags, HashSet<string> UserFlags)
        {
            bool changed = false;

            // IMAP can only update flags. Anything else is a new UID/message.
            if (updateFlags (emailMessage, Flags, UserFlags)) {
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

        public static McEmailMessage ParseEmail (int accountId, string ServerId, MessageSummary summary)
        {
            NcAssert.NotNull (summary.Envelope);

            var emailMessage = new McEmailMessage () {
                ServerId = ServerId,
                AccountId = accountId,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = string.Empty,
                cachedFromColor = 1,
            };

            emailMessage.To = summary.Envelope.To.ToString ();
            emailMessage.Cc = summary.Envelope.Cc.ToString ();
            emailMessage.Bcc = summary.Envelope.Bcc.ToString ();

            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                emailMessage.From = summary.Envelope.From [0].ToString ();
                McEmailAddress fromEmailAddress;
                if (McEmailAddress.Get (accountId, summary.Envelope.From [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                    emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                    emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
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
                foreach (var header in summary.Headers) {
                    switch (header.Id) {
                    case HeaderId.ContentClass:
                        emailMessage.ContentClass = header.Value;
                        break;

                    case HeaderId.Importance:
                        // according to https://tools.ietf.org/html/rfc2156
                        //       importance      = "low" / "normal" / "high"
                        // But apparently I need to make sure to account for case (i.e. Normal and Low, etc).
                        switch (header.Value.ToLower ()) {
                        case "low":
                            emailMessage.Importance = NcImportance.Low_0;
                            break;

                        case "normal":
                            emailMessage.Importance = NcImportance.Normal_1;
                            break;

                        case "high":
                            emailMessage.Importance = NcImportance.High_2;
                            break;

                        default:
                            Log.Error (Log.LOG_IMAP, string.Format ("Unknown importance header value '{0}'", header.Value));
                            break;
                        }
                        break;

                    case HeaderId.DkimSignature:
                        break;
                    }
                }
            }

            if (summary.GMailThreadId.HasValue) {
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
            }
            if (string.Empty == emailMessage.MessageID && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }
            if (string.IsNullOrEmpty (emailMessage.ConversationId)) {
                emailMessage.ConversationId = System.Guid.NewGuid ().ToString ();
            }
            emailMessage.IsIncomplete = false;

            return emailMessage;
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

        private string getPreviewFromSummary (MessageSummary summary, IMailFolder mailKitFolder)
        {
            string preview = string.Empty;

            var part = findPreviewablePart (summary);
            if (null != part) {
                try {
                    int previewBytes = PreviewSizeBytes;
                    string partSpecifier = part.PartSpecifier;
                    ContentEncoding encoding = ContentEncoding.Default;
                    BodyPartBasic m = part as BodyPartBasic;
                    bool isPlainText = false; // when in doubt, run the http decode, just in case.
                    if (null != m) {
                        if (!MimeKit.Utils.MimeUtils.TryParse (m.ContentTransferEncoding, out encoding)) {
                            Log.Error (Log.LOG_IMAP, "Could not parse ContentTransferEncoding {0}", m.ContentTransferEncoding);
                            encoding = ContentEncoding.Default;
                        }
                        if (previewBytes >= m.Octets) {
                            previewBytes = (int)m.Octets;
                        }
                        if (string.Empty == m.PartSpecifier) {
                            partSpecifier = "TEXT";
                        } else if (m is BodyPartMessage) {
                            partSpecifier = m.PartSpecifier + ".TEXT";
                        }
                    } else {
                        Log.Warn (Log.LOG_IMAP, "BodyPart is not BodyPartBasic: {0}", part);
                    }
                    BodyPartText t = part as BodyPartText;
                    if (null != t) {
                        isPlainText = t.IsPlain;
                    }
                    Stream stream;
                    try {
                        stream = mailKitFolder.GetStream (summary.UniqueId.Value, partSpecifier, 0, previewBytes, Cts.Token);
                    } catch (ImapCommandException e) {
                        Log.Error (Log.LOG_IMAP, "Could not fetch stream: {0}", e);
                        return null;
                    }

                    preview = getTextFromStream (stream, part, encoding);
                    if (!isPlainText) {
                        var p = Html2Text (preview);
                        if (string.Empty == p) {
                            preview = string.Empty;
                        } else {
                            preview = p;
                        }
                    }
                } catch (ImapCommandException e) {
                    Log.Error (Log.LOG_IMAP, "{0}", e);
                }
            } else {
                Log.Error (Log.LOG_IMAP, "Could not find any previewable segments");
            }

            if (string.Empty == preview) {
                // This can happen if there's only attachments in the message.
                Log.Info (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId.Value);
            }
            return preview;
        }

        private BodyPart findPreviewablePart (MessageSummary summary)
        {
            BodyPart text;
            text = summary.BodyParts.OfType<BodyPartMessage> ().FirstOrDefault ();
            if (null == text) {
                var multipart = summary.Body as BodyPartMultipart;
                if (null != multipart) {
                    text = multipart.BodyParts.OfType<BodyPartMessage> ().FirstOrDefault ();
                }
            }
            if (null == text) {
                text = summary.TextBody ?? summary.HtmlBody;
            }
            return text;
        }

        private string getTextFromStream (Stream stream, BodyPart part, ContentEncoding enc)
        {
            using (var decoded = new MemoryStream ()) {
                using (var filtered = new FilteredStream (decoded)) {
                    filtered.Add (DecoderFilter.Create (enc));
                    if (part.ContentType.Charset != null) {
                        try {
                            filtered.Add (new CharsetFilter (part.ContentType.Charset, "utf-8"));
                        } catch (NotSupportedException ex) {
                            // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                            Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", part.ContentType.Charset, ex);
                            // continue without the filter
                        } catch (ArgumentException ex) {
                            // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                            Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", part.ContentType.Charset, ex);
                            // continue without the filter
                        }
                    }
                    stream.CopyTo (filtered);
                }
                var buffer = decoded.GetBuffer ();
                var length = (int)decoded.Length;
                return Encoding.UTF8.GetString (buffer, 0, length);
            }
        }
    }
}
