//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Model;
using NachoPlatform;
using HtmlAgilityPack;
using MimeKit;

namespace NachoCore.Utils
{

    public interface MessageComposerDelegate
    {
        void MessageComposerDidCompletePreparation (MessageComposer composer);
    }

    public class MessageComposer : MessageDownloadDelegate
    {

        #region Properties

        public MessageComposerDelegate Delegate;
        public readonly McAccount Account;
        public McEmailMessage Message;
        public McEmailMessageThread RelatedThread;
        public McCalendar RelatedCalendarItem;
        public EmailHelper.Action Kind = EmailHelper.Action.Send;
        public List<McAttachment> InitialAttachments;
        public string InitialText;
        public NcEmailMessageBundle Bundle {
            get {
                if (_Bundle == null) {
                    if (Message != null) {
                        _Bundle = new NcEmailMessageBundle (Message);
                    }
                }
                return _Bundle;
            }
        }
        public bool IsMessagePrepared {
            get {
                return MessagePreparationState == MessagePreparationStatus.Done;
            }
        }

        private McEmailMessage RelatedMessage;

        enum MessagePreparationStatus {
            NotStarted,
            Preparing,
            Done
        };

        NcEmailMessageBundle _Bundle;
        MessagePreparationStatus MessagePreparationState = MessagePreparationStatus.NotStarted;
        MessageDownloader MainMessageDownloader;
        MessageDownloader RelatedMessageDownloader;

        #endregion

        #region Constructors

        public MessageComposer (McAccount account)
        {
            Account = account;
        }

        #endregion

        #region Prepare Message for Compose

        public void StartPreparingMessage ()
        {
            if (MessagePreparationState != MessagePreparationStatus.NotStarted) {
                return;
            }
            MessagePreparationState = MessagePreparationStatus.Preparing;
            // We can be given a Message beforehand, but it's not required.
            // So if we weren't given a message, create a new completely blank one before proceeding.
            if (Message == null) {
                Message = McEmailMessage.MessageWithSubject (Account, "");
            }
            if (Message.Id == 0) {
                // If the message we were handed (or we created) has not been saved to the database yet,
                // then we're in a "New Message" scenario.
                PrepareNewMessage ();
            } else {
                // If the message has been saved, then we're re-opening a draft message
                PrepareSavedMessage ();
            }
        }

        private void PrepareNewMessage ()
        {
            // For a new message, we need to first save it to the database so we have an Id
            // that will be referenced by the bundle, attachments, etc.
            if (RelatedThread != null) {
                RelatedMessage = RelatedThread.FirstMessageSpecialCase ();
                Message.ReferencedEmailId = RelatedMessage.Id;
                Message.ReferencedIsForward = Kind == EmailHelper.Action.Forward;
                Message.ReferencedBodyIsIncluded = true;
                Message.Subject = EmailHelper.CreateInitialSubjectLine (Kind, RelatedMessage.Subject);
                if (EmailHelper.IsReplyAction (Kind)) {
                    EmailHelper.PopulateMessageRecipients (Account, Message, Kind, RelatedMessage);
                }
                // FIXME: was causing an error
                //                var now = DateTime.UtcNow;
                //                NcBrain.MessageReplyStatusUpdated (RelatedMessage, now, 0.1);
            }
            var mailbox = new MailboxAddress (Pretty.UserNameForAccount (Account), Account.EmailAddr);
            Message.From = mailbox.ToString ();
            Message.Insert ();
            EmailHelper.SaveEmailMessageInDrafts (Message);

            // Now we need to start building the initial message.  It could be blank, it
            // could inlude a quick reply, or it could be quoting another message.
            if (RelatedThread != null) {
                if (Kind == EmailHelper.Action.Forward) {
                    // FIXME: we may want to skip inline attachments here...gotta put everything together and test
                    // to see what works best
                    CopyAttachments (McAttachment.QueryByItemId (RelatedMessage));
                }
                DownloadRelatedMessage ();
            } else {
                // There may be attachments we need to copy
                if (InitialAttachments != null) {
                    CopyAttachments (InitialAttachments);
                }
                PrepareMessageBody ();
            }
        }

        private void PrepareSavedMessage ()
        {
            // This is essentially a draft message, so there's no need to do anything
            // but load & display the contents that were saved.
            if (Bundle.NeedsUpdate) {
                MainMessageDownloader = new MessageDownloader ();
                MainMessageDownloader.Delegate = this;
                MainMessageDownloader.Bundle = Bundle;
                MainMessageDownloader.Download (Message);
            } else {
                FinishPreparingMessage ();
            }
        }

        private void CopyAttachments (List<McAttachment> attachments)
        {
            foreach (var attachment in attachments){
                CopyAttachment (attachment);
            }
        }

        private void CopyAttachment (McAttachment attachment)
        {
            // AttachmentHelper has a similar method to this one, but that file is iOS-only.
            // Plus, I'd like to eventually split some work out into tasks (see below)
            // And, by setting ItemId here (which AttachmentHelper doesn't do), we can save another Update() call
            var copy = new McAttachment () {
                AccountId = Message.AccountId,
                ItemId = Message.Id,
                ClassCode = McAbstrFolderEntry.ClassCodeEnum.Email,
                ContentId = attachment.ContentId,
                ContentType = attachment.ContentType,
            };
            copy.Insert ();
            // TODO: It would be nice to do any heavy work like large file copying in a background task.
            // I'm not familiar enough with the details to set that up quite yet.
            copy.SetDisplayName (attachment.DisplayName);
            copy.UpdateFileCopy (attachment.GetFilePath ());
            copy.Update ();
        }

        private void DownloadRelatedMessage ()
        {
            var relatedBundle = new NcEmailMessageBundle (RelatedMessage);
            if (relatedBundle.NeedsUpdate) {
                RelatedMessageDownloader = new MessageDownloader ();
                RelatedMessageDownloader.Delegate = this;
                RelatedMessageDownloader.Bundle = relatedBundle;
                RelatedMessageDownloader.Download (RelatedMessage);
            } else {
                PrepareMessageBodyUsingRelatedBundle (relatedBundle);
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            if (downloader == MainMessageDownloader) {
                FinishPreparingMessage ();
            } else if (downloader == RelatedMessageDownloader) {
                PrepareMessageBodyUsingRelatedBundle (downloader.Bundle);
            } else {
                NcAssert.CaseError ();
            }
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: probably just continue with blank content?
            // Maybe notify Delegate so it can show an alert or something
            if (downloader == MainMessageDownloader) {
            } else if (downloader == RelatedMessageDownloader) {
            } else {
                NcAssert.CaseError ();
            }
        }

        void PrepareMessageBodyUsingRelatedBundle (NcEmailMessageBundle relatedBundle)
        {
            NcTask.Run (() => {
                var doc = new HtmlDocument ();
                doc.LoadHtml (relatedBundle.FullHtml);
                if (EmailHelper.IsReplyAction(Kind)){
                    ReplaceInlineImages (doc);
                }
                if (Kind == EmailHelper.Action.Forward || EmailHelper.IsReplyAction(Kind)){
                    QuoteHtml (doc, RelatedMessage);
                    InsertInitialHtml (doc);
                }
                Bundle.SetFullHtml (doc, relatedBundle);
                InvokeOnUIThread.Instance.Invoke (() => {
                    FinishPreparingMessage ();
                });
            }, "MessageComposer_SetFullHtml");
        }

        void InsertInitialHtml (HtmlDocument doc)
        {
            var body = doc.DocumentNode.Element ("html").Element ("body");
            var firstChildBeforeInserts = body.FirstChild;
            string messageText = "";
            if (!String.IsNullOrWhiteSpace (InitialText)){
                messageText += InitialText;
            }
            messageText += SignatureText ();
            if (!String.IsNullOrWhiteSpace (messageText)){
                using (var reader = new StringReader (messageText)){
                    var line = reader.ReadLine ();
                    while (line != null) {
                        var div = doc.CreateElement ("div");
                        if (String.IsNullOrWhiteSpace (line)){
                            div.AppendChild (doc.CreateElement ("br"));
                        }else{
                            div.AppendChild (doc.CreateTextNodeWithEscaping (line));
                        }
                        if (firstChildBeforeInserts != null) {
                            body.InsertBefore (div, firstChildBeforeInserts);
                        } else {
                            body.AppendChild (div);
                        }
                        line = reader.ReadLine ();
                    }
                }
            }
        }

        void QuoteHtml (HtmlDocument doc, McEmailMessage sourceMessage)
        {
            var body = doc.DocumentNode.Element ("html").Element ("body");
            HtmlNode blockquote = doc.CreateElement ("blockquote");
            blockquote.SetAttributeValue ("type", "cite");
            var attribution = EmailHelper.AttributionLineForMessage (sourceMessage);
            var attributionLine = doc.CreateElement ("div");
            attributionLine.AppendChild (doc.CreateTextNodeWithEscaping (attribution));
            blockquote.AppendChild (attributionLine);
            blockquote.AppendChild (EmptyLine (doc));
            HtmlNode node;
            HtmlNode following = null;
            for (int i = body.ChildNodes.Count - 1; i >= 0; --i) {
                node = body.ChildNodes [i];
                node.Remove ();
                if (following != null) {
                    blockquote.InsertBefore (node, following);
                } else {
                    blockquote.AppendChild (node);
                }
                following = node;
            }
            body.AppendChild (EmptyLine (doc));
            body.AppendChild (blockquote);
            body.AppendChild (EmptyLine (doc));
        }

        HtmlNode EmptyLine (HtmlDocument doc)
        {
            var div = doc.CreateElement ("div");
            var br = doc.CreateElement ("br");
            div.AppendChild (br);
            return div;
        }

        void ReplaceInlineImages (HtmlDocument doc)
        {
            HtmlNode node;
            var stack = new List<HtmlNode> ();
            var body = doc.DocumentNode.Element ("html").Element ("body");
            stack.Add (body);
            while (stack.Count > 0) {
                node = stack [0];
                stack.RemoveAt (0);
                if (node.NodeType == HtmlNodeType.Element) {
                    if (node.Name.Equals ("img")) {
                        if (node.Attributes.Contains ("nacho-bundle-entry")) {
                            var replacement = doc.CreateTextNode ("[image]");
                            node.ParentNode.InsertBefore (replacement, node);
                            node.Remove ();
                        }
                    }
                }
                foreach (var child in node.ChildNodes) {
                    stack.Add (child);
                }
            }
        }

        void PrepareMessageBody ()
        {
            string messageText = "";
            if (!String.IsNullOrWhiteSpace(InitialText)) {
                messageText += InitialText;
            }
            messageText += SignatureText ();
            NcTask.Run (() => {
                Bundle.SetFullText (messageText);
                InvokeOnUIThread.Instance.Invoke(() => {
                    FinishPreparingMessage ();
                });
            }, "MessageComposer_SetFullText");
        }

        void FinishPreparingMessage ()
        {
            MessagePreparationState = MessagePreparationStatus.Done;
            if (Delegate != null) {
                Delegate.MessageComposerDidCompletePreparation (this);
            }
        }

        private string SignatureText ()
        {
            if (!String.IsNullOrEmpty (Account.Signature)) {
                return "\n\n" + Account.Signature;
            }
            return null;
        }

        #endregion

        #region Save Message 

        public MimeMessage Save (string html)
        {
            var mime = BuildMimeMessage (html);
            var text = mime.GetTextBody (MimeKit.Text.TextFormat.Text);
            var preview = text.Substring (0, Math.Min (text.Length, 256));
            McBody body;
            if (Message.BodyId != 0) {
                body = McBody.QueryById<McBody> (Message.BodyId);
                body.UpdateData ((FileStream stream) => {
                    mime.WriteTo (stream);
                });
            } else {
                body = McBody.InsertFile (Account.Id, McAbstrFileDesc.BodyTypeEnum.MIME_4, (FileStream stream) => {
                    mime.WriteTo (stream);
                });
            }
            if (Message.ReferencedEmailId != 0) {
                var relatedMessage = McEmailMessage.QueryById<McEmailMessage> (Message.ReferencedEmailId);
                // the referenced message may not exist anyore
                if (relatedMessage != null) {
                    EmailHelper.SetupReferences (ref mime, relatedMessage);
                }
            }
            Message = Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                var message = record as McEmailMessage;
                message.Subject = Message.Subject;
                message.To = Message.To;
                message.Cc = Message.Cc;
                message.Bcc = Message.Bcc;
                message.Intent = Message.Intent;
                message.IntentDate = Message.IntentDate;
                message.IntentDateType = Message.IntentDateType;
                message.BodyId = body.Id;
                message.BodyPreview = preview;
                message.DateReceived = mime.Date.DateTime;
//                message.QRType = Message.QRType;
                return true;
            });
            Bundle.Invalidate ();
            return mime;
        }

        public MimeMessage BuildMimeMessage (string html)
        {
            var toList = EmailHelper.AddressList (NcEmailAddress.Kind.To, null, Message.To);
            var ccList = EmailHelper.AddressList (NcEmailAddress.Kind.Cc, null, Message.Cc);
            var bccList = EmailHelper.AddressList (NcEmailAddress.Kind.Bcc, null, Message.Bcc);
            var mime = EmailHelper.CreateMessage (Account, toList, ccList, bccList);
            mime.Subject = Message.Subject ?? "";
            var doc = new HtmlDocument ();
            doc.LoadHtml (html);
            var serializer = new HtmlTextSerializer (doc);
            var alternative = new MultipartAlternative ();
            var plainPart = new TextPart ("plain");
            plainPart.Text = serializer.Serialize ();
            alternative.Add (plainPart);
            var htmlPart = HtmlPart (doc);
            alternative.Add (htmlPart);
            var attachments = McAttachment.QueryByItemId (Message);
            if (attachments.Count > 0) {
                var mixed = new Multipart ();
                mixed.Add (alternative);
                foreach (var attachment in attachments) {
                    var attachmentPart = new MimePart ();
                    // TODO: populate attachment part
                    mixed.Add (attachmentPart);
                }
                mime.Body = mixed;
            } else {
                mime.Body = alternative;
            }
            return mime;
        }

        MimeEntity HtmlPart (HtmlDocument doc)
        {
            bool hasRelatedParts = false;
            var related = new MultipartRelated ();
            var htmlPart = new TextPart ("html");
            related.Root = htmlPart;
            var stack = new List<HtmlNode> ();
            HtmlNode node;
            stack.Add (doc.DocumentNode);
            while (stack.Count > 0) {
                node = stack [0];
                stack.RemoveAt (0);
                if (node.NodeType == HtmlNodeType.Element) {
                    if (node.Attributes.Contains ("nacho-tag")) {
                        node.Remove ();
                    }
                    if (node.Attributes.Contains ("contenteditable")){
                        node.Attributes.Remove ("contenteditable");
                    }
                    if (node.Name.Equals ("img")) {
                        if (node.Attributes.Contains ("nacho-bundle-entry")) {
                            var entryName = node.Attributes ["nacho-bundle-entry"].Value;
                            hasRelatedParts = true;
                            var attachment = new MimePart();
                            // TODO: populate attachment part
                            attachment.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId ();
                            related.Add (attachment);
                            node.SetAttributeValue ("src", "cid:" + attachment.ContentId);
                            node.Attributes.Remove ("nacho-bundle-entry");
                        }
                    }
                }
                foreach (var child in node.ChildNodes) {
                    stack.Add (child);
                }
            }
            using (var writer = new StringWriter ()) {
                doc.Save (writer);
                htmlPart.Text = writer.ToString ();
            }
            if (hasRelatedParts) {
                return related;
            }
            return htmlPart;
        }

        #endregion

        #region Send Message

        public void Send (string html)
        {
            var mime = Save (html);
            Message = Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                var message = record as McEmailMessage;
                message.Subject = EmailHelper.CreateSubjectWithIntent (mime.Subject, Message.Intent, Message.IntentDateType, Message.IntentDate);
                return true;
            });
            EmailHelper.SendTheMessage (Message, RelatedCalendarItem);
        }

        #endregion

    }

}

