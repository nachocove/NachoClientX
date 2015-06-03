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

            public string body { get; set; }

            public McAbstrFileDesc.BodyTypeEnum bodyType { get; set; }
        }

        public ImapSyncCommand (IBEContext beContext, SyncKit syncKit) : base (beContext)
        {
            SyncKit = syncKit;
        }

        protected override Event ExecuteCommand ()
        {
            // FIXME - need map from McFolder to MailKit folder.
            List<MailSummary> summaries = new List<MailSummary> ();
            if (!Client.Inbox.IsOpen) {
                FolderAccess access;
                lock (Client.SyncRoot) {
                    access = Client.Inbox.Open (FolderAccess.ReadOnly, Cts.Token);
                }
                if (FolderAccess.None == access) {
                    return Event.Create ((uint)SmEvt.E.HardFail, "IMAPSYNCNOOPEN");
                }
            }
            switch (SyncKit.Method) {
            case SyncKit.MethodEnum.Range:
                lock (Client.SyncRoot) {
                    IList<IMessageSummary> imapSummaries = Client.Inbox.Fetch (
                        new UniqueIdRange (new UniqueId (Client.Inbox.UidValidity, SyncKit.Start),
                            new UniqueId (Client.Inbox.UidValidity, SyncKit.Start + SyncKit.Span)),
                                                                   SyncKit.Flags, Cts.Token);
                    foreach (var imapSummary in imapSummaries) {
                        string body;
                        McAbstrFileDesc.BodyTypeEnum bodyType;
                        var preview = getPreviewFromSummary (imapSummary as MessageSummary, Client.Inbox, out body, out bodyType);
                        summaries.Add (new MailSummary () {
                            imapSummary = imapSummary as MessageSummary,
                            preview = preview,
                            body = body,
                            bodyType = bodyType,
                        });
                    }
                }
                break;
            case SyncKit.MethodEnum.OpenOnly:
                // Just load UID with SELECT.
                SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapUidNext = Client.Inbox.UidNext.Value.Id;
                    target.ImapUidValidity = Client.Inbox.UidValidity;
                    return true;
                });
                return Event.Create ((uint)SmEvt.E.Success, "IMAPSYNCSUC");
            }
            if (null != summaries && 0 < summaries.Count) {
                foreach (var summary in summaries) {
                    // FIXME use NcApplyServerCommand framework.
                    var uniqueId = summary.imapSummary.UniqueId.Value.Id;
                    ServerSaysAddOrChangeEmail (summary, SyncKit.Folder);
                    if (uniqueId > SyncKit.Folder.ImapUidHighestUidSynced ||
                            uniqueId < SyncKit.Folder.ImapUidLowestUidSynced) {
                        SyncKit.Folder = SyncKit.Folder.UpdateWithOCApply<McFolder> ((record) => {
                            var target = (McFolder)record;
                            target.ImapUidHighestUidSynced = Math.Max (uniqueId, target.ImapUidHighestUidSynced);
                            target.ImapUidLowestUidSynced = Math.Min (uniqueId, target.ImapUidLowestUidSynced);
                            return true;
                        });
                    }
                }
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
            if (NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultInbox_2 == SyncKit.Folder.Type) {
                var protocolState = BEContext.ProtocolState;
                if (!protocolState.HasSyncedInbox) {
                    protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.HasSyncedInbox = true;
                        return true;
                    });
                }
            }
            if (SyncKit.isNarrow) {
                var protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.LastNarrowSync = DateTime.UtcNow;
                    return true;
                });
            }
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

//            if (null != summary.body) {
//                McBody body;
//                if (0 == emailMessage.BodyId) {
//                    body = McBody.InsertFile (folder.AccountId, summary.bodyType, summary.body); 
//                    emailMessage.BodyId = body.Id;
//                } else {
//                    body = McBody.QueryById<McBody> (emailMessage.BodyId);
//                    body.UpdateData (summary.body);
//                }
//                body.BodyType = summary.bodyType;
//                body.Truncated = false;
//                body.FilePresence = McAbstrFileDesc.FilePresenceEnum.Complete;
//                body.FileSize = summary.body.Length;
//                body.FileSizeAccuracy = McAbstrFileDesc.FileSizeAccuracyEnum.Actual;
//                body.Update ();
//
//            }

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

        private string getPreviewFromSummary (MessageSummary summary, IMailFolder folder, out string body, out McAbstrFileDesc.BodyTypeEnum bodyType)
        {
            string preview;

            preview = findPreviewText (summary, folder, out body, out bodyType);
            if (string.Empty != preview) {
                return preview;
            }

            if (string.Empty != preview) {
                Log.Info (Log.LOG_IMAP, "IMAP uid {0} preview <{1}>", summary.UniqueId.Value, preview);
            } else {
                Log.Error (Log.LOG_IMAP, "IMAP uid {0} Could not find Content to make preview from", summary.UniqueId.Value);
            }
            return preview;
        }

        private string findPreviewText (MessageSummary summary, IMailFolder folder, out string body, out McAbstrFileDesc.BodyTypeEnum bodyType)
        {
            string preview = string.Empty;
            body = null;
            bodyType = McAbstrFileDesc.BodyTypeEnum.None;

            bool isPlain;
            var text = findPreviewablePart (summary, out isPlain);
            if (null != text) {
                try {
                    int previewBytes;
                    if (text.Octets <= 4096) {
                        previewBytes = (int)text.Octets;
                    } else {
                        previewBytes = isPlain ? PreviewSizeBytes : PreviewSizeBytes * 4;
                    }
                    Stream stream;
                    string partSpecifier = text.PartSpecifier;
                    if (string.Empty == partSpecifier) {
                        partSpecifier = "TEXT";
                    } else {
                        partSpecifier += ".TEXT";
                    }
                    try {
                        stream = folder.GetStream (summary.UniqueId.Value, partSpecifier, 0, previewBytes, Cts.Token);
                    } catch (ImapCommandException e) {
                        Log.Warn (Log.LOG_IMAP, "Need to adjust partSpecifier for part {0} {1} {2}", text.PartSpecifier, text.ContentTransferEncoding, e);
                        stream = folder.GetStream (summary.UniqueId.Value, text.PartSpecifier, 0, previewBytes, Cts.Token);
                    }
                    ContentEncoding encoding;
                    if (!MimeKit.Utils.MimeUtils.TryParse(text.ContentTransferEncoding, out encoding)) {
                        Log.Error (Log.LOG_IMAP, "findPreviewText: Could not parse ContentTransferEncoding {0}", text.ContentTransferEncoding);
                        encoding = ContentEncoding.Default;
                    }
                    preview = getTextFromStream (stream, text.ContentType, encoding);
                    if (text.Octets <= 4096) {
                        body = preview;
                        preview = body.Substring (0, Math.Min (PreviewSizeBytes, body.Length));
                        bodyType = isPlain ? McAbstrFileDesc.BodyTypeEnum.PlainText_1 : McAbstrFileDesc.BodyTypeEnum.HTML_2;
                    }
                    if (!isPlain) {
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
            return preview;
        }

        private BodyPartText findPreviewablePart (MessageSummary summary, out bool isPlaintext)
        {
            Log.Info (Log.LOG_IMAP, "Finding text bodypart for uid {0}", summary.UniqueId.Value.Id);
            BodyPartText text;
            text = summary.TextBody;
            if (null == text) {
                text = summary.HtmlBody;
            }
            if (null == text) {
                text = summary.Body as BodyPartText;
            }
            if (null == text) {
                var multipart = summary.Body as BodyPartMultipart;
                if (null != multipart) {
                    text = multipart.BodyParts.OfType<BodyPartText> ().FirstOrDefault ();
                }
            }
            if (null != text) {
                isPlaintext = text.IsPlain;
            } else {
                isPlaintext = true;
            }
            return text;
        }

        private string getTextFromStream (Stream stream, ContentType type, ContentEncoding enc)
        {
            using (var decoded = new MemoryStream ()) {
                using (var filtered = new FilteredStream (decoded)) {
                    filtered.Add (DecoderFilter.Create (enc));
                    if (type.Charset != null)
                        filtered.Add (new CharsetFilter (type.Charset, "utf-8"));
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
