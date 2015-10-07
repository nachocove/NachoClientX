﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoPlatform;
using HtmlAgilityPack;
using MimeKit;

namespace NachoCore.Utils
{

    public interface MessageComposerDelegate
    {
        void MessageComposerDidCompletePreparation (MessageComposer composer);
        void MessageComposerDidFailToLoadMessage (MessageComposer composer);
        PlatformImage ImageForMessageComposerAttachment (MessageComposer composer, Stream stream);
    }

    public class MessageComposer : MessageDownloadDelegate
    {
        
        protected static readonly long DEFAULT_MAX_SIZE = 2000000;

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

        public bool HasRecipient {
            get {
                if (Message == null) {
                    return false;
                }
                return !String.IsNullOrWhiteSpace (Message.To) || !String.IsNullOrWhiteSpace (Message.Cc) || !String.IsNullOrWhiteSpace (Message.Bcc);
            }
        }

        public long MaxSize = DEFAULT_MAX_SIZE;
        public Tuple <float, float> ImageLengths = null;

        private McEmailMessage RelatedMessage;
        McBody Body;

        public Tuple<float, float> SmallImageLengths = new Tuple<float, float> (240, 320);
        public Tuple<float, float> MediumImageLengths = new Tuple<float, float> (480, 640);
        public Tuple<float, float> LargeImageLengths = new Tuple<float, float> (960, 1280);

        long _EstimatedSmallSizeDelta = 0;
        long _EstimatedMediumSizeDelta = 0;
        long _EstimatedLargeSizeDelta = 0;

        public long EstimatedSmallSize {
            get {
                return MessageSize - _EstimatedSmallSizeDelta;
            }
        }

        public long EstimatedMediumSize {
            get {
                return MessageSize - _EstimatedMediumSizeDelta;
            }
        }

        public long EstimatedLargeSize {
            get {
                return MessageSize - _EstimatedLargeSizeDelta;
            }
        }

        public bool CanResize {
            get {
                return _EstimatedLargeSizeDelta > 0 || _EstimatedMediumSizeDelta > 0 || _EstimatedSmallSizeDelta > 0;
            }
        }

        public long MessageSize {
            get {
                if (Body != null) {
                    return Body.FileSize;
                }
                return 0;
            }
        }

        public bool IsOversize {
            get {
                return MessageSize > MaxSize;
            }
        }

        enum MessagePreparationStatus {
            NotStarted,
            Preparing,
            Done
        };

        NcEmailMessageBundle _Bundle;
        MessagePreparationStatus MessagePreparationState = MessagePreparationStatus.NotStarted;
        MessageDownloader MainMessageDownloader;
        MessageDownloader RelatedMessageDownloader;
        List<BinaryReader> OpenReaders;
        MimeMessage Mime;
        Dictionary <string, McAttachment> AttachmentsBySrc;
        Dictionary <int, bool> AttachmentsInHtml;

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
                var now = DateTime.UtcNow;
                NcBrain.MessageReplyStatusUpdated (RelatedMessage, now, 0.1);
            }
            var mailbox = new MailboxAddress (Pretty.UserNameForAccount (Account), Account.EmailAddr);
            Message.From = mailbox.ToString ();
            Message.Insert ();
            EmailHelper.SaveEmailMessageInDrafts (Message);

            // Now we need to start building the initial message.  It could be blank, it
            // could inlude a quick reply, or it could be quoting another message.
            if (RelatedThread != null) {
                if (Kind == EmailHelper.Action.Forward) {
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
                IsInline = attachment.IsInline
            };
            copy.Insert ();
            // TODO: It would be nice to do any heavy work like large file copying in a background task.
            // I'm not familiar enough with the details to set that up quite yet.
            copy.SetDisplayName (attachment.DisplayName);
            copy.Update ();
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                copy.UpdateFileCopy (attachment.GetFilePath ());
            } else {
                // Download attachment?
            }
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
            if (downloader == MainMessageDownloader) {
                Delegate.MessageComposerDidFailToLoadMessage (this);
            } else if (downloader == RelatedMessageDownloader) {
                FinishPreparingMessage ();
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
                } else {
                    StripAttachedImagesInlinedByBundle (doc);
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
            blockquote.SetAttributeValue ("id", "quoted-original");
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
        void StripAttachedImagesInlinedByBundle (HtmlDocument doc)
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
                        if (node.Attributes.Contains ("nacho-image-attachment")) {
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

        public string SignatureText ()
        {
            if (!String.IsNullOrEmpty (Account.Signature)) {
                return "\n\n" + Account.Signature;
            }
            return null;
        }

        #endregion

        #region Save Message 

        public void Save (string html)
        {
            if (Bundle.NeedsUpdate) {
                // If we resized images, it could be the second time through this save.
                // The first time through would have invalidated the bundle, and we'll need
                // to update it the second time through so it can create inline images properly
                Bundle.Update ();
            }
            OpenReaders = new List<BinaryReader> ();
            BuildMimeMessage (html);
            WriteBody ();
            ClearReaders ();
            var text = Mime.GetTextBody (MimeKit.Text.TextFormat.Text);
            var preview = text.Substring (0, Math.Min (text.Length, 256));
            Message = Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                var message = record as McEmailMessage;
                message.Subject = Message.Subject;
                message.To = Message.To;
                message.Cc = Message.Cc;
                message.Bcc = Message.Bcc;
                message.Intent = Message.Intent;
                message.IntentDate = Message.IntentDate;
                message.IntentDateType = Message.IntentDateType;
                message.BodyId = Body.Id;
                message.BodyPreview = preview;
                message.DateReceived = Mime.Date.DateTime;
                return true;
            });
            Bundle.Invalidate ();
        }

        void WriteBody ()
        {
            if (Message.BodyId != 0) {
                Body = McBody.QueryById<McBody> (Message.BodyId);
                Body.UpdateData ((FileStream stream) => {
                    Mime.WriteTo (stream);
                });
                Body.UpdateSaveFinish ();
            } else {
                Body = McBody.InsertFile (Account.Id, McAbstrFileDesc.BodyTypeEnum.MIME_4, (FileStream stream) => {
                    Mime.WriteTo (stream);
                });
            }
        }

        void BuildMimeMessage (string html)
        {
            _EstimatedLargeSizeDelta = 0;
            _EstimatedMediumSizeDelta = 0;
            _EstimatedSmallSizeDelta = 0;
            AttachmentsBySrc = new Dictionary<string, McAttachment> ();
            AttachmentsInHtml = new Dictionary<int, bool> ();
            var toList = EmailHelper.AddressList (NcEmailAddress.Kind.To, null, Message.To);
            var ccList = EmailHelper.AddressList (NcEmailAddress.Kind.Cc, null, Message.Cc);
            var bccList = EmailHelper.AddressList (NcEmailAddress.Kind.Bcc, null, Message.Bcc);
            var attachments = McAttachment.QueryByItemId (Message);
            foreach (var attachment in attachments) {
                if (!String.IsNullOrEmpty (attachment.ContentId)) {
                    AttachmentsBySrc ["cid:" + attachment.ContentId] = attachment;
                }
            }
            Mime = EmailHelper.CreateMessage (Account, toList, ccList, bccList);
            Mime.Subject = Message.Subject ?? "";
            var doc = new HtmlDocument ();
            doc.LoadHtml (html);
            var serializer = new HtmlTextSerializer (doc);
            var alternative = new MultipartAlternative ();
            var plainPart = new TextPart ("plain");
            plainPart.Text = serializer.Serialize ();
            alternative.Add (plainPart);
            var htmlPart = HtmlPart (doc);
            alternative.Add (htmlPart);
            Multipart mixed = null;
            foreach (var attachment in attachments) {
                if (!AttachmentsInHtml.ContainsKey(attachment.Id)) {
                    if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                        if (mixed == null) {
                            mixed = new Multipart ();
                            mixed.Add (alternative);
                        }
                        var attachmentPart = new MimePart (attachment.ContentType ?? "application/octet-stream");
                        attachmentPart.FileName = attachment.DisplayName;
                        attachmentPart.IsAttachment = true;
                        attachmentPart.ContentTransferEncoding = ContentEncoding.Base64;
                        var reader = new BinaryReader (new FileStream (attachment.GetFilePath (), FileMode.Open));
                        OpenReaders.Add (reader);
                        attachmentPart.ContentObject = new ContentObject (reader.BaseStream);
                        mixed.Add (attachmentPart);
                        AdjustEstimatedSizesForAttachment (reader.BaseStream);
                    } else {
                        Log.Error (Log.LOG_EMAIL, "MessageComposer could not include attachment ID#{0} because its state is {1}", attachment.Id, attachment.FilePresence);
                    }
                }
            }
            if (mixed != null) {
                Mime.Body = mixed;
            } else {
                Mime.Body = alternative;
            }
            if (Message.ReferencedEmailId != 0) {
                var relatedMessage = McEmailMessage.QueryById<McEmailMessage> (Message.ReferencedEmailId);
                // the referenced message may not exist anyore
                if (relatedMessage != null) {
                    EmailHelper.SetupReferences (ref Mime, relatedMessage);
                }
            }
        }

        MimeEntity HtmlPart (HtmlDocument doc)
        {
            bool hasRelatedParts = false;
            var related = new MultipartRelated ();
            var htmlPart = new TextPart ("html");
            related.Root = htmlPart;
            var SrcsByEntryName = new Dictionary<string, string> ();
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
                        if (node.Attributes.Contains ("nacho-image-attachment")){
                            node.Attributes.Remove ("nacho-image-attachment");
                        }
                        if (node.Attributes.Contains ("nacho-bundle-entry")) {
                            var entryName = node.Attributes ["nacho-bundle-entry"].Value;
                            if (!SrcsByEntryName.ContainsKey (entryName)) {
                                McAttachment mcattachment = null;
                                if (node.Attributes.Contains ("nacho-original-src")) {
                                    var originalSrc = node.Attributes ["nacho-original-src"].Value;
                                    if (AttachmentsBySrc.ContainsKey (originalSrc)) {
                                        mcattachment = AttachmentsBySrc [originalSrc];
                                    }
                                    node.Attributes.Remove ("nacho-original-src");
                                }
                                var info = Bundle.MemberForEntryName (entryName);
                                OpenReaders.Add (info.Reader);
                                hasRelatedParts = true;
                                var attachment = new MimePart (info.ContentType ?? "image/jpeg");
                                attachment.FileName = info.Filename;
                                attachment.IsAttachment = true;
                                attachment.ContentTransferEncoding = ContentEncoding.Base64;
                                attachment.ContentObject = new ContentObject (info.Reader.BaseStream);
                                attachment.ContentId = MimeKit.Utils.MimeUtils.GenerateMessageId ();
                                related.Add (attachment);
                                var src = "cid:" + attachment.ContentId;
                                node.SetAttributeValue ("src", src);
                                AdjustEstimatedSizesForAttachment (info.Reader.BaseStream);
                                SrcsByEntryName [entryName] = src;
                                if (mcattachment != null) {
                                    mcattachment.ContentId = attachment.ContentId;
                                    mcattachment.Update ();
                                    AttachmentsInHtml [mcattachment.Id] = true;
                                }
                            } else {
                                node.SetAttributeValue ("src", SrcsByEntryName [entryName]);
                            }
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

        void ClearReaders ()
        {
            if (OpenReaders != null) {
                foreach (var reader in OpenReaders) {
                    reader.Close ();
                }
                OpenReaders = null;
            }
        }

        #endregion

        #region Image Resizing

        public void ResizeImages ()
        {
            long length;
            using (var stream = Message.ToMime (out length)) {
                Mime = MimeMessage.Load (stream);
            }
            var openStreams = new List<FileStream> ();
            foreach (var entity in Mime.BodyParts.Where(p => p.ContentType.Matches("image", "*"))) {
                var part = entity as MimePart;
                if (part != null) {
                    var tmpFilePath = Path.GetTempFileName ();
                    using (var stream = new FileStream (tmpFilePath, FileMode.Create)) {
                        part.ContentObject.DecodeTo (stream);
                    }
                    var tmpStream = new FileStream (tmpFilePath, FileMode.Open);
                    var image = Delegate.ImageForMessageComposerAttachment (this, tmpStream);
                    tmpStream.Dispose ();
                    if (image != null) {
                        tmpStream = new FileStream (tmpFilePath, FileMode.Create);
                        var jpg = image.ResizedData (ImageLengths.Item1, ImageLengths.Item2);
                        jpg.CopyTo (tmpStream);
                        tmpStream.Dispose ();
                        jpg.Dispose ();
                        tmpStream = new FileStream (tmpFilePath, FileMode.Open);
                        openStreams.Add (tmpStream);
                        part.ContentType.MediaSubtype = "jpeg";
                        part.ContentObject = new ContentObject (tmpStream);
                        image.Dispose ();
                    }
                }
            }
            WriteBody ();
            foreach (var stream in openStreams) {
                stream.Dispose ();
                File.Delete (stream.Name);
            }
        }

        void AdjustEstimatedSizesForAttachment (Stream stream)
        {
            stream.Seek (0, 0);
            var image = Delegate.ImageForMessageComposerAttachment (this, stream);
            if (image != null) {
                AdjustEstimatedSizesForImageWithProperties (stream.Length, image.Size);
                image.Dispose ();
            }
            stream.Seek (0, 0);
        }

        void AdjustEstimatedSizesForImageWithProperties (long fileSize, Tuple<float, float> imageSize)
        {
            long encodedByteSize = (long)((float)fileSize * 4.0 / 3.0);
                float smallRatio = RatioForSizedImage (imageSize, SmallImageLengths);
                float mediumRatio = RatioForSizedImage (imageSize, MediumImageLengths);
                float largeRatio = RatioForSizedImage (imageSize, LargeImageLengths);
                long estimatedSmallEncodedSize = (long)((float)encodedByteSize * smallRatio * smallRatio);
                long estimatedMediumEncodedSize = (long)((float)encodedByteSize * mediumRatio * mediumRatio);
                long estimatedLargeEncodedSize = (long)((float)encodedByteSize * largeRatio * largeRatio);
                _EstimatedSmallSizeDelta += encodedByteSize - estimatedSmallEncodedSize;
                _EstimatedMediumSizeDelta += encodedByteSize - estimatedMediumEncodedSize;
                _EstimatedLargeSizeDelta += encodedByteSize - estimatedLargeEncodedSize;
        }

        float RatioForSizedImage (Tuple<float, float> imageSize, Tuple<float, float> newSize)
        {
            float width = imageSize.Item1;
            float height = imageSize.Item2;
            float widthRatio = 1.0f;
            float heightRatio = 1.0f;
            if (width > newSize.Item1) {
                widthRatio = newSize.Item1 / width;
            }
            if (height > newSize.Item2) {
                heightRatio = newSize.Item2 / height;
            }
            return Math.Min (widthRatio, heightRatio);
        }

        #endregion

        #region Send Message

        public void Send ()
        {
            var subjectWithoutIntent = Message.Subject;
            Message = Message.UpdateWithOCApply<McEmailMessage> ((McAbstrObject record) => {
                var message = record as McEmailMessage;
                message.Subject = EmailHelper.CreateSubjectWithIntent (subjectWithoutIntent, Message.Intent, Message.IntentDateType, Message.IntentDate);
                return true;
            });
            if (ImageLengths != null) {
                ResizeImages ();
            }
            EmailHelper.SendTheMessage (Message, RelatedCalendarItem);
        }

        #endregion

    }

}

