//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NachoCore.Utils;
using System.Threading;
using MimeKit;
using MailKit;
using MailKit.Search;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using MailKit.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using MimeKit.IO;
using MimeKit.IO.Filters;
using HtmlAgilityPack;

namespace NachoCore.IMAP
{
    public class ImapSyncCommand : ImapCommand
    {
        SyncKit SyncKit;
        private const int PreviewSizeBytes = 500;

        public class MailSummary
        {
            public MessageSummary imapSummary { get; set; }

            public string preview { get; set; }
        }

        public ImapSyncCommand (IBEContext beContext, SyncKit syncKit) : base (beContext)
        {
            SyncKit = syncKit;
        }

        private IMailFolder GetOpenMailkitFolder(McFolder folder)
        {
            IMailFolder mailKitFolder;
            FolderAccess access;
            mailKitFolder = Client.GetFolder (folder.ServerId);
            if (null == mailKitFolder) {
                return null;
            }
            access = mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
            if (FolderAccess.None == access) {
                return null;
            }
            return mailKitFolder;
        }

        protected override Event ExecuteCommand ()
        {
            var sw = new PlatformStopwatch ();
            IMailFolder mailKitFolder;
            List<MailSummary> summaries = new List<MailSummary> ();
            switch (SyncKit.Method) {
            case SyncKit.MethodEnum.Range:
                IList<IMessageSummary> imapSummaries = null;

                lock (Client.SyncRoot) {
                    mailKitFolder = GetOpenMailkitFolder (SyncKit.Folder);
                    if (null == mailKitFolder) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN1");
                    }
                    sw.Start ();
                    imapSummaries = mailKitFolder.Fetch (
                        new UniqueIdRange (new UniqueId (mailKitFolder.UidValidity, SyncKit.Start),
                            new UniqueId (mailKitFolder.UidValidity, SyncKit.Start + SyncKit.Span)),
                        SyncKit.Flags, Cts.Token);
                    sw.Stop ();
                    Log.Info (Log.LOG_IMAP, "Retrieved {0} summaries in {1}ms", imapSummaries.Count, sw.ElapsedMilliseconds);
                }
                sw.Start ();
                foreach (var imapSummary in imapSummaries) {
                    var preview = getPreviewFromSummary (imapSummary as MessageSummary, mailKitFolder);
                    summaries.Add (new MailSummary () {
                        imapSummary = imapSummary as MessageSummary,
                        preview = preview,
                    });
                }
                sw.Stop ();
                Log.Info (Log.LOG_IMAP, "Retrieved {0} previews in {1}ms", imapSummaries.Count, sw.ElapsedMilliseconds);
                break;
            case SyncKit.MethodEnum.OpenOnly:
                // Just load UID with SELECT.
                IList<UniqueId> uids;
                lock (Client.SyncRoot) {
                    mailKitFolder = GetOpenMailkitFolder (SyncKit.Folder);
                    if (null == mailKitFolder) {
                        return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN2");
                    }
                    sw.Start ();
                    var query = SearchQuery.NotDeleted;
                    uids = mailKitFolder.Search (query);  // FIXME This does a 1:* search. We'll need to narrow that down in case the mailbox is huge.
                    sw.Stop ();
                    Log.Info (Log.LOG_IMAP, "Retrieved search all non-deleted messages in {0}ms. Found {1} uids", sw.ElapsedMilliseconds, uids.Count);
                }
                SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapUidNext = mailKitFolder.UidNext.Value.Id;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    target.ImapLowestUid = (0 == uids.Count) ? 1 : uids.Min ().Id;
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
            }
            if (null != summaries && 0 < summaries.Count) {
                uint maxUid = UInt32.MinValue;
                uint minUid = UInt32.MaxValue;
                foreach (var summary in summaries) {
                    // FIXME use NcApplyServerCommand framework.
                    ServerSaysAddOrChangeEmail (summary, SyncKit.Folder);
                    var uniqueId = summary.imapSummary.UniqueId.Value.Id;
                    maxUid = Math.Max (maxUid, uniqueId);
                    minUid = Math.Min (minUid, uniqueId);
                }
                SyncKit.Folder = SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapUidHighestUidSynced = Math.Max (maxUid, target.ImapUidHighestUidSynced);
                    target.ImapUidLowestUidSynced = Math.Min (minUid, target.ImapUidLowestUidSynced);
                    return true;
                });
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
            } else {
                // All the messages could be deleted on the server. Record UIDs of the dead spot to keep from looping.
                SyncKit.Folder = SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapUidHighestUidSynced = Math.Max (SyncKit.Start + SyncKit.Span, target.ImapUidHighestUidSynced);
                    target.ImapUidLowestUidSynced = Math.Min (SyncKit.Start, target.ImapUidLowestUidSynced);
                    return true;
                });
            }
            var protocolState = BEContext.ProtocolState;
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == SyncKit.Folder.Type) {
                if (!protocolState.HasSyncedInbox) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.HasSyncedInbox = true;
                        return true;
                    });
                }
            }
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.LastNarrowSync = DateTime.UtcNow;
                return true;
            });
            return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
        }

        public McEmailMessage ServerSaysAddOrChangeEmail (MailSummary summary, McFolder folder)
        {
            if (null == summary.imapSummary.UniqueId || string.Empty == summary.imapSummary.UniqueId.Value.ToString ()) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: No Summary ServerId present.");
                return null;
            }

            string McEmailMessageServerId = ImapProtoControl.MessageServerId(folder, summary.imapSummary.UniqueId.Value.ToString ());

            // If the server attempts to overwrite, delete the pre-existing record first.
            var eMsg = McEmailMessage.QueryByServerId<McEmailMessage> (folder.AccountId, McEmailMessageServerId);
            if (null != eMsg) {
                eMsg.Delete ();
                eMsg = null;
            }

            McEmailMessage emailMessage = null;
            try {
                var r = ParseEmail (McEmailMessageServerId, summary.imapSummary);
                emailMessage = r.GetValue<McEmailMessage> ();
                emailMessage.BodyPreview = summary.preview;
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "ServerSaysAddOrChangeEmail: Exception parsing: {0}", ex.ToString ());
                if (null == emailMessage || null == emailMessage.ServerId || string.Empty == emailMessage.ServerId) {
                    emailMessage = new McEmailMessage () {
                        ServerId = McEmailMessageServerId,
                    };
                }
                emailMessage.IsIncomplete = true;
            }

            // TODO move the rest to parent class or into the McEmailAddress class before insert or update?
            NcModel.Instance.RunInTransaction (() => {
                if ((0 != emailMessage.FromEmailAddressId) || !String.IsNullOrEmpty (emailMessage.To)) {
                    if (!folder.IsJunkFolder ()) {
                        NcContactGleaner.GleanContactsHeaderPart1 (emailMessage);
                    }
                }

                bool justCreated = false;
                if (null == eMsg) {
                    justCreated = true;
                    emailMessage.AccountId = folder.AccountId;
                }
                if (justCreated) {
                    emailMessage.Insert ();
                    folder.Link (emailMessage);
                    // FIXME
                    // InsertAttachments (emailMessage);
                } else {
                    emailMessage.AccountId = folder.AccountId;
                    emailMessage.Id = eMsg.Id;
                    folder.UpdateLink (emailMessage);
                    emailMessage.Update ();
                }
            });

            if (!emailMessage.IsIncomplete) {
                // Extra work that needs to be done, but doesn't need to be in the same database transaction.
            }

            return emailMessage;
        }

        public NcResult ParseEmail (string ServerId, IMessageSummary summary)
        {
            var emailMessage = new McEmailMessage () {
                ServerId = ServerId,
                AccountId = BEContext.Account.Id,
                Subject = summary.Envelope.Subject,
                InReplyTo = summary.Envelope.InReplyTo,
                // FIXME - Any error.
                // cachedHasAttachments = summary.Attachments.Any (),
                MessageID = summary.Envelope.MessageId,
                DateReceived = summary.InternalDate.HasValue ? summary.InternalDate.Value.UtcDateTime : DateTime.MinValue,
                FromEmailAddressId = 0,
                cachedFromLetters = "",
                cachedFromColor = 1,
            };

            // TODO: DRY this out. Perhaps via Reflection?
            if (summary.Envelope.To.Count > 0) {
                if (summary.Envelope.To.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} To entries in message.", summary.Envelope.To.Count);
                }
                emailMessage.To = ((MailboxAddress)summary.Envelope.To [0]).Address;
            }
            if (summary.Envelope.Cc.Count > 0) {
                if (summary.Envelope.Cc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Cc entries in message.", summary.Envelope.Cc.Count);
                }
                emailMessage.Cc = ((MailboxAddress)summary.Envelope.Cc [0]).Address;
            }
            if (summary.Envelope.Bcc.Count > 0) {
                if (summary.Envelope.Bcc.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Bcc entries in message.", summary.Envelope.Bcc.Count);
                }
                emailMessage.Bcc = ((MailboxAddress)summary.Envelope.Bcc [0]).Address;
            }

            McEmailAddress fromEmailAddress;
            if (summary.Envelope.From.Count > 0) {
                if (summary.Envelope.From.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} From entries in message.", summary.Envelope.From.Count);
                }
                emailMessage.From = ((MailboxAddress)summary.Envelope.From [0]).Address;
                if (McEmailAddress.Get (BEContext.Account.Id, summary.Envelope.From [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.FromEmailAddressId = fromEmailAddress.Id;
                    emailMessage.cachedFromLetters = EmailHelper.Initials (emailMessage.From);
                    emailMessage.cachedFromColor = fromEmailAddress.ColorIndex;
                }
            }

            if (summary.Envelope.ReplyTo.Count > 0) {
                if (summary.Envelope.ReplyTo.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} ReplyTo entries in message.", summary.Envelope.ReplyTo.Count);
                }
                emailMessage.ReplyTo = ((MailboxAddress)summary.Envelope.ReplyTo [0]).Address;
            }
            if (summary.Envelope.Sender.Count > 0) {
                if (summary.Envelope.Sender.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} Sender entries in message.", summary.Envelope.Sender.Count);
                }
                emailMessage.Sender = ((MailboxAddress)summary.Envelope.Sender [0]).Address;
                if (McEmailAddress.Get (BEContext.Account.Id, summary.Envelope.Sender [0] as MailboxAddress, out fromEmailAddress)) {
                    emailMessage.SenderEmailAddressId = fromEmailAddress.Id;
                }
            }
            if (null != summary.References && summary.References.Count > 0) {
                if (summary.References.Count > 1) {
                    Log.Error (Log.LOG_IMAP, "Found {0} References entries in message.", summary.References.Count);
                }
                emailMessage.References = summary.References [0];
            }

            if (summary.Flags.HasValue) {
                if (summary.Flags.Value != MessageFlags.None) {
                    if ((summary.Flags.Value & MessageFlags.Seen) == MessageFlags.Seen) {
                        emailMessage.IsRead = true;
                    }
                    // FIXME Where do we set these flags?
                    if ((summary.Flags.Value & MessageFlags.Answered) == MessageFlags.Answered) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Flagged) == MessageFlags.Flagged) {
                        emailMessage.UserAction = 1;
                    }
                    if ((summary.Flags.Value & MessageFlags.Deleted) == MessageFlags.Deleted) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Draft) == MessageFlags.Draft) {
                    }
                    if ((summary.Flags.Value & MessageFlags.Recent) == MessageFlags.Recent) {
                    }
                    if ((summary.Flags.Value & MessageFlags.UserDefined) == MessageFlags.UserDefined) {
                        // FIXME See if these are handled by the summary.UserFlags
                    }
                }
            }
            if (null != summary.UserFlags && summary.UserFlags.Count > 0) {
                // FIXME Where do we set these flags?
            }

            if (null != summary.Headers) {
                foreach (var header in summary.Headers) {
                    Log.Info (Log.LOG_IMAP, "IMAP header id {0} {1} {2}", header.Id, header.Field, header.Value);
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
                    }
                }
            }

            if (summary.GMailThreadId.HasValue) {
                emailMessage.ConversationId = summary.GMailThreadId.Value.ToString ();
            }
            if ("" == emailMessage.MessageID && summary.GMailMessageId.HasValue) {
                emailMessage.MessageID = summary.GMailMessageId.Value.ToString ();
            }
            emailMessage.IsIncomplete = false;

            return NcResult.OK (emailMessage);
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
                            Log.Warn (Log.LOG_IMAP, "Html-converted preview is empty. Source {0}", preview);
                            preview = "(No Preview available)";
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

            if (string.Empty != preview) {
                Log.Info (Log.LOG_IMAP, "IMAP uid {0} preview <{1}>", summary.UniqueId.Value, preview);
            } else {
                Log.Error (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId.Value);
            }
            return preview;
        }

        private BodyPart findPreviewablePart (MessageSummary summary)
        {
            Log.Info (Log.LOG_IMAP, "Finding text bodypart for uid {0}", summary.UniqueId.Value.Id);
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
                    if (part.ContentType.Charset != null)
                        filtered.Add (new CharsetFilter (part.ContentType.Charset, "utf-8"));
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
