//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoPlatform;
using HtmlAgilityPack;

namespace NachoCore.Utils
{

    public interface MessageComposerDelegate
    {
        void MessageComposerDidCompletePreparation (MessageComposer composer);
    }

    public class MessageComposer : MessageDownloadDelegate
    {

        public MessageComposerDelegate Delegate;
        public readonly McAccount Account;
        public McEmailMessage Message;
        public McEmailMessageThread RelatedThread;
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

        public MessageComposer (McAccount account)
        {
            Account = account;
        }

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
            Message.Insert ();
            EmailHelper.SaveEmailMessageInDrafts (Message);

            // Now we need to start building the initial message.  It could be blank, it
            // could inlude a quick reply, or it could be quoting another message.
            if (RelatedThread != null) {
                RelatedMessage = RelatedThread.FirstMessageSpecialCase ();
                Message.Subject = EmailHelper.CreateInitialSubjectLine (Kind, RelatedMessage.Subject);
                if (Kind == EmailHelper.Action.Forward) {
                    // FIXME: we may want to skip inline attachments here...gotta put everything together and test
                    // to see what works best
                    CopyAttachments (McAttachment.QueryByItemId (RelatedMessage));
                } else if (EmailHelper.IsReplyAction(Kind)) {
                    EmailHelper.PopulateMessageRecipients (Account, Message, Kind, RelatedMessage);
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
                    QuoteHtml (doc);
                    // TODO: insert signature & initial text, if any
                }
                Bundle.SetFullHtml (doc, relatedBundle);
                InvokeOnUIThread.Instance.Invoke (() => {
                    FinishPreparingMessage ();
                });
            }, "MessageComposer_SetFullHtml");
        }

        void QuoteHtml (HtmlDocument doc)
        {
            var body = doc.DocumentNode.Element ("html").Element ("body");
            HtmlNode blockquote = doc.CreateElement ("blockquote");
            blockquote.SetAttributeValue ("type", "cite");
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
            // TODO: add attribution line ("On XYZ ABC wrote:")
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

    }
}

