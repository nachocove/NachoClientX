//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ChatMessagesViewController : NcUIViewControllerNoLeaks, INachoContactChooserDelegate, ChatViewDataSource, ChatViewDelegate
    {

        #region Properties

        public McAccount Account;
        public McChat Chat;
        List<McEmailMessage> Messages;
        ChatView ChatView;
        ChatMessagesHeaderView HeaderView;
        ChatMessageComposeView ComposeView;
        bool IsListeningForStatusChanges;
        UIStoryboard mainStorybaord;
        Dictionary<string, bool> LoadedMessageIDs;
        UIStoryboard MainStoryboard {
            get {
                if (mainStorybaord == null) {
                    mainStorybaord = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                }
                return mainStorybaord;
            }

        }

        #endregion

        #region Constructors

        public ChatMessagesViewController ()
        {
            HidesBottomBarWhenPushed = true;
            Messages = new List<McEmailMessage> ();
            LoadedMessageIDs = new Dictionary<string, bool> ();
        }

        #endregion

        #region View Lifecycle

        protected override void CreateViewHierarchy ()
        {
            CGRect headerFrame = new CGRect (0.0f, 0.0f, View.Bounds.Width, 44.0f);
            CGRect composeFrame = new CGRect (0.0f, View.Bounds.Height - ChatMessageComposeView.STANDARD_HEIGHT, View.Bounds.Width, ChatMessageComposeView.STANDARD_HEIGHT);
            CGRect tableFrame = new CGRect (0.0f, headerFrame.Height, View.Bounds.Width, View.Bounds.Height - headerFrame.Height - composeFrame.Height);
            HeaderView = new ChatMessagesHeaderView (headerFrame);
            HeaderView.ChatViewController = this;
            HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            ChatView = new ChatView (tableFrame);
            ChatView.DataSource = this;
            ChatView.Delegate = this;
            ChatView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            ComposeView = new ChatMessageComposeView (composeFrame);
            ComposeView.ChatViewController = this;
            ComposeView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            View.AddSubview (HeaderView);
            View.AddSubview (ChatView);
            View.AddSubview (ComposeView);
            if (Chat == null) {
                HeaderView.EditingEnabled = true;
            } else {
                HeaderView.ParticipantsLabel.Text = Chat.CachedParticipantsLabel;
                HeaderView.EditingEnabled = false;
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            StartListeningForStatusChanges ();
            if (Chat != null) {
                ReloadMessages ();
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            StopListeningForStatusChanges ();
            base.ViewDidDisappear (animated);
        }

        protected override void OnKeyboardChanged ()
        {
            var scrolledToBottom = ChatView.IsScrolledToBottom;
            base.OnKeyboardChanged ();
            SubviewChangedHeight ();
            ChatView.LayoutIfNeeded ();
        }

        public void SubviewChangedHeight ()
        {
            var y = HeaderView.Frame.Top + HeaderView.Frame.Height;
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

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }

        #endregion

        #region User Actions

        public void Send ()
        {
            if (Chat == null) {
                var addresses = HeaderView.ToView.AddressList;
                var emailAddresses = new List<McEmailAddress> (addresses.Count);
                McEmailAddress emailAddress;
                foreach (var address in addresses) {
                    McEmailAddress.Get (Account.Id, address.address, out emailAddress);
                    emailAddresses.Add (emailAddress);
                }
                Chat = McChat.ChatForAddresses (Account.Id, emailAddresses);
                ReloadMessages ();
                HeaderView.ParticipantsLabel.Text = Chat.CachedParticipantsLabel;
                HeaderView.EditingEnabled = false;
            }
            var text = ComposeView.GetMessage ();
            var previousMessages = new List<McEmailMessage> ();
            for (int i = Messages.Count - 1; i >= Messages.Count - 3 && i >= 0; --i){
                previousMessages.Add (Messages [i]);
            }
            ChatMessageComposer.SendChatMessage (Chat, text, previousMessages, (McEmailMessage message) => {
                // TODO: this message is in the outbox.  It will be deleted and replaced by a message from the sent folder
                Chat.AddMessage (message);
                ComposeView.Clear ();
                ChatView.ScrollToBottom ();
            });
        }

        #endregion

        #region System Events

        void StartListeningForStatusChanges ()
        {
            if (!IsListeningForStatusChanges) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                IsListeningForStatusChanges = true;
            }
        }

        void StopListeningForStatusChanges ()
        {
            if (IsListeningForStatusChanges) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                IsListeningForStatusChanges = false;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (Chat == null) {
                return;
            }
            var s = (StatusIndEventArgs)e;
            if (s.Account != null) {
                if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified || NcApplication.Instance.Account.Id == s.Account.Id) {
                    if (s.Status.SubKind == NcResult.SubKindEnum.Info_ChatMessageAdded) {
                        var chatId = Convert.ToInt32 (s.Tokens[0]);
                        var messageId = Convert.ToInt32 (s.Tokens[1]);
                        if (chatId == Chat.Id) {
                            var message = McEmailMessage.QueryById<McEmailMessage> (messageId);
                            if (message != null) {
                                if (LoadedMessageIDs.ContainsKey (message.MessageID)) {
                                    // Swap out the old message, which was possibly deleted, with the new one.
                                    for (int i = Messages.Count - 1; i >= 0; --i) {
                                        var existingMessage = Messages[i];
                                        if (existingMessage.MessageID == message.MessageID) {
                                            Messages [i] = message;
                                            break;
                                        }
                                    }
                                }else{
                                    Messages.Add (message);
                                    LoadedMessageIDs.Add (message.MessageID, true);
                                    ChatView.InsertMessageViewAtEnd ();
                                }
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Message Loading & Display

        void ReloadMessages ()
        {
            if (Chat != null) {
                LoadedMessageIDs.Clear ();
                Messages = Chat.GetMessages ();
                Messages.Reverse ();
                foreach (var message in Messages) {
                    LoadedMessageIDs.Add (message.MessageID, true);
                }
                ChatView.ReloadData ();
                ChatView.ScrollToBottom ();
            }
        }

        public int NumberOfMessagesInChatView (ChatView chatView)
        {
            return Messages.Count;
        }

        public ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index)
        {
            var message = Messages [index];
            if (!message.IsRead) {
                EmailHelper.MarkAsRead (message, true);
            }
            var messageView = chatView.DequeueReusableChatMessageView ();
            bool isFromMe = false;
            MimeKit.InternetAddress fromAddress;
            if (MimeKit.MailboxAddress.TryParse (message.From, out fromAddress)) {
                if (String.Equals ((fromAddress as MimeKit.MailboxAddress).Address, Account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                    isFromMe = true;
                }
            }
            messageView.SetMessage (message, isFromMe);
            return messageView;
        }

        #endregion

        #region Contact Choosing Ownership

        public void ShowContactChooser (NcEmailAddress address)
        {
            ContactChooserViewController chooserController = MainStoryboard.InstantiateViewController ("ContactChooserViewController") as ContactChooserViewController;
            chooserController.SetOwner (this, Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, chooserController);
        }

        public void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            HeaderView.ToView.Append (address);
        }

        public void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            // The contact chooser was pushed on the nav stack, rather than shown as a modal.
            // So we need to pop it from the stack
            vc.Cleanup ();
            NavigationController.PopViewController (true);
            HeaderView.ToView.BecomeFirstResponder ();
        }

        public void RemoveAddress (NcEmailAddress address)
        {
        }

        #endregion
    }

    public class ChatMessagesHeaderView : UIView, IUcAddressBlockDelegate
    {

        public readonly UcAddressBlock ToView;
        bool _EditingEnabled;
        public bool EditingEnabled {
            get {
                return _EditingEnabled;
            }
            set {
                _EditingEnabled = value;
                SetNeedsLayout ();
            }
        }

        public ChatMessagesViewController ChatViewController;
        UIView BottomBorderView;
        public readonly UILabel ParticipantsLabel;

        public ChatMessagesHeaderView (CGRect frame) : base (frame)
        {
            BackgroundColor = UIColor.White;
            BottomBorderView = new UIView (new CGRect (0.0f, Bounds.Height - 1.0f, Bounds.Size.Width, 1.0f));
            BottomBorderView.BackgroundColor = A.Color_NachoBorderGray;
            BottomBorderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
            ToView = new UcAddressBlock (this, "To:", null, Bounds.Width);
            ToView.SetCompact (false, -1);
            ToView.ConfigureView ();
            ToView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            ParticipantsLabel = new UILabel (Bounds);
            ParticipantsLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            ParticipantsLabel.Font = A.Font_AvenirNextDemiBold17;
            ParticipantsLabel.TextAlignment = UITextAlignment.Center;
            ParticipantsLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            ParticipantsLabel.TextColor = A.Color_NachoGreen;
            ParticipantsLabel.Lines = 1;
            AddSubview (ToView);
            AddSubview (ParticipantsLabel);
            AddSubview (BottomBorderView);
        }

        public void AddressBlockNeedsLayout (UcAddressBlock view)
        {
            view.SetNeedsLayout ();
            view.LayoutIfNeeded ();
            SetNeedsLayout ();
        }

        public void AddressBlockWillBecomeActive (UcAddressBlock view)
        {
        }

        public void AddressBlockWillBecomeInactive (UcAddressBlock view)
        {
        }

        public void AddressBlockAutoCompleteContactClicked(UcAddressBlock view, string prefix)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.Unknown, prefix);
            ChatViewController.ShowContactChooser (address);
        }

        public void AddressBlockSearchContactClicked(UcAddressBlock view, string prefix)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.Unknown, prefix);
            ChatViewController.ShowContactChooser (address);
        }

        public void AddressBlockRemovedAddress (UcAddressBlock view, NcEmailAddress address)
        {
            ChatViewController.RemoveAddress (address);
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            var previousPreferredHeight = Frame.Height;
            nfloat preferredHeight = 43.0f;
            if (EditingEnabled) {
                base.LayoutSubviews ();
                preferredHeight = (nfloat)Math.Max (43.0f, ToView.Frame.Size.Height + 1.0f);
                ToView.Hidden = false;
                ParticipantsLabel.Hidden = true;
            } else {
                ToView.Hidden = true;
                ParticipantsLabel.Hidden = false;
            }
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            if ((Math.Abs (preferredHeight - previousPreferredHeight) > 0.5) && ChatViewController != null) {
                ChatViewController.SubviewChangedHeight ();
            }
        }
    }

    public class ChatMessageComposeView : UIView
    {

        public ChatMessagesViewController ChatViewController;
        UITextView MessageField;
        UIButton SendButton;
        UIView TopBorderView;
        UILabel MessagePlaceholderLabel;
        public static readonly nfloat STANDARD_HEIGHT = 41.0f;

        public ChatMessageComposeView (CGRect frame) : base (frame)
        {
            TopBorderView = new UIView (new CGRect (0.0f, 0.0f, Bounds.Size.Width, 1.0f));
            TopBorderView.BackgroundColor = A.Color_NachoBorderGray;
            TopBorderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            BackgroundColor = UIColor.White;
            MessageField = new UITextView (Bounds);
            MessageField.Changed += MessageChanged;
            MessageField.Font = A.Font_AvenirNextRegular17;
            MessagePlaceholderLabel = new UILabel (MessageField.Bounds);
            MessagePlaceholderLabel.UserInteractionEnabled = false;
            MessagePlaceholderLabel.Font = MessageField.Font;
            MessagePlaceholderLabel.TextColor = A.Color_NachoTextGray;
            MessagePlaceholderLabel.Text = "Type a message...";
            SendButton = new UIButton (UIButtonType.Custom);
            SendButton.SetTitle ("Send", UIControlState.Normal);
            SendButton.TouchUpInside += Send;
            SendButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            SendButton.SetTitleColor (A.Color_NachoTextGray, UIControlState.Disabled);
            SendButton.TitleEdgeInsets = new UIEdgeInsets (0.0f, 7.0f, 0.5f, 7.0f);
            SendButton.Font = A.Font_AvenirNextMedium17;
            AddSubview (TopBorderView);
            AddSubview (MessageField);
            AddSubview (MessagePlaceholderLabel);
            AddSubview (SendButton);
            UpdateSendEnabled ();
        }

        void MessageChanged (object sender, EventArgs e)
        {
            UpdateSendEnabled ();
            var frame = MessageField.Frame;
            MessageField.Frame = new CGRect (frame.X, frame.Y, frame.Width, 1.0f);
            if (MessageField.ContentSize.Height != frame.Height) {
                SetNeedsLayout ();
                LayoutIfNeeded ();
            } else {
                MessageField.Frame = frame;
            }
        }

        void Send (object sender, EventArgs e)
        {
            ChatViewController.Send ();
        }

        public string GetMessage ()
        {
            return MessageField.Text;
        }

        public void Clear ()
        {
            MessageField.Text = "";
            UpdateSendEnabled ();
            SetNeedsLayout ();
        }

        void UpdateSendEnabled ()
        {
            SendButton.Enabled = !String.IsNullOrWhiteSpace (MessageField.Text);
            MessagePlaceholderLabel.Hidden = SendButton.Enabled;
        }

        public override void LayoutSubviews ()
        {
            var preferredHeight = (nfloat)Math.Min (MessageField.ContentSize.Height + 1.0f, 4.0f * STANDARD_HEIGHT);
            var previousHeight = Frame.Height;
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            bool changedHeight = (Math.Abs (preferredHeight - previousHeight) > 0.5) && ChatViewController != null;
            SendButton.SizeToFit ();
            var sendButtonHeight = STANDARD_HEIGHT - 1.0f;
            SendButton.Frame = new CGRect (
                Bounds.Width - SendButton.Frame.Size.Width - SendButton.TitleEdgeInsets.Left - SendButton.TitleEdgeInsets.Right,
                Bounds.Height - sendButtonHeight,
                SendButton.Frame.Size.Width + SendButton.TitleEdgeInsets.Left + SendButton.TitleEdgeInsets.Right,
                sendButtonHeight
            );
            MessageField.Frame = new CGRect (
                0.0f,
                1.0f,
                SendButton.Frame.X,
                Bounds.Height - 1.0f
            );
            MessagePlaceholderLabel.SizeToFit ();
            MessagePlaceholderLabel.Frame = new CGRect (MessageField.Frame.X + 5.0f, MessageField.Frame.Y + 8.0f, MessagePlaceholderLabel.Frame.Width, MessagePlaceholderLabel.Frame.Height);
            if (changedHeight) {
                ChatViewController.SubviewChangedHeight ();
            }
        }

    }


    public interface ChatViewDataSource {
        int NumberOfMessagesInChatView (ChatView chatView);
        ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index);
    }

    public interface ChatViewDelegate {
    }

    public class ChatView : UIView, IUIScrollViewDelegate
    {
        public readonly UIScrollView ScrollView;
        public ChatViewDataSource DataSource;
        public ChatViewDelegate Delegate;
        Queue<ChatMessageView> ReusableMessageViews;
        List<ChatMessageView> VisibleMessageViews;
        int MessageCount;
        int TopCalculatedIndex;
        nfloat MessageSpacing = 4.0f;

        public bool IsScrolledToBottom {
            get {
                return ScrollView.ContentOffset.Y >= Math.Max (0, ScrollView.ContentSize.Height - ScrollView.Bounds.Height - MessageSpacing);
            }
        }

        public ChatView (CGRect frame) : base (frame)
        {
            BackgroundColor = A.Color_NachoBackgroundGray;
            ScrollView = new UIScrollView (Bounds);
            ScrollView.Delegate = this;
            AddSubview (ScrollView);
            ReusableMessageViews = new Queue<ChatMessageView> ();
            VisibleMessageViews = new List<ChatMessageView> ();
        }

        public ChatMessageView DequeueReusableChatMessageView ()
        {
            ChatMessageView messageView;
            if (ReusableMessageViews.Count > 0) {
                messageView = ReusableMessageViews.Dequeue ();
            }
            messageView = new ChatMessageView (new CGRect (0.0f, 0.0f, Bounds.Width, 100.0f));
            messageView.ChatView = this;
            return messageView;
        }

        void EnqueueReusableChatMessageView (ChatMessageView messageView)
        {
            messageView.ChatView = null;
            ReusableMessageViews.Enqueue (messageView);
        }

        public void ReloadData ()
        {
            MessageCount = DataSource.NumberOfMessagesInChatView (this);
            var index = MessageCount - 1;
            nfloat height = MessageSpacing;
            ChatMessageView messageView;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i) {
                messageView = VisibleMessageViews [i];
                messageView.RemoveFromSuperview ();
                EnqueueReusableChatMessageView (messageView);
            }
            VisibleMessageViews.Clear ();
            TopCalculatedIndex = 0;
            while (index >= 0 && height < Bounds.Height * 3.0f) {
                messageView = DataSource.ChatMessageViewAtIndex (this, index);
                messageView.Index = index;
                messageView.SizeToFit ();
                if (height < Bounds.Height) {
                    VisibleMessageViews.Add (messageView);
                } else {
                    EnqueueReusableChatMessageView (messageView);
                }
                height += messageView.Frame.Height + MessageSpacing;
                TopCalculatedIndex = index;
                --index;
            }
            VisibleMessageViews.Sort (CompareMessageViews);
            var y = height - MessageSpacing;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i) {
                messageView = VisibleMessageViews [i];
                messageView.Frame = new CGRect (messageView.Frame.X, y - messageView.Frame.Height, messageView.Frame.Width, messageView.Frame.Height);
                ScrollView.AddSubview (messageView);
                y -= messageView.Frame.Height + MessageSpacing;
            }
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width, height);
        }

        int CompareMessageViews (ChatMessageView a, ChatMessageView b)
        {
            return a.Index - b.Index;
        }

        public void InsertMessageViewAtEnd ()
        {
            MessageCount += 1;
            bool isAtBottom = IsScrolledToBottom;
            var index = MessageCount - 1;
            var messageView = DataSource.ChatMessageViewAtIndex (this, index);
            messageView.SizeToFit ();
            messageView.Frame = new CGRect (messageView.Frame.X, ScrollView.ContentSize.Height, messageView.Frame.Width, messageView.Frame.Height);
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width, ScrollView.ContentSize.Height + messageView.Frame.Height + MessageSpacing);
            if (isAtBottom) {
                messageView.Index = index;
                VisibleMessageViews.Add (messageView);
                ScrollView.AddSubview (messageView);
                ScrollToBottom (true);
            } else {
                EnqueueReusableChatMessageView (messageView);
            }
        }

        public void ScrollToBottom (bool animated = false)
        {
            ScrollView.SetContentOffset (new CGPoint (0.0f, Math.Max (0, ScrollView.ContentSize.Height - ScrollView.Bounds.Height)), animated);
        }

        [Foundation.Export("scrollViewDidScroll:")]
        public void Scrolled (UIScrollView scrollView)
        {
            if (MessageCount == 0) {
                return;
            }
            var topY = scrollView.ContentOffset.Y;
            var bottomY = topY + scrollView.Bounds.Height;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i){
                var messageView = VisibleMessageViews [i];
                if ((messageView.Frame.Y >= scrollView.ContentOffset.Y + scrollView.Bounds.Height) || (messageView.Frame.Y <= scrollView.ContentOffset.Y)) {
                    messageView.RemoveFromSuperview ();
                    VisibleMessageViews.RemoveAt (i);
                    EnqueueReusableChatMessageView (messageView);
                }
            }
            if (VisibleMessageViews.Count == 0) {
                // This can happen if we've scrolled so far that none of the previously visible messages are in the
                // window anymore.  It only happens (currently) if we are scrolled far up and a request is made to
                // scroll to the bottom.  Since we're at the bottom, we can just reload the data, which starts at the bottom.
                ReloadData ();
                return;
            }
            var firstVisibleView = VisibleMessageViews [0];
            var lastVisibleView = VisibleMessageViews [VisibleMessageViews.Count - 1];
            int index;
            var y = firstVisibleView.Frame.Y - MessageSpacing;
            if (TopCalculatedIndex > 0 && topY < Bounds.Height) {
                // TODO: calculate a couple more pages
            }
            while (topY < y && firstVisibleView.Index > 0) {
                index = firstVisibleView.Index - 1;
                firstVisibleView = DataSource.ChatMessageViewAtIndex(this, index);
                firstVisibleView.Index = index;
                VisibleMessageViews.Insert (0, firstVisibleView);
                firstVisibleView.SizeToFit ();
                firstVisibleView.Frame = new CGRect (firstVisibleView.Frame.X, y - firstVisibleView.Frame.Height, firstVisibleView.Frame.Width, firstVisibleView.Frame.Height);
                ScrollView.AddSubview (firstVisibleView);
                y -= firstVisibleView.Frame.Height + MessageSpacing;
            }
            y = lastVisibleView.Frame.Y + lastVisibleView.Frame.Height + MessageSpacing;
            while (bottomY > y && lastVisibleView.Index < MessageCount - 1) {
                index = lastVisibleView.Index + 1;
                lastVisibleView = DataSource.ChatMessageViewAtIndex(this, index);
                lastVisibleView.Index = index;
                VisibleMessageViews.Add (lastVisibleView);
                lastVisibleView.SizeToFit ();
                lastVisibleView.Frame = new CGRect (lastVisibleView.Frame.X, y, lastVisibleView.Frame.Width, lastVisibleView.Frame.Height);
                ScrollView.AddSubview (lastVisibleView);
                y += lastVisibleView.Frame.Height + MessageSpacing;
            }
        }

        public override void LayoutSubviews ()
        {
            bool scrolledToBottom = IsScrolledToBottom;
            base.LayoutSubviews ();
            ScrollView.Frame = Bounds;
            if (scrolledToBottom) {
                ScrollToBottom ();
            }
        }

    }

    public class ChatMessageView : UIView
    {
        bool IsFromMe;
        public ChatView ChatView;
        public UIView BubbleView;
        McEmailMessage Message;
        UILabel MessageLabel;
        public int Index;
        UIEdgeInsets MessageInsets;
        nfloat BubbleSideInset = 15.0f;
        nfloat MaxBubbleWidthPercent = 0.75f;

        public ChatMessageView (CGRect frame) : base (frame)
        {
            MessageInsets = new UIEdgeInsets (6.0f, 9.0f, 6.0f, 9.0f);
            BubbleView = new UIView (new CGRect(0.0f, 0.0f, Bounds.Width * 0.75f, Bounds.Height));
            BubbleView.BackgroundColor = UIColor.White;
            BubbleView.Layer.MasksToBounds = true;
            BubbleView.Layer.BorderWidth = 1.0f;
            BubbleView.Layer.CornerRadius = 8.0f;
            MessageLabel = new UILabel (new CGRect(MessageInsets.Left, MessageInsets.Top, BubbleView.Bounds.Width - MessageInsets.Left - MessageInsets.Right, BubbleView.Bounds.Height - MessageInsets.Top - MessageInsets.Bottom));
            MessageLabel.Lines = 0;
            MessageLabel.Font = A.Font_AvenirNextRegular17;
            MessageLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            BubbleView.AddSubview (MessageLabel);
            AddSubview (BubbleView);
        }

        public void SetMessage(McEmailMessage message, bool isFromMe)
        {
            Message = message;
            IsFromMe = isFromMe;
            Update ();
        }

        public void Update ()
        {
            if (Message == null) {
                MessageLabel.Text = "";
            }else{
                var bundle = new NcEmailMessageBundle (Message);
                if (bundle.NeedsUpdate) {
                    MessageLabel.Text = "!! " + Message.BodyPreview;  
                } else {
                    MessageLabel.Text = bundle.TopText;
                }
            }
            if (IsFromMe) {
                BubbleView.BackgroundColor = A.Color_NachoGreen;
                BubbleView.Layer.BorderColor = A.Color_NachoGreen.CGColor;
                MessageLabel.TextColor = UIColor.White;
            } else {
                BubbleView.BackgroundColor = UIColor.White;
                BubbleView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                MessageLabel.TextColor = A.Color_NachoGreen;
            }
            MessageLabel.BackgroundColor = BubbleView.BackgroundColor;
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            LayoutBubbleView ();
        }

        void LayoutBubbleView ()
        {
            var maxMessageWidth = Bounds.Width * MaxBubbleWidthPercent - MessageInsets.Left - MessageInsets.Right;
            var messageSize = MessageLabel.SizeThatFits (new CGSize (maxMessageWidth, 99999999.0f));
            var height = messageSize.Height + MessageInsets.Top + MessageInsets.Bottom;
            nfloat width;
            if (messageSize.Height > MessageLabel.Font.LineHeight + 5.0f) {
                width = maxMessageWidth;
            } else {
                width = messageSize.Width + MessageInsets.Left + MessageInsets.Right;
            }
            if (IsFromMe) {
                BubbleView.Frame = new CGRect (Bounds.Width - BubbleView.Frame.Width - BubbleSideInset, 0.0f, width, height);
            } else {
                BubbleView.Frame = new CGRect (BubbleSideInset, 0.0f, width, height);
            }
        }

        public override void SizeToFit ()
        {
            LayoutBubbleView ();
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, BubbleView.Frame.Size.Height);
        }

    }
}

