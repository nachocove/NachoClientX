//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Collections.Generic;
using NachoCore.Model;
using MimeKit;
using HtmlAgilityPack;
using NachoPlatform;

namespace NachoCore.Utils
{
    public class ChatMessageComposer : MessageComposer
    {
        public McChat Chat { get; set; }
        string Text;
        List<McEmailMessage> PreviousMessages;
        Action<McEmailMessage, NcResult> MessageReady;
        
        private ChatMessageComposer (McChat chat, string text, List<McEmailMessage> previousMessages) : base(chat.AccountId)
        {
            Chat = chat;
            Text = text;
            PreviousMessages = previousMessages == null ? new List<McEmailMessage>() : previousMessages;
            Kind = PreviousMessages.Count > 0 ? EmailHelper.Action.ReplyAll : EmailHelper.Action.Send;
            var name = Pretty.UserNameForAccount (Account);
            var mailbox = new MailboxAddress (name, Account.EmailAddr);
            var subject = String.Format ("{0}Chat with {1} [Nacho Chat]", EmailHelper.IsReplyAction(Kind) ? "RE: " : "", !String.IsNullOrEmpty (mailbox.Name) ? mailbox.Name : mailbox.Address);
            Message = McEmailMessage.MessageWithSubject(Account, subject);
            Message.To = EmailHelper.AddressStringFromList (ChatToList ());
            Message.IsChat = true;
            Message.DateReceived = DateTime.Now;
            Message.IsRead = true;
            if (previousMessages.Count > 0) {
                Message.ReferencedEmailId = previousMessages [0].Id;
            }
            InitialAttachments = McAttachment.QueryByItemId (Chat.AccountId, Chat.Id, McAbstrFolderEntry.ClassCodeEnum.Chat);
            foreach (var attachment in InitialAttachments) {
                attachment.Unlink (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
            }
            Message.cachedHasAttachments = InitialAttachments.Count > 0;
        }

        public static void SendChatMessage (McChat chat, string text, List<McEmailMessage> previousMessages, Action<McEmailMessage, NcResult> callback)
        {
            var composer = new ChatMessageComposer (chat, text, previousMessages);
            composer.MessageReady = callback;
            composer.PrepareAndSendMessage ();
        }

        protected void PrepareAndSendMessage ()
        {
            StartPreparingMessage ();
            // While this appears to only kick off the message prep,
            // we also override FinishPreparingMessage to save & send
        }

        protected override void BuildMimeMessage (string html)
        {
            base.BuildMimeMessage (html);
            Mime.MessageId = "NachoChat." + Mime.MessageId;
        }

        protected override void PrepareMessageBody ()
        {
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            var htmlPath = Path.Combine (documentsPath, "chat-email.html");
            var doc = new HtmlDocument ();
            using (var stream = new FileStream (htmlPath, FileMode.Open, FileAccess.Read)) {
                doc.Load (stream);
            }

            var deserializer = new HtmlTextDeserializer ();
            var stack = new List<HtmlNode> ();
            stack.Add (doc.DocumentNode.Element("html"));

            while (stack.Count > 0) {
                var node = stack [0];
                stack.RemoveAt (0);
                if (node.NodeType == HtmlNodeType.Element) {
                    var templateVar = node.GetAttributeValue ("nacho-template", "");
                    if (templateVar != "") {
                        node.RemoveAllChildren ();
                    }
                    if (templateVar.Equals ("from-email")) {
                        deserializer.DeserializeInto (Account.EmailAddr, node, inline: true);
                    }else if (templateVar.Equals ("from-initials")) {
                        deserializer.DeserializeInto (EmailHelper.Initials(Account.EmailAddr), node, inline: true);
                    }else if (templateVar.Equals ("message")) {
                        deserializer.DeserializeInto (Text, node);
                    }
                    if (node.ChildNodes.Count > 0) {
                        stack.InsertRange (0, node.ChildNodes);
                    }
                }
            }

            NcTask.Run (() => {
                Bundle.SetParsed(fullHtml: doc, topText: Text);
                InvokeOnUIThread.Instance.Invoke (() => {
                    FinishPreparingMessage ();
                });
            }, "MessageComposer_SetFullText");
        }

        protected override void FinishPreparingMessage ()
        {
            base.FinishPreparingMessage ();
            Save (Bundle.FullHtml, invalidateBundle: false);
            var result = Send ();
            if (MessageReady != null) {
                MessageReady (Message, result);
            }
        }

        List<NcEmailAddress> ChatToList ()
        {
            var participants = McChatParticipant.GetChatParticipants (Chat.Id);
            var to = new List<NcEmailAddress> (participants.Count);
            foreach (var participant in participants) {
                var email = McEmailAddress.QueryById<McEmailAddress> (participant.EmailAddrId);
                if (email != null) {
                    to.Add (new NcEmailAddress (NcEmailAddress.Kind.To, email.CanonicalEmailAddress));
                }
            }
            return to;
        }

        public override string SignatureText ()
        {
            if (!String.IsNullOrEmpty (Account.Signature)) {
                return "\n" + Account.Signature;
            }
            return null;
        }
    }
}

