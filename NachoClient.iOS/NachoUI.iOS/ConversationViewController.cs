//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using System.Linq;

namespace NachoClient.iOS
{
    public class ConversationViewController : NcUIViewControllerNoLeaks, ChatMessageComposeDelegate, ChatViewDelegate, ChatViewDataSource, MessageDownloadDelegate, ThemeAdopter
    {
        
        public NachoEmailMessages Messages;
        McAccount Account;
        ChatView ChatView;
        ChatMessageComposeView ComposeView;
        Dictionary<int, List<McAttachment>> AttachmentsByMessageId;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;
        Dictionary<int, MessageDownloader> Downloaders;

        public ConversationViewController ()
        {
            HidesBottomBarWhenPushed = true;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            AttachmentsByMessageId = new Dictionary<int, List<McAttachment>> ();
            ParticipantsByEmailId = new Dictionary<int, McChatParticipant> ();
            Downloaders = new Dictionary<int, MessageDownloader> ();
        }

        #region Theme

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (adoptedTheme != theme) {
                adoptedTheme = theme;
                ChatView.AdoptTheme (theme);
            }
        }

        #endregion

        #region View Lifecycle

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
            SubviewChangedHeight ();
            if (IsMovingToParentViewController) {
                ChatView.ReloadData ();
                ChatView.ScrollToBottom ();
            }
        }

        protected override void CreateViewHierarchy ()
        {
            CGRect headerFrame = new CGRect (0.0f, 0.0f, View.Bounds.Width, 0.0f);
            CGRect composeFrame = new CGRect (0.0f, View.Bounds.Height - ChatMessageComposeView.STANDARD_HEIGHT, View.Bounds.Width, ChatMessageComposeView.STANDARD_HEIGHT);
            CGRect tableFrame = new CGRect (0.0f, headerFrame.Height, View.Bounds.Width, View.Bounds.Height - headerFrame.Height - composeFrame.Height);
            //HeaderView = new ChatMessagesHeaderView (headerFrame);
            //HeaderView.ChatViewController = this;
            //HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            ChatView = new ChatView (tableFrame);
            ChatView.DataSource = this;
            ChatView.Delegate = this;
            ChatView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            ChatView.ShowPortraits = true;
            ChatView.ShowNameLabels = true;
            ComposeView = new ChatMessageComposeView (composeFrame);
            ComposeView.ComposeDelegate = this;
            ComposeView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            //View.AddSubview (HeaderView);
            View.AddSubview (ChatView);
            View.AddSubview (ComposeView);
            //if (Chat == null) {
            //    HeaderView.EditingEnabled = true;
            //} else {
            //    UpdateForChat ();
            //    ComposeView.SetMessage (Chat.DraftMessage);
            //    var attachments = McAttachment.QueryByItemId (Chat.AccountId, Chat.Id, McAbstrFolderEntry.ClassCodeEnum.Chat);
            //    foreach (var attachment in attachments) {
            //        ComposeView.AddAttachment (attachment);
            //    }
            //}
            //UpdateFromField ();
        }

        #endregion

        #region Layout

        protected override void OnKeyboardChanged ()
        {
            base.OnKeyboardChanged ();
            SubviewChangedHeight ();
            ChatView.LayoutIfNeeded ();
        }

        public void SubviewChangedHeight ()
        {
            var y = 0;
            ComposeView.Frame = new CGRect (
                ComposeView.Frame.X,
                View.Bounds.Height - keyboardHeight - ComposeView.Frame.Height,
                ComposeView.Frame.Width,
                ComposeView.Frame.Height
            );
            ChatView.Frame = new CGRect (
                ChatView.Frame.X,
                y,
                ChatView.Frame.Width,
                ComposeView.Frame.Top - y
            );
        }

        #endregion

        public void ShowAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            }
        }

        #region Compose Delegate

        public void ChatComposeDidSend (ChatMessageComposeView composeView)
        {
            //Send ();
        }

        public void ChatComposeWantsAttachment (ChatMessageComposeView composeView)
        {
            //Attach ();
        }

        public void ChatComposeShowAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
            ShowAttachment (attachment);
        }

        public void ChatComposeDidRemoveAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
        }

        public bool ChatComposeCanSend (ChatMessageComposeView composeView)
        {
            return true;
        }

        public void ChatComposeChangedHeight (ChatMessageComposeView composeView)
        {
            SubviewChangedHeight ();
        }

        #endregion

        #region Chat View Delegate & Data Source

        public int NumberOfMessagesInChatView (ChatView chatView)
        {
            return Messages.Count ();
        }

        public ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index)
        {
            //var indexInArray = index - MessageCount + Messages.Count;
            //if (indexInArray < 0) {
            //    var messages = Chat.GetMessages (Messages.Count, Math.Max (MessagesPerQuery, -indexInArray));
            //    messages.Reverse ();
            //    Messages.InsertRange (0, messages);
            //}
            //indexInArray = index - MessageCount + Messages.Count;
            //if (indexInArray < 0) {
            //    // Hmmm...a message must have been deleted and messed up our count
            //    // Should rarely happen, and only if you scroll all the way back to the top
            //    // while a message gets deleted.
            //} else {
            //    message = Messages [indexInArray];
            //    ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant);
            //}
            McEmailMessage message = Messages.GetCachedMessage (Messages.Count () - 1 - index);
            McChatParticipant particpant = GetParticipant (message);
            List<McAttachment> attachments = GetAttachments(message);
            var messageView = chatView.DequeueReusableChatMessageView ();
            if (!message.IsRead) {
                EmailHelper.MarkAsRead (message, true);
            }
            messageView.OnAttachmentSelected = ShowAttachment;
            messageView.SetMessage (message, particpant, attachments, forceIsLoading: Downloaders.ContainsKey (message.Id));
            UpdateMessageViewBlockProperties (chatView, index, messageView);
            return messageView;
        }

        public void UpdateMessageViewBlockProperties (ChatView chatView, int index, ChatMessageView messageView)
        {
        }

        public void ChatMessageViewDidSelectError (ChatView chatView, int index)
        { 
        }

        public void ChatMessageViewNeedsLoad (ChatView chatView, McEmailMessage message)
        {
            if (!Downloaders.ContainsKey (message.Id)) {
                var downloader = new MessageDownloader ();
                downloader.Delegate = this;
                Downloaders.Add (message.Id, downloader);
                downloader.Download (message);
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            Downloaders.Remove (downloader.Message.Id);
            ChatView.ReloadData ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            Downloaders.Remove (downloader.Message.Id);
        }

        public void ChatViewDidSelectMessage (ChatView chatView, int index)
        {
            var message = Messages.GetCachedMessage (Messages.Count () - 1 - index);
            var vc = new MessageViewController ();
            vc.Message = message;
            NavigationController.PushViewController (vc, true);
        }

        #endregion

        McChatParticipant GetParticipant (McEmailMessage message)
        {
            if (Account == null) {
                Account = McAccount.QueryById<McAccount> (message.AccountId);
            }
            McChatParticipant particpant = null;
            if (!ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant)) {
                var address = McEmailAddress.QueryById<McEmailAddress> (message.FromEmailAddressId);
                if (String.Equals (address.CanonicalEmailAddress, Account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                    particpant = null;
                } else {
                    particpant = new McChatParticipant ();
                    particpant.AccountId = Account.Id;
                    particpant.EmailAddrId = address.Id;
                    particpant.EmailAddress = address.CanonicalEmailAddress;
                    particpant.UpdateCachedProperties ();
                }
                ParticipantsByEmailId [message.FromEmailAddressId] = particpant;
            }
            return particpant;
        }

        List<McAttachment> GetAttachments (McEmailMessage message)
        {
            List<McAttachment> attachments = null;
            if (message.cachedHasAttachments) {
                if (!AttachmentsByMessageId.ContainsKey (message.Id)) {
                    AttachmentsByMessageId.Add (message.Id, McAttachment.QueryByItemId (message.AccountId, message.Id, McAbstrFolderEntry.ClassCodeEnum.Email));
                }
                AttachmentsByMessageId.TryGetValue (message.Id, out attachments);
            }
            return attachments;
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
            foreach (var pair in Downloaders) {
                pair.Value.Delegate = null;
            }
            Downloaders.Clear ();
        }
    }
}
