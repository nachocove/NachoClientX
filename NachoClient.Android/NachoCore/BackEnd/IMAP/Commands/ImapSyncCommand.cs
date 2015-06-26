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
using HtmlAgilityPack;
using MailKit.Search;
using System.Text.RegularExpressions;

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapCommand
    {
        SyncKit Synckit;
        private const int PreviewSizeBytes = 500;
        private List<Regex> RegexList;

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
            //RedactProtocolLogFunc = RedactProtocolLog;
            RegexList = new List<Regex> ();

            //* 59 FETCH (UID 8721 MODSEQ (952121) BODY[1]<0> {500} ... )
            RegexList.Add (new Regex (@"^(?<star>\* )(?<num>\d+ )(?<cmd>FETCH )(?<openparen>\()(?<stuff>[^\n]+)(?<redact>.*)(?<closeparen>\))$", NcMailKitProtocolLogger.rxOptions));

            //* 38 FETCH (X-GM-THRID 1503699202635470816 X-GM-MSGID 1503699202635470816 UID 8695 RFC822.SIZE 64686 MODSEQ (950792) INTERNALDATE "11-Jun-2015 16:15:08 +0000" FLAGS () ENVELOPE ("Thu, 11 Jun 2015 16:15:02 +0000" "test with attachment" (("Jan Vilhuber" NIL "janv" "nachocove.com")) (("Jan Vilhuber" NIL "janv" "nachocove.com")) (("Jan Vilhuber" NIL "janv" "nachocove.com")) (("Jan Vilhuber" NIL "jan.vilhuber" "gmail.com")) NIL NIL NIL "<C4E2D584-AC73-492F-B08B-D0FA8A12929E@nachocove.com>") BODYSTRUCTURE ((2015-06-17T23:20:14.541Z: IMAP S: "TEXT" "PLAIN" ("CHARSET" "us-ascii") NIL NIL "QUOTED-PRINTABLE" 0 0 NIL NIL NIL)("IMAGE" "PNG" ("NAME" "Screen Shot 2015-06-10 at 10.13.12 AM.png") "<9A84A7CB1408CC4A96BF4CB3CC02846B@prod.exchangelabs.com>" "Screen Shot 2015-06-10 at 10.13.12 AM.png" "BASE64" 59598 NIL ("ATTACHMENT" ("CREATION-DATE" "Thu, 11 Jun 2015 16:15:02 GMT" "FILENAME" "Screen Shot 2015-06-10 at 10.13.12 AM.png" "MODIFICATION-DATE" "Thu, 11 Jun 2015 16:15:02 GMT" "SIZE" "43549")) NIL) "MIXED" ("BOUNDARY" "_002_C4E2D584AC73492FB08BD0FA8A12929Enachocovecom_") NIL NIL) BODY[HEADER.FIELDS (IMPORTANCE DKIM-SIGNATURE CONTENT-CLASS)] {2}
            // Need to redact the entire Envelope and BODYSTRUCTURE filenames

            NcCapture.AddKind (KImapFetchTiming);
            NcCapture.AddKind (KImapPreviewGeneration);
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            return NcMailKitProtocolLogger.RedactLogDataRegex (RegexList, logData);
        }

        protected override Event ExecuteCommand ()
        {
            IMailFolder mailKitFolder;

            var timespan = BEContext.Account.DaysSyncEmailSpan ();
            mailKitFolder = GetOpenMailkitFolder (Synckit.Folder);
            if (null == mailKitFolder) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN2");
            }
            if (UInt32.MinValue != Synckit.Folder.ImapUidValidity &&
                Synckit.Folder.ImapUidValidity != mailKitFolder.UidValidity) {
                return Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPSYNCUIDINVAL");
            }

            switch (Synckit.Method) {
            case SyncKit.MethodEnum.OpenOnly:
                return getFolderMetaDataInternal (mailKitFolder, timespan);

            case SyncKit.MethodEnum.Sync:
                return syncFolder (mailKitFolder, timespan);

            default:
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCHARDCASE");
            }
        }

        private Event getFolderMetaDataInternal (IMailFolder mailKitFolder, TimeSpan timespan)
        {
            if (!GetFolderMetaData(ref Synckit.Folder, mailKitFolder, timespan)) {
                return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCMETAFAIL");
            } else {
                return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCOPENSUC");
            }
        }

        public static bool GetFolderMetaData (ref McFolder folder, IMailFolder mailKitFolder, TimeSpan timespan)
        {
            // Just load UID with SELECT.
            Log.Info (Log.LOG_IMAP, "ImapSyncCommand {0}: Getting Folderstate", folder.ImapFolderNameRedacted());

            var query = SearchQuery.NotDeleted;
            if (TimeSpan.Zero != timespan) {
                query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
            }
            UniqueIdSet uids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query));
            Log.Info (Log.LOG_IMAP, "{1}: Uids from last {2} days: {0}",
                uids.ToString (),
                folder.ImapFolderNameRedacted(), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());

            query = SearchQuery.Deleted;
            if (TimeSpan.Zero != timespan) {
                query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
            }
            var deletedUids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query));
            Log.Info (Log.LOG_IMAP, "{1}: DeletedUids from last {2} days: {0}",
                deletedUids.ToString (),
                folder.ImapFolderNameRedacted(), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());
            UpdateImapSetting (mailKitFolder, ref folder);

            var highestUid = new UniqueId (mailKitFolder.UidNext.Value.Id - 1);
            if (uids.Any () && !uids.Contains(highestUid))
            {
                // need to artificially add this to the set, otherwise we'll loop forever if there's a hole at the top.
                uids.Add (highestUid);
            }
            // FIXME: Alternatively, perhaps we can store this in SyncKit and pass the synckit back to strategy somehow.
            // TODO Store only 1000, but can we (easily) do that in a set? Or do we need to convert to List?
            folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidSet = uids.ToString ();
                if (string.IsNullOrEmpty (target.ImapLastUidSet)) {
                    target.ImapLastUidSet = target.ImapUidSet;
                }
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
            if (deleted.Any ()) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            if (toDelete.Any ()) {
                // Remove the deleted from the ImapLastUidSet so we don't try to delete them again and loop forever
                // TODO Need a better way to know when to just copy over ImapUidSet to ImapLastUidSet
                UniqueIdSet last;
                NcAssert.True (UniqueIdSet.TryParse (Synckit.Folder.ImapLastUidSet, out last));
                Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                    McFolder target = (McFolder)record;
                    Synckit.Folder.ImapLastUidSet = SyncKit.MustUniqueIdSet (last.Except (toDelete).ToList ()).ToString ();
                    return true;
                });

            }

            if (!deleted.Any () && !newOrChanged.Any ()) {
                Log.Info (Log.LOG_IMAP, "Nothing was done.");
            }

            // remember where we sync'd
            uint MaxSynced = Synckit.SyncSet.Max ().Id;
            uint MinSynced = Synckit.SyncSet.Min ().Id;
            Synckit.Folder = Synckit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                var target = (McFolder)record;
                target.ImapUidHighestUidSynced = Math.Max (MaxSynced, target.ImapUidHighestUidSynced);
                target.ImapUidLowestUidSynced = Math.Min (MinSynced, target.ImapUidLowestUidSynced);
                target.ImapLastUidSynced = MinSynced;
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

            // mark the pending as resolved.
            PendingResolveApply ((pending) => {
                pending.ResolveAsSuccess (BEContext.ProtoControl, 
                    NcResult.Info (NcResult.SubKindEnum.Info_SyncSucceeded));
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
        }

        private UniqueIdSet GetNewOrChangedMessages(IMailFolder mailKitFolder, UniqueIdSet uidset, out UniqueIdSet vanished)
        {
            Log.Info (Log.LOG_IMAP, "ImapSyncCommand {0}: Getting Message summaries {1}", Synckit.Folder.ImapFolderNameRedacted (), uidset.ToString ());

            NcCapture cap;
            IList<IMessageSummary> imapSummaries = getMessageSummaries (mailKitFolder, uidset);

            List<MailSummary> summaries = new List<MailSummary> ();
            if (imapSummaries.Any ()) {
                cap = NcCapture.CreateAndStart (KImapPreviewGeneration);
                foreach (var imapSummary in imapSummaries) {
                    if (imapSummary.Flags.Value.HasFlag (MessageFlags.Deleted)) {
                        continue;
                    }
                    var preview = getPreviewFromSummary (imapSummary as MessageSummary, mailKitFolder);
                    summaries.Add (new MailSummary () {
                        imapSummary = imapSummary as MessageSummary,
                        preview = preview,
                    });
                }
                cap.Stop ();
                Log.Info (Log.LOG_IMAP, "Retrieved {0} previews in {1}ms", imapSummaries.Count, cap.ElapsedMilliseconds);
            }
            bool somethingChanged = false;
            UniqueIdSet summaryUids = new UniqueIdSet ();
            foreach (var summary in summaries) {
                // FIXME use NcApplyServerCommand framework.
                ServerSaysAddOrChangeEmail (BEContext.Account.Id, summary, Synckit.Folder);
                somethingChanged = true;
                summaryUids.Add (summary.imapSummary.UniqueId.Value);
            }
            if (somethingChanged) {
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            }
            vanished = SyncKit.MustUniqueIdSet (uidset.Except (summaryUids).ToList ());
            return summaryUids;
        }

        private IList<IMessageSummary> getMessageSummaries (IMailFolder mailKitFolder, UniqueIdSet uidset)
        {
            NcCapture cap;
            IList<IMessageSummary> imapSummaries = null;
            try {
                cap = NcCapture.CreateAndStart (KImapFetchTiming);
                HashSet<HeaderId> headers = new HashSet<HeaderId> ();
                headers.Add (HeaderId.Importance);
                headers.Add (HeaderId.DkimSignature);
                headers.Add (HeaderId.ContentClass);

                imapSummaries = mailKitFolder.Fetch (uidset, Synckit.Flags, headers, Cts.Token);
                cap.Stop ();
                Log.Info (Log.LOG_IMAP, "Retrieved {0} summaries in {1}ms", imapSummaries.Count, cap.ElapsedMilliseconds);
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

        public static McEmailMessage ServerSaysAddOrChangeEmail (int accountId, MailSummary summary, McFolder folder)
        {
            if (null == summary.imapSummary.UniqueId || string.Empty == summary.imapSummary.UniqueId.Value.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No Summary ServerId present.");
                return null;
            }

            string McEmailMessageServerId = ImapProtoControl.MessageServerId(folder, summary.imapSummary.UniqueId.Value);
            bool justCreated = false;
            McEmailMessage emailMessage = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, McEmailMessageServerId);
            if (null != emailMessage) {
                try {
                    updateFlags (emailMessage, summary.imapSummary.Flags.GetValueOrDefault (), summary.imapSummary.UserFlags);
                } catch (Exception ex) {
                    Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception updating: {0}", ex.ToString ());
                }
            } else {
                try {
                    emailMessage = ParseEmail (accountId, McEmailMessageServerId, summary.imapSummary);
                    updateFlags (emailMessage, summary.imapSummary.Flags.GetValueOrDefault (), summary.imapSummary.UserFlags);
                    emailMessage.BodyPreview = summary.preview;
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
                    // FIXME
                    // InsertAttachments (emailMessage);
                } else {
                    emailMessage.AccountId = folder.AccountId;
                    emailMessage.Update ();
                }
            });

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
            }

            return emailMessage;
        }

        private UniqueIdSet FindDeletedUids(IMailFolder mailKitFolder, UniqueIdSet uids)
        {
            // Check for deleted messages
            SearchQuery query = SearchQuery.Deleted;
            UniqueIdSet messagesDeleted = SyncKit.MustUniqueIdSet (mailKitFolder.Search (uids, query));
            return messagesDeleted;
        }

        private UniqueIdSet deleteEmails(UniqueIdSet uids)
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

        private static string CommaSeparatedString(InternetAddressList AddrList)
        {
            string result = null;
            if (AddrList.Any ()) {
                var addrs = new List<string> ();
                foreach (var addr in AddrList) {
                    addrs.Add (addr.ToString ());
                }
                result = string.Join (",", addrs);
            }
            return result;
        }

        public static bool UpdateEmailMetaData(McEmailMessage emailMessage, IMessageSummary summary)
        {
            if (!summary.Flags.HasValue) {
                Log.Error (Log.LOG_IMAP, "Trying to update email message without any flags");
                return false;
            }
            return UpdateEmailMetaData (emailMessage, summary.Flags.Value, summary.UserFlags);
        }

        public static bool UpdateEmailMetaData(McEmailMessage emailMessage, MessageFlags Flags, HashSet<string> UserFlags)
        {
            bool changed = false;

            // IMAP can only update flags. Anything else is a new UID/message.
            if (updateFlags (emailMessage, Flags, UserFlags)) {
                changed = true;
            }
            return changed;
        }

        private static bool updateFlags(McEmailMessage emailMessage, MessageFlags Flags, HashSet<string> UserFlags)
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

        public static McEmailMessage ParseEmail (int accountId, string ServerId, IMessageSummary summary)
        {
            var emailMessage = new McEmailMessage () {
                ServerId = ServerId,
                AccountId = accountId,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                // FIXME - Any error.
                // cachedHasAttachments = summary.Attachments.Any (),
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = string.Empty,
                cachedFromColor = 1,
            };

            emailMessage.To = CommaSeparatedString (summary.Envelope.To);
            emailMessage.Cc = CommaSeparatedString (summary.Envelope.Cc);
            emailMessage.Bcc = CommaSeparatedString (summary.Envelope.Bcc);

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
                        switch (header.Value) {
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

        private string getPreviewFromSummary (MessageSummary summary, IMailFolder folder)
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
                        if (!MimeKit.Utils.MimeUtils.TryParse(m.ContentTransferEncoding, out encoding)) {
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
                        stream = folder.GetStream (summary.UniqueId.Value, partSpecifier, 0, previewBytes, Cts.Token);
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

        private string Html2Text (string html)
        {
            HtmlDocument doc = new HtmlDocument ();
            doc.LoadHtml (html);

            StringWriter sw = new StringWriter ();
            ConvertTo (doc.DocumentNode, sw);
            sw.Flush ();
            return sw.ToString ();
        }

        public void ConvertTo (HtmlNode node, TextWriter outText)
        {
            string html;
            switch (node.NodeType) {
            case HtmlNodeType.Comment:
                // don't output comments
                break;

            case HtmlNodeType.Document:
                ConvertContentTo (node, outText);
                break;

            case HtmlNodeType.Text:
                // script and style must not be output
                string parentName = node.ParentNode.Name;
                if ((parentName == "script") || (parentName == "style"))
                    break;

                // get text
                html = ((HtmlTextNode)node).Text;

                // is it in fact a special closing node output as text?
                if (HtmlNode.IsOverlappedClosingElement (html))
                    break;

                // check the text is meaningful and not a bunch of whitespaces
                if (html.Trim ().Length > 0) {
                    outText.Write (HtmlEntity.DeEntitize (html));
                }
                break;

            case HtmlNodeType.Element:
                switch (node.Name) {
                case "p":
                    // treat paragraphs as crlf
                    outText.Write ("\r\n");
                    break;
                }

                if (node.HasChildNodes) {
                    ConvertContentTo (node, outText);
                }
                break;
            }
        }

        private void ConvertContentTo (HtmlNode node, TextWriter outText)
        {
            foreach (HtmlNode subnode in node.ChildNodes) {
                ConvertTo (subnode, outText);
            }
        }
    }
}
