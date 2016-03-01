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
using System.Linq;

namespace NachoClient.iOS
{
    public class ChatMessagesViewController : NcUIViewControllerNoLeaks, INachoContactChooserDelegate, ChatViewDataSource, ChatViewDelegate, INachoFileChooserParent
    {

        #region Properties

        const int MessagesPerQuery = 50;

        public McAccount Account;
        public McChat Chat;
        List<McEmailMessage> Messages;
        ChatView ChatView;
        ChatMessagesHeaderView HeaderView;
        ChatMessageComposeView ComposeView;
        bool IsListeningForStatusChanges;
        Dictionary<string, bool> LoadedMessageIDs;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;
        Dictionary<int, List<McAttachment>> AttachmentsByMessageId;
        int MessageCount;
        UIStoryboard mainStorybaord;
        UIStoryboard MainStoryboard {
            get {
                if (mainStorybaord == null) {
                    mainStorybaord = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                }
                return mainStorybaord;
            }

        }
        public bool CanSend {
            get {
                return Chat != null || !HeaderView.ToView.IsEmpty ();
            }
        }

        #endregion

        #region Constructors

        public ChatMessagesViewController ()
        {
            HidesBottomBarWhenPushed = true;
            Messages = new List<McEmailMessage> ();
            AttachmentsByMessageId = new Dictionary<int, List<McAttachment>> ();
            LoadedMessageIDs = new Dictionary<string, bool> ();
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            using (var image = UIImage.FromBundle ("chat-add-contact")) {
                NavigationItem.RightBarButtonItem = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, AddContact);
            }
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
                UpdateForChat ();

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

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (Chat == null) {
                HeaderView.ToView.SetEditFieldAsFirstResponder ();
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            var text = ComposeView.GetMessage ();
            if ((text == null && Chat.DraftMessage != null) || (text != null && Chat.DraftMessage == null) || (text != null && Chat.DraftMessage != null && !text.Equals(Chat.DraftMessage))) {
                Chat.DraftMessage = text;
                Chat.Update ();
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            StopListeningForStatusChanges ();
            base.ViewDidDisappear (animated);
        }

        protected override void OnKeyboardChanged ()
        {
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
                Chat = McChat.ChatForAddresses (Account.Id, addresses);
                if (Chat == null) {
                    Log.Error (Log.LOG_CHAT, "Got null chat when sending new message");
                    return;
                }
                Chat.ClearDraft ();
                UpdateForChat ();
                ReloadMessages ();
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

        public void ShowParticipantDetails ()
        {
            if (ParticipantsByEmailId.Count == 1) {
                var participant = ParticipantsByEmailId.Values.First();
                var contactDetailViewController = MainStoryboard.InstantiateViewController ("ContactDetailViewController") as ContactDetailViewController;
                contactDetailViewController.contact = McContact.QueryById<McContact> (participant.ContactId);
                NavigationController.PushViewController (contactDetailViewController, true);
            } else {
                var viewController = new ChatParticipantListViewController ();
                viewController.MessagesViewController = this;
                viewController.Participants = new List<McChatParticipant> (ParticipantsByEmailId.Values);
                NavigationController.PushViewController (viewController, true);
            }
        }

        void AddContact (object sender, EventArgs e)
        {
            var address = new NcEmailAddress (NcEmailAddress.Kind.To);
            ShowContactSearch (address);
        }

        public void Attach ()
        {
            var helper = new AddAttachmentViewController.MenuHelper (this, Account, MainStoryboard, View);
            PresentViewController (helper.MenuViewController, true, null);
        }

        // User picking a file as an attachment
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            var attachment = obj as McAttachment;
            if (attachment == null){
                var file = obj as McDocument;
                if (file != null) {
                    attachment = McAttachment.InsertSaveStart (Account.Id);
                    attachment.SetDisplayName (file.DisplayName);
                    attachment.UpdateFileCopy (file.GetFilePath ());
                } else {
                    var note = obj as McNote;
                    if (note != null) {
                        attachment = McAttachment.InsertSaveStart (Account.Id);
                        attachment.SetDisplayName (note.DisplayName + ".txt");
                        attachment.UpdateData (note.noteContent);
                    }
                }
            }

            if (attachment != null) {
                attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
                attachment.Update ();
                ComposeView.AddAttachment (attachment);
                this.DismissViewController (true, null);
            } else {
                NcAssert.CaseError ();
            }

        }

        // User adding an attachment from media browser
        public void Append (McAttachment attachment)
        {
            attachment.Update ();
            attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
            ComposeView.AddAttachment (attachment);
        }

        public void AttachmentUpdated (McAttachment attachment)
        {
            ComposeView.UpdateAttachment (attachment);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            DismissViewController (true, null);
        }

        // Not really a direct user action, but caused by the user selecting a date for the intent
        public void DismissPhotoPicker ()
        {
            DismissViewController (true, null);
        }

        public void PresentFileChooserViewController (UIViewController vc)
        {
            PresentViewController (vc, true, null);
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            attachment.Unlink (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
            attachment.Delete ();
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
                                    MessageCount += 1;
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

        void UpdateForChat ()
        {
            HeaderView.ParticipantsLabel.Text = Chat.CachedParticipantsLabel;
            HeaderView.EditingEnabled = false;
            ChatView.ShowPortraits = ChatView.ShowNameLabels = Chat.ParticipantCount > 1;
            ParticipantsByEmailId = McChatParticipant.GetChatParticipantsByEmailId (Chat.Id);
            ComposeView.SetMessage (Chat.DraftMessage);
            var attachments = McAttachment.QueryByItemId (Chat.AccountId, Chat.Id, McAbstrFolderEntry.ClassCodeEnum.Chat);
            foreach (var attachment in attachments) {
                ComposeView.AddAttachment (attachment);
            }
        }

        void ReloadMessages ()
        {
            if (Chat != null) {
                LoadedMessageIDs.Clear ();
                MessageCount = Chat.MessageCount ();
                Messages = Chat.GetMessages (0, MessagesPerQuery);
                Messages.Reverse ();
                foreach (var message in Messages) {
                    LoadedMessageIDs.Add (message.MessageID, true);
                }
                ChatView.ReloadData ();
                ChatView.ScrollToBottom ();
            } else {
                MessageCount = 0;
                Messages.Clear ();
                ChatView.ReloadData ();
            }
        }

        public int NumberOfMessagesInChatView (ChatView chatView)
        {
            return MessageCount;
        }

        public ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index)
        {
            var indexInArray = index - MessageCount + Messages.Count;
            if (indexInArray < 0) {
                var messages = Chat.GetMessages (Messages.Count, Math.Max (MessagesPerQuery, -indexInArray));
                messages.Reverse ();
                Messages.InsertRange (0, messages);
            }
            indexInArray = index - MessageCount + Messages.Count;
            McEmailMessage message = null;
            McChatParticipant particpant = null;
            List<McAttachment> attachments = null;
            if (indexInArray < 0) {
                // Hmmm...a message must have been deleted and messed up our count
                // Should rarely happen, and only if you scroll all the way back to the top
                // while a message gets deleted.
            } else {
                message = Messages [indexInArray];
                ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant);
                if (!message.IsRead) {
                    EmailHelper.MarkAsRead (message, true);
                }
                if (message.cachedHasAttachments) {
                    if (!AttachmentsByMessageId.ContainsKey (message.Id)) {
                        AttachmentsByMessageId.Add (message.Id, McAttachment.QueryByItemId (message.AccountId, message.Id, McAbstrFolderEntry.ClassCodeEnum.Email));
                    }
                    AttachmentsByMessageId.TryGetValue (message.Id, out attachments);
                }
            }
            var messageView = chatView.DequeueReusableChatMessageView ();
            messageView.OnAttachmentSelected = ShowAttachment;
            messageView.SetMessage (message, particpant, attachments);
            UpdateMessageViewBlockProperties (chatView, index, messageView);
            return messageView;
        }

        public void UpdateMessageViewBlockProperties (ChatView chatView, int index, ChatMessageView messageView)
        {
            var indexInArray = index - MessageCount + Messages.Count;
            if (indexInArray >= 0) {
                var message = Messages [indexInArray];
                var previous = indexInArray > 0 ? Messages [indexInArray - 1] : null;
                var next = indexInArray < Messages.Count - 1 ? Messages [indexInArray + 1] : null;
                var oneHour = TimeSpan.FromHours (1);
                var atTimeBlockStart = previous == null || (message.DateReceived - previous.DateReceived > oneHour);
                var atTimeBlockEnd = next == null || (next.DateReceived - message.DateReceived > oneHour);
                var atParticipantBlockStart = previous == null || previous.FromEmailAddressId != message.FromEmailAddressId;
                var atParticipantBlockEnd = next == null || next.FromEmailAddressId != message.FromEmailAddressId;
                var showName = atTimeBlockStart || atParticipantBlockStart;
                var showPortrait = atTimeBlockEnd || atParticipantBlockEnd;
                var showTimestamp = atTimeBlockStart;
                messageView.SetShowsName (showName);
                messageView.SetShowsPortrait (showPortrait);
                messageView.SetShowsTimestamp (showTimestamp);
            }
        }

        public void ShowAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.Complete == attachment.FilePresence) {
                PlatformHelpers.DisplayAttachment (this, attachment);
            }
        }

        #endregion

        #region Contact Choosing Ownership

        public void ShowContactChooser (NcEmailAddress address)
        {
            ContactChooserViewController chooserController = MainStoryboard.InstantiateViewController ("ContactChooserViewController") as ContactChooserViewController;
            chooserController.SetOwner (this, Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, chooserController);
        }

        public void ShowContactSearch (NcEmailAddress address)
        {
            ContactSearchViewController searchController = MainStoryboard.InstantiateViewController ("ContactSearchViewController") as ContactSearchViewController;
            searchController.SetOwner (this, Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, searchController);
        }

        public void UpdateEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
            if (String.IsNullOrEmpty (address.address)) {
                NcAlertView.ShowMessage (NavigationController.TopViewController, "No Email Address", String.Format ("Sorry, the contact you chose does not have an email address, which is required for chat."));
            }else if (String.Equals (address.address, Account.EmailAddr, StringComparison.OrdinalIgnoreCase)) {
                NcAlertView.ShowMessage (NavigationController.TopViewController, "Cannot Add Self", String.Format ("Sorry, but it's not possible to setup a chat with yourself ({0})", Account.EmailAddr));
            } else {
                var mailbox = MimeKit.MailboxAddress.Parse (address.address);
                if (mailbox == null) {
                    NcAlertView.ShowMessage (NavigationController.TopViewController, "Invalid Email Address", String.Format ("Sorry, the email address you provided does not appear to be valid."));
                } else {
                    if (Chat != null) {
                        Chat = null;
                        ReloadMessages ();
                        HeaderView.ToView.Clear ();
                        foreach (var participant in ParticipantsByEmailId.Values) {
                            var participantAddress = new NcEmailAddress(NcEmailAddress.Kind.To, participant.EmailAddress);
                            HeaderView.ToView.Append (participantAddress);
                        }
                        ParticipantsByEmailId = null;
                        HeaderView.EditingEnabled = true;
                    }
                    HeaderView.ToView.Append (address);
                    ComposeView.UpdateSendEnabled ();
                }
            }
        }

        public void DeleteEmailAddress (INachoContactChooser vc, NcEmailAddress address)
        {
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            // The contact chooser was pushed on the nav stack, rather than shown as a modal.
            // So we need to pop it from the stack
            vc.Cleanup ();
            NavigationController.PopToViewController (this, true);
            HeaderView.ToView.SetEditFieldAsFirstResponder ();
        }

        public void RemoveAddress (NcEmailAddress address)
        {
            ComposeView.UpdateSendEnabled ();
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
        UIImageView DisclosureIndicator;

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
            using (var image = UIImage.FromBundle ("chat-arrow-more")) {
                DisclosureIndicator = new UIImageView (image);
                DisclosureIndicator.Frame = new CGRect (Bounds.Width - image.Size.Width - 7.0f, (Bounds.Height - image.Size.Height) / 2.0f, image.Size.Width, image.Size.Height);
            }
            DisclosureIndicator.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin;
            var x = Bounds.Width - DisclosureIndicator.Frame.X;
            ParticipantsLabel = new UILabel (new CGRect(x, 0.0f, Bounds.Width - 2.0f * x, Bounds.Height));
            ParticipantsLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            ParticipantsLabel.Font = A.Font_AvenirNextDemiBold17;
            ParticipantsLabel.TextAlignment = UITextAlignment.Center;
            ParticipantsLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            ParticipantsLabel.TextColor = A.Color_NachoGreen;
            ParticipantsLabel.Lines = 1;
            AddSubview (ToView);
            AddSubview (DisclosureIndicator);
            AddSubview (ParticipantsLabel);
            AddSubview (BottomBorderView);
            AddGestureRecognizer (new UITapGestureRecognizer (Tap));
        }

        void Tap ()
        {
            if (!EditingEnabled){
                ChatViewController.ShowParticipantDetails ();
            }
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
            ChatViewController.ShowContactSearch (address);
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
                DisclosureIndicator.Hidden = true;
            } else {
                ToView.Hidden = true;
                ParticipantsLabel.Hidden = false;
                DisclosureIndicator.Hidden = false;
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
        UIButton AttachButton;
        UIButton SendButton;
        UIView TopBorderView;
        UILabel MessagePlaceholderLabel;
        List<UcAttachmentCell> AttachmentViews;
        UIScrollView AttachmentsScrollView;
        public static readonly nfloat STANDARD_HEIGHT = 41.0f;

        public ChatMessageComposeView (CGRect frame) : base (frame)
        {
            AttachmentViews = new List<UcAttachmentCell> ();
            TopBorderView = new UIView (new CGRect (0.0f, 0.0f, Bounds.Size.Width, 1.0f));
            TopBorderView.BackgroundColor = A.Color_NachoBorderGray;
            TopBorderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            BackgroundColor = UIColor.White;
            AttachButton = new UIButton (UIButtonType.Custom);
            using (var image = UIImage.FromBundle ("chat-attachfile")) {
                AttachButton.SetImage (image, UIControlState.Normal);
            }
            AttachButton.TouchUpInside += Attach;
            AttachButton.ContentMode = UIViewContentMode.Center;
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
            AttachmentsScrollView = new UIScrollView (new CGRect(0.0f, Bounds.Height, Bounds.Width, 0.0f));
            AddSubview (TopBorderView);
            AddSubview (AttachButton);
            AddSubview (MessageField);
            AddSubview (MessagePlaceholderLabel);
            AddSubview (SendButton);
            AddSubview (AttachmentsScrollView);
            UpdateSendEnabled ();
        }

        void MessageChanged (object sender, EventArgs e)
        {
            UpdateSendEnabled ();
            ResizeMessageField ();
        }

        void ResizeMessageField ()
        {
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

        void Attach (object sender, EventArgs e)
        {
            ChatViewController.Attach ();
        }

        public string GetMessage ()
        {
            return MessageField.Text;
        }

        public void SetMessage (string text)
        {
            if (text != null) {
                MessageField.Text = text;
            } else {
                MessageField.Text = "";
            }
            UpdateSendEnabled ();
            ResizeMessageField ();
        }

        public void Clear ()
        {
            MessageField.Text = "";
            UpdateSendEnabled ();
            foreach (var view in AttachmentViews) {
                view.RemoveFromSuperview ();
                view.Dispose ();
            }
            AttachmentViews.Clear ();
            SetNeedsLayout ();
        }

        public void UpdateSendEnabled ()
        {
            bool controllerCanSend = ChatViewController != null && ChatViewController.CanSend;
            bool hasText = !String.IsNullOrWhiteSpace (MessageField.Text);
            bool hasAttachment = AttachmentViews.Count > 0;
            SendButton.Enabled = controllerCanSend && (hasText || hasAttachment);
            MessagePlaceholderLabel.Hidden = hasText;
        }

        public override void LayoutSubviews ()
        {
            nfloat maxHeight = 5.0f * STANDARD_HEIGHT;
            var preferredHeight = MessageField.ContentSize.Height + 1.0f;
            if (AttachmentViews.Count > 0) {
                nfloat attachmentsHeight = 0.0f;
                nfloat y = 0.0f;
                foreach (var attachmentView in AttachmentViews) {
                    preferredHeight += attachmentView.Frame.Height;
                    attachmentView.Frame = new CGRect (0.0f, y, Bounds.Width, attachmentView.Frame.Height);
                    y += attachmentView.Frame.Height;
                    attachmentsHeight += attachmentView.Frame.Height;
                }
                AttachmentsScrollView.ContentSize = new CGSize (AttachmentsScrollView.Bounds.Width, attachmentsHeight);
                if (preferredHeight > maxHeight) {
                    attachmentsHeight = (nfloat)Math.Min (maxHeight - STANDARD_HEIGHT, attachmentsHeight);
                    preferredHeight = maxHeight;
                }
                AttachmentsScrollView.Frame = new CGRect (0.0f, preferredHeight - attachmentsHeight, Bounds.Width, attachmentsHeight);
            } else {
                preferredHeight = (nfloat)Math.Min (preferredHeight, maxHeight);
                AttachmentsScrollView.Frame = new CGRect (0.0f, preferredHeight, Bounds.Width, 0.0f);
            }
            var previousHeight = Frame.Height;
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            bool changedHeight = (Math.Abs (preferredHeight - previousHeight) > 0.5) && ChatViewController != null;
            SendButton.SizeToFit ();
            var sendButtonHeight = STANDARD_HEIGHT - 1.0f;
            SendButton.Frame = new CGRect (
                Bounds.Width - SendButton.Frame.Size.Width - SendButton.TitleEdgeInsets.Left - SendButton.TitleEdgeInsets.Right,
                AttachmentsScrollView.Frame.Y - sendButtonHeight,
                SendButton.Frame.Size.Width + SendButton.TitleEdgeInsets.Left + SendButton.TitleEdgeInsets.Right,
                sendButtonHeight
            );
            AttachButton.Frame = new CGRect (
                0.0f,
                AttachmentsScrollView.Frame.Y - STANDARD_HEIGHT,
                STANDARD_HEIGHT,
                STANDARD_HEIGHT
            );
            MessageField.Frame = new CGRect (
                AttachButton.Frame.X + AttachButton.Frame.Width,
                1.0f,
                SendButton.Frame.X,
                AttachmentsScrollView.Frame.Y - 1.0f
            );
            MessagePlaceholderLabel.SizeToFit ();
            MessagePlaceholderLabel.Frame = new CGRect (MessageField.Frame.X + 5.0f, MessageField.Frame.Y + 8.0f, MessagePlaceholderLabel.Frame.Width, MessagePlaceholderLabel.Frame.Height);
            if (changedHeight) {
                ChatViewController.SubviewChangedHeight ();
            }
        }

        public void AddAttachment (McAttachment attachment)
        {
            var view = new UcAttachmentCell (attachment, Bounds.Width, true, TapAttachment, RemoveAttachment);
            AttachmentsScrollView.AddSubview (view);
            AttachmentViews.Add (view);
            UpdateSendEnabled ();
            SetNeedsLayout ();
        }

        public void UpdateAttachment (McAttachment attachment)
        {
            foreach (var view in AttachmentViews) {
                if (view.attachment.Id == attachment.Id) {
                    view.attachment = attachment;
                    view.ConfigureView ();
                }
            }
        }

        public void TapAttachment (UcAttachmentCell cell)
        {
            ChatViewController.ShowAttachment (cell.attachment);
        }

        public void RemoveAttachment (UcAttachmentCell cell)
        {
            AttachmentViews.Remove (cell);
            cell.RemoveFromSuperview ();
            cell.Dispose ();
            UpdateSendEnabled ();
            SetNeedsLayout ();
            ChatViewController.RemoveAttachment (cell.attachment);
        }

    }


    public interface ChatViewDataSource {
        int NumberOfMessagesInChatView (ChatView chatView);
        ChatMessageView ChatMessageViewAtIndex (ChatView chatView, int index);
        void UpdateMessageViewBlockProperties (ChatView chatView, int index, ChatMessageView messageView);
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
        public bool ShowPortraits;
        public bool ShowNameLabels;
        public nfloat TimestampRevealProgress = 0.0f;
        public nfloat TimestampRevealWidth = 72.0f;
        public nfloat LastOffsetX = 0.0f;

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
            ScrollView.AlwaysBounceVertical = true;
            ScrollView.ShowsHorizontalScrollIndicator = false;
            ScrollView.AlwaysBounceHorizontal = false;
            ScrollView.DirectionalLockEnabled = true;
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
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width + TimestampRevealWidth, height);
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
            ScrollView.ContentSize = new CGSize (ScrollView.Bounds.Width + TimestampRevealWidth, ScrollView.ContentSize.Height + messageView.Frame.Height + MessageSpacing);
            if (isAtBottom) {
                messageView.Index = index;
                VisibleMessageViews.Add (messageView);
                ScrollView.AddSubview (messageView);
                ScrollToBottom (true);
            } else {
                EnqueueReusableChatMessageView (messageView);
            }
            foreach (var visibleView in VisibleMessageViews){
                DataSource.UpdateMessageViewBlockProperties (this, visibleView.Index, visibleView);
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
            if (TopCalculatedIndex > 0 && scrollView.ContentOffset.Y < Bounds.Height) {
                var i = TopCalculatedIndex - 1;
                nfloat extraHeight = 0.0f;
                ChatMessageView messageView;
                while (i >= 0 && extraHeight < ScrollView.Bounds.Height * 3.0) {
                    messageView = DataSource.ChatMessageViewAtIndex (this, i);
                    messageView.Index = i;
                    messageView.SizeToFit ();
                    EnqueueReusableChatMessageView (messageView);
                    extraHeight += messageView.Frame.Height + MessageSpacing;
                    TopCalculatedIndex = i;
                    --i;
                }
                ScrollView.ContentSize = new CGSize (ScrollView.ContentSize.Width, ScrollView.ContentSize.Height + extraHeight);
                foreach (var visibleView in VisibleMessageViews) {
                    var frame = visibleView.Frame;
                    frame.Y += extraHeight;
                    visibleView.Frame = frame;
                }
                ScrollView.ContentOffset = new CGPoint (ScrollView.ContentOffset.X, ScrollView.ContentOffset.Y + extraHeight);
            }
            if (scrollView.ContentOffset.X < 0.0f) {
                scrollView.ContentOffset = new CGPoint (0.0f, scrollView.ContentOffset.Y);
            }
            if (scrollView.ContentOffset.X > TimestampRevealWidth) {
                scrollView.ContentOffset = new CGPoint (TimestampRevealWidth, scrollView.ContentOffset.Y);
            }
            var topY = scrollView.ContentOffset.Y;
            var bottomY = topY + scrollView.Bounds.Height;
            for (int i = VisibleMessageViews.Count - 1; i >= 0; --i){
                var messageView = VisibleMessageViews [i];
                if ((messageView.Frame.Y >= scrollView.ContentOffset.Y + scrollView.Bounds.Height) || (messageView.Frame.Y + messageView.Frame.Height <= scrollView.ContentOffset.Y)) {
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
            if (Math.Abs (LastOffsetX - scrollView.ContentOffset.X) >= 0.5f) {
                TimestampRevealProgress = (nfloat)Math.Max (0.0f, Math.Min (1.0f, scrollView.ContentOffset.X / TimestampRevealWidth));
                foreach (var visibleView in VisibleMessageViews) {
                    visibleView.SetNeedsLayout ();
                    visibleView.LayoutIfNeeded ();
                }
            }
            LastOffsetX = scrollView.ContentOffset.X;
        }

        [Export("scrollViewWillEndDragging:withVelocity:targetContentOffset:")]
        public virtual void WillEndDragging (UIScrollView scrollView, CGPoint velocity, ref CGPoint targetContentOffset)
        {
            targetContentOffset.X = 0.0f;
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
        public ChatView ChatView;
        public UIView BubbleView;
        public AttachmentView.AttachmentSelectedCallback OnAttachmentSelected;
        McEmailMessage Message;
        McChatParticipant Participant;
        List<McAttachment> Attachments;
        UILabel MessageLabel;
        public int Index;
        UIEdgeInsets MessageInsets;
        nfloat BubbleSideInset = 15.0f;
        nfloat PortraitBubbleSpacing = 7.0f;
        nfloat TimestampRevealRightSpacing = 10.0f;
        nfloat MaxBubbleWidthPercent = 0.75f;
        UILabel TimestampDividerLabel;
        UILabel TimestampRevealLabel;
        UILabel _NameLabel;
        UILabel NameLabel {
            get {
                if (_NameLabel == null) {
                    var nameFont = A.Font_AvenirNextRegular14;
                    _NameLabel = new UILabel (new CGRect (0.0f, 0.0f, BubbleView.Frame.Width, nameFont.LineHeight));
                    _NameLabel.Lines = 1;
                    _NameLabel.LineBreakMode = UILineBreakMode.TailTruncation;
                    _NameLabel.Font = nameFont;
                    _NameLabel.TextColor = A.Color_NachoTextGray;
                    AddSubview (_NameLabel);
                }
                return _NameLabel;
            }
        }
        PortraitView _PortraitView = null;
        PortraitView PortraitView {
            get {
                if (_PortraitView == null) {
                    nfloat portraitSize = MessageLabel.Font.LineHeight + MessageInsets.Top + MessageInsets.Bottom;
                    _PortraitView = new PortraitView (new CGRect (BubbleSideInset, Bounds.Height - portraitSize, portraitSize, portraitSize));
                    _PortraitView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin;
                    AddSubview (_PortraitView);
                }
                return _PortraitView;
            }
        }
        List<ChatMessageAttachmentView> AttachmentViews;

        public ChatMessageView (CGRect frame) : base (frame)
        {
            AttachmentViews = new List<ChatMessageAttachmentView> ();
            MessageInsets = new UIEdgeInsets (6.0f, 9.0f, 6.0f, 9.0f);
            BubbleView = new UIView (new CGRect(0.0f, 0.0f, Bounds.Width * 0.75f, Bounds.Height));
            BubbleView.BackgroundColor = UIColor.White;
            BubbleView.Layer.MasksToBounds = true;
            BubbleView.Layer.BorderWidth = 1.0f;
            BubbleView.Layer.CornerRadius = 8.0f;
            MessageLabel = new UILabel (new CGRect(MessageInsets.Left, MessageInsets.Top, BubbleView.Bounds.Width - MessageInsets.Left - MessageInsets.Right, BubbleView.Bounds.Height - MessageInsets.Top - MessageInsets.Bottom));
            MessageLabel.Lines = 0;
            MessageLabel.Font = A.Font_AvenirNextRegular17;
            var timestampDividerFont = A.Font_AvenirNextDemiBold14;
            TimestampDividerLabel = new UILabel (new CGRect (0.0f, 0.0f, Bounds.Width, timestampDividerFont.LineHeight * 2.0f));
            TimestampDividerLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            TimestampDividerLabel.TextAlignment = UITextAlignment.Center;
            TimestampDividerLabel.Lines = 1;
            TimestampDividerLabel.Font = timestampDividerFont;
            TimestampDividerLabel.TextColor = A.Color_NachoTextGray;
            var timestampFont = A.Font_AvenirNextRegular14;
            TimestampRevealLabel = new UILabel (new CGRect (Bounds.Width, (Bounds.Height - timestampFont.LineHeight) / 2.0f, Bounds.Width, timestampFont.LineHeight));
            TimestampRevealLabel.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            TimestampRevealLabel.TextAlignment = UITextAlignment.Right;
            TimestampRevealLabel.Lines = 1;
            TimestampRevealLabel.Font = timestampFont;
            TimestampRevealLabel.TextColor = A.Color_NachoTextGray;
            AddSubview (TimestampDividerLabel);
            AddSubview (TimestampRevealLabel);
            BubbleView.AddSubview (MessageLabel);
            AddSubview (BubbleView);
        }

        public void SetMessage(McEmailMessage message, McChatParticipant participant, List<McAttachment> attachments)
        {
            Message = message;
            Participant = participant;
            Attachments = attachments;
            Update ();
        }

        public void SetShowsPortrait (bool showsPortrait)
        {
            if (ChatView != null && ChatView.ShowPortraits && Participant != null) {
                PortraitView.Hidden = !showsPortrait;
            }
        }

        public void SetShowsName (bool showsName)
        {
            if (ChatView != null && ChatView.ShowNameLabels && Participant != null) {
                NameLabel.Hidden = !showsName;
            }
        }

        public void SetShowsTimestamp (bool showsTimestamp)
        {
            TimestampDividerLabel.Hidden = !showsTimestamp;
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
                TimestampDividerLabel.Text = Pretty.VariableDayTime (Message.DateReceived);
                TimestampRevealLabel.Text = Pretty.Time (Message.DateReceived);
            }
            if (Participant == null) {
                BubbleView.BackgroundColor = A.Color_NachoGreen;
                BubbleView.Layer.BorderColor = A.Color_NachoGreen.CGColor;
                MessageLabel.TextColor = UIColor.White;
                if (ChatView != null && ChatView.ShowPortraits) {
                    PortraitView.SetPortrait (0, 0, "");
                    PortraitView.Hidden = true;
                }
                if (ChatView != null && ChatView.ShowNameLabels) {
                    NameLabel.Text = "";
                    NameLabel.Hidden = true;
                }
            } else {
                BubbleView.BackgroundColor = UIColor.White;
                BubbleView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
                MessageLabel.TextColor = A.Color_NachoGreen;
                if (ChatView != null && ChatView.ShowPortraits) {
                    PortraitView.SetPortrait (Participant.CachedPortraitId, Participant.CachedColor, Participant.CachedInitials);
                    PortraitView.Hidden = false;
                }
                if (ChatView != null && ChatView.ShowNameLabels) {
                    NameLabel.Text = Participant.CachedName;
                    NameLabel.Hidden = false;
                }
            }
            MessageLabel.BackgroundColor = BubbleView.BackgroundColor;
            TimestampDividerLabel.Hidden = false;
            UpdateAttachments ();
            SetNeedsLayout ();
        }

        void UpdateAttachments ()
        {
            foreach (var view in AttachmentViews) {
                view.RemoveFromSuperview ();
            }
            AttachmentViews.Clear ();
            if (Attachments != null) {
                foreach (var attachment in Attachments) {
                    var frame = new CGRect (0.0f, 0.0f, Bounds.Width, ChatMessageAttachmentView.VIEW_HEIGHT);
                    var view = new ChatMessageAttachmentView (frame, attachment);
                    BubbleView.AddSubview (view);
                    AttachmentViews.Add (view);
                    view.SetColors (MessageLabel.BackgroundColor, MessageLabel.TextColor);
                    view.OnAttachmentSelected = OnAttachmentSelected;
                }
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            LayoutBubbleView ();
            if (_PortraitView != null) {
                var x = BubbleSideInset;
                if (ChatView != null) {
                    x += (ChatView.TimestampRevealWidth - _PortraitView.Frame.Width - x) * ChatView.TimestampRevealProgress;
                }
                var frame = PortraitView.Frame;
                frame.X = x;
                PortraitView.Frame = frame;
            }
            if (ChatView != null) {
                var frame = TimestampDividerLabel.Frame;
                frame.X = (ChatView.TimestampRevealWidth * ChatView.TimestampRevealProgress);
                TimestampDividerLabel.Frame = frame;
                frame = TimestampRevealLabel.Frame;
                frame.Width = ChatView.TimestampRevealWidth - TimestampRevealRightSpacing;
                frame.Y = BubbleView.Frame.Y + (BubbleView.Frame.Height - frame.Height) / 2.0f;
                TimestampRevealLabel.Frame = frame;
            }
        }

        void LayoutBubbleView ()
        {
            var maxMessageWidth = (nfloat)Math.Floor (Bounds.Width * MaxBubbleWidthPercent - MessageInsets.Left - MessageInsets.Right);
            if (ChatView != null && ChatView.ShowPortraits && Participant != null) {
                // If we're showing portraits, they take up space, so we need to make the bubble smaller
                // than it would otherwise be.  Instead of removing the entire width added by the portrait,
                // just take a bit off.  BubbleSideInset is a good smallish size, but we could do anything here.
                maxMessageWidth -= BubbleSideInset;
            }
            var messageSize = MessageLabel.SizeThatFits (new CGSize (maxMessageWidth, 99999999.0f));
            if (messageSize.Height > MessageLabel.Font.LineHeight + 5.0f) {
                // If we've got more than one line, just use maxMessageWidth so consecutive mutli-line
                // messages line up.  Without this, each message would be a few pixels different because
                // of differences in how each message wraps before actually hitting maxMessageWidth
                messageSize.Width = maxMessageWidth;
            }
            if (AttachmentViews.Count > 0) {
                // If there's an attachment view, use the max width so there's enough room to show attachment info
                messageSize.Width = maxMessageWidth;
            }
            messageSize.Height = (nfloat)Math.Ceiling (messageSize.Height);
            MessageLabel.Frame = new CGRect (MessageInsets.Left, MessageInsets.Top, messageSize.Width, messageSize.Height);
            nfloat bubbleY = MessageLabel.Frame.Y + MessageLabel.Frame.Height;
            var bubbleSize = new CGSize (messageSize.Width + MessageInsets.Left + MessageInsets.Right, messageSize.Height + MessageInsets.Top + MessageInsets.Bottom);
            foreach (var attachmentView in AttachmentViews) {
                attachmentView.Frame = new CGRect (MessageInsets.Left, bubbleY, messageSize.Width, attachmentView.Frame.Height);
                bubbleSize.Height += attachmentView.Frame.Height;
                bubbleY += attachmentView.Frame.Height;
            }
            nfloat x = 0.0f;
            nfloat y = 0.0f;
            if (!TimestampDividerLabel.Hidden) {
                y = TimestampDividerLabel.Frame.Y + TimestampDividerLabel.Frame.Height;
            }
            if (Participant == null) {
                x = Bounds.Width - BubbleView.Frame.Width - BubbleSideInset;
                BubbleView.Frame = new CGRect (x, y, bubbleSize.Width, bubbleSize.Height);
            } else {
                x = BubbleSideInset;
                if (ChatView != null && ChatView.ShowPortraits) {
                    x += PortraitView.Frame.Width + PortraitBubbleSpacing;
                }
                if (ChatView != null) {
                    x += (ChatView.TimestampRevealWidth + BubbleSideInset - x) * ChatView.TimestampRevealProgress;
                }
                if (_NameLabel != null && !_NameLabel.Hidden) {
                    NameLabel.Frame = new CGRect (x, y, maxMessageWidth + MessageInsets.Left + MessageInsets.Right, NameLabel.Frame.Height);
                    y += NameLabel.Frame.Height;
                }
                BubbleView.Frame = new CGRect (x, y, bubbleSize.Width, bubbleSize.Height);
            }
        }

        public override void SizeToFit ()
        {
            LayoutBubbleView ();
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, BubbleView.Frame.Y + BubbleView.Frame.Size.Height);
        }

    }

    public class ChatMessageAttachmentView : AttachmentView 
    {

        public ChatMessageAttachmentView (CGRect frame, McAttachment attachment) : base(frame, attachment)
        {
            AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            HideSeparator ();
            if (attachment.IsInline && detailView.Text.StartsWith ("Inline ")) {
                detailView.Text = detailView.Text.Substring ("Inline ".Length);
            }
//            imageView.BackgroundColor = UIColor.White;
//            imageView.ContentMode = UIViewContentMode.Center;
//            imageView.ClipsToBounds = true;
//            imageView.Layer.CornerRadius = imageView.Frame.Size.Width / 2.0f;
        }

        public void SetColors (UIColor backgroundColor, UIColor foregroundColor)
        {
            BackgroundColor = backgroundColor;
            filenameView.BackgroundColor = backgroundColor;
            detailView.BackgroundColor = backgroundColor;
            filenameView.TextColor = foregroundColor;
            detailView.TextColor = foregroundColor;
        }
    }
}

