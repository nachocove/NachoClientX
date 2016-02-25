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
        Action<McEmailMessage> MessageReady;
        
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
            if (previousMessages.Count > 0) {
                Message.ReferencedEmailId = previousMessages [0].Id;
            }
        }

        public static void SendChatMessage (McChat chat, string text, List<McEmailMessage> previousMessages, Action<McEmailMessage> callback)
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
            var doc = new HtmlDocument ();
            var html = doc.CreateElement ("html");
            var head = doc.CreateElement ("head");
            var meta = doc.CreateElement ("meta");
            meta.SetAttributeValue ("charset", "utf-8");
            head.AppendChild (meta);
            var body = doc.CreateElement ("body");
            var chat = doc.CreateElement ("div");
            chat.SetAttributeValue ("id", "nacho-chat");
            body.AppendChild (chat);
            html.AppendChild (head);
            html.AppendChild (body);
            doc.DocumentNode.AppendChild (html);
            var serializer = new HtmlTextDeserializer ();
            serializer.DeserializeInto (Text, chat);
            serializer.DeserializeInto (SignatureText (), body);
            var parent = body;
            foreach (var previousMessage in PreviousMessages) {
                // It's safe to assume that any PreviousMessage we were given was already downloaded & displayed
                if (previousMessage.BodyId != 0) {
                    serializer.DeserializeInto ("\n", parent);
                    var blockquote = doc.CreateElement ("blockquote");
                    blockquote.SetAttributeValue ("type", "cite");
                    parent.AppendChild (blockquote);
                    serializer.DeserializeInto (EmailHelper.AttributionLineForMessage (previousMessage) + "\n\n", blockquote);
                    var previousBundle = new NcEmailMessageBundle (previousMessage);
                    if (previousBundle.NeedsUpdate) {
                        previousBundle.Update ();
                    }
                    serializer.DeserializeInto (previousBundle.TopText, blockquote);
                    parent = blockquote;
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
            Send ();
            if (MessageReady != null) {
                MessageReady (Message);
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

