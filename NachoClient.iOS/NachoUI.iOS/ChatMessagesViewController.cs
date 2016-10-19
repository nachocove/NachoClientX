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
    public class ChatMessagesViewController : NcUIViewControllerNoLeaks, INachoContactChooserDelegate, ChatViewDataSource, ChatViewDelegate, INachoFileChooserParent, AccountPickerViewControllerDelegate, ChatMessageComposeDelegate
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
        Dictionary<string, int> PendingSendMap;
        List<McAttachment> AttachmentsForUnsavedChat;
        int MessageCount;
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
            AttachmentsForUnsavedChat = new List<McAttachment> ();
            AttachmentsByMessageId = new Dictionary<int, List<McAttachment>> ();
            PendingSendMap = new Dictionary<string, int> ();
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
            ComposeView.ComposeDelegate = this;
            ComposeView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            View.AddSubview (HeaderView);
            View.AddSubview (ChatView);
            View.AddSubview (ComposeView);
            if (Chat == null) {
                HeaderView.EditingEnabled = true;
            } else {
                UpdateForChat ();
                ComposeView.SetMessage (Chat.DraftMessage);
                var attachments = McAttachment.QueryByItemId (Chat.AccountId, Chat.Id, McAbstrFolderEntry.ClassCodeEnum.Chat);
                foreach (var attachment in attachments) {
                    ComposeView.AddAttachment (attachment);
                }
            }
            UpdateFromField ();
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
            if (Chat != null) {
                if ((text == null && Chat.DraftMessage != null) || (text != null && Chat.DraftMessage == null) || (text != null && Chat.DraftMessage != null && !text.Equals (Chat.DraftMessage))) {
                    Chat.DraftMessage = text;
                    Chat.Update ();
                }
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
            ComposeView.SetEnabled (false);
            if (Chat == null) {
                var addresses = HeaderView.ToView.AddressList;
                Chat = McChat.ChatForAddresses (Account.Id, addresses);
                if (Chat == null) {
                    Log.Error (Log.LOG_CHAT, "Got null chat when sending new message");
                    return;
                }
                Chat.ClearDraft ();
                foreach (var attachment in AttachmentsForUnsavedChat) {
                    attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
                }
                AttachmentsForUnsavedChat.Clear ();
                UpdateForChat ();
                ReloadMessages ();
            }
            var text = ComposeView.GetMessage ();
            var previousMessages = new List<McEmailMessage> ();
            for (int i = Messages.Count - 1; i >= Messages.Count - 5 && i >= 0; --i){
                previousMessages.Add (Messages [i]);
            }
            ChatMessageComposer.SendChatMessage (Chat, text, previousMessages, (McEmailMessage message, NcResult result) => {
                if (IsViewLoaded){
                    ComposeView.SetEnabled(true);
                    if (result.isOK()){
                        Chat.AddMessage (message);
                        ComposeView.Clear ();
                        ChatView.ScrollToBottom ();
                        PendingSendMap.Add(result.Value as string, message.Id);
                    }else{
                        NcAlertView.ShowMessage (this, "Could not send messasge", "Sorry, there was a problem sending the message.  Please try again.");
                    }
                }
            });
        }

        void Resend (McEmailMessage message)
        {
            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if (pending != null) {
                pending.Delete ();
            }
            var result = EmailHelper.SendTheMessage (message, null);
            if (result.isOK ()) {
                PendingSendMap.Add (result.Value as string, message.Id);
            }
        }

        public void ShowParticipantDetails ()
        {
            if (ParticipantsByEmailId.Count == 1) {
                var participant = ParticipantsByEmailId.Values.First();
                var contactDetailViewController = new ContactDetailViewController ();
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
            var helper = new AddAttachmentViewController.MenuHelper (this, Account, View);
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
                if (Chat == null) {
                    AttachmentsForUnsavedChat.Add (attachment);
                } else {
                    attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
                }
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
            if (Chat == null) {
                AttachmentsForUnsavedChat.Add (attachment);
            } else {
                attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
            }
            ComposeView.AddAttachment (attachment);
        }

        public void AttachmentUpdated (McAttachment attachment)
        {
            ComposeView.UpdateAttachment (attachment);
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
            if (Chat == null) {
                for (int i = 0; i < AttachmentsForUnsavedChat.Count; ++i) {
                    if (AttachmentsForUnsavedChat [i].Id == attachment.Id) {
                        AttachmentsForUnsavedChat.RemoveAt (i);
                        break;
                    }
                }
            } else {
                attachment.Unlink (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
            }
        }

        // User tapping the from field
        public void ChooseAccount ()
        {
            var picker = new AccountPickerViewController ();
            picker.PickerDelegate = this;
            picker.Accounts = new List<McAccount> (McAccount.QueryByAccountCapabilities (McAccount.AccountCapabilityEnum.EmailSender).Where((McAccount a) => { return a.AccountType != McAccount.AccountTypeEnum.Unified; }));
            picker.SelectedAccount = Account;
            NavigationController.PushViewController (picker, true);
        }

        public void AccountPickerDidPickAccount (AccountPickerViewController vc, McAccount account)
        {
            NavigationController.PopViewController (true);
            Account = account;
            UpdateFromField ();
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
                        var chatId = Convert.ToInt32 (s.Tokens [0]);
                        var messageId = Convert.ToInt32 (s.Tokens [1]);
                        if (chatId == Chat.Id) {
                            var message = McEmailMessage.QueryById<McEmailMessage> (messageId);
                            if (message != null) {
                                if (LoadedMessageIDs.ContainsKey (message.MessageID)) {
                                    // Swap out the old message, which was possibly deleted, with the new one.
                                    for (int i = Messages.Count - 1; i >= 0; --i) {
                                        var existingMessage = Messages [i];
                                        if (existingMessage.MessageID == message.MessageID) {
                                            Messages [i] = message;
                                            break;
                                        }
                                    }
                                } else {
                                    Messages.Add (message);
                                    MessageCount += 1;
                                    LoadedMessageIDs.Add (message.MessageID, true);
                                    ChatView.InsertMessageViewAtEnd ();
                                }
                            }
                        }
                    } else if (s.Status.SubKind == NcResult.SubKindEnum.Info_EmailMessageSendSucceeded) {
                        var pendingToken = s.Tokens [0];
                        if (PendingSendMap.ContainsKey (pendingToken)) {
                            PendingSendMap.Remove (pendingToken);
                        }
                    } else if (s.Status.SubKind == NcResult.SubKindEnum.Error_EmailMessageSendFailed || s.Status.SubKind == NcResult.SubKindEnum.Error_EmailMessageReplyFailed) {
                        var pendingToken = s.Tokens [0];
                        if (PendingSendMap.ContainsKey (pendingToken)) {
                            var messageId = PendingSendMap [pendingToken];
                            PendingSendMap.Remove (pendingToken);
                            for (int i = Messages.Count - 1; i >= 0; --i) {
                                var index = i + MessageCount - Messages.Count;
                                var existingMessage = Messages [i];
                                if (existingMessage.Id == messageId) {
                                    var messageView = ChatView.MessageViewAtIndex (index);
                                    if (messageView != null) {
                                        messageView.Update (forceHasError: true);
                                        UpdateMessageViewBlockProperties (ChatView, messageView.Index, messageView);
                                    }
                                    break;
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
        }

        void UpdateFromField ()
        {
            HeaderView.FromView.ValueLabel.Text = Account.EmailAddr;
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

        public void ChatMessageViewDidSelectError (ChatView chatView, int index)
        {
            var messageView = ChatView.MessageViewAtIndex (index);
            var indexInArray = index - MessageCount + Messages.Count;
            if (indexInArray >= 0) {
                var message = Messages [indexInArray];
                NcAlertView.Show (this, "Could not send message", "Sorry, there was an issue sending the message.", new NcAlertAction[] {
                    new NcAlertAction("OK", null),
                    new NcAlertAction("Try Again", () => { 
                        Resend (message);
                        if (messageView != null){
                            messageView.Update ();
                            UpdateMessageViewBlockProperties (ChatView, messageView.Index, messageView);
                        }
                    })
                });
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
            var chooserController = new ContactChooserViewController ();
            chooserController.SetOwner (this, Account, address, NachoContactType.EmailRequired);
            FadeCustomSegue.Transition (this, chooserController);
        }

        public void ShowContactSearch (NcEmailAddress address)
        {
            var searchController = new ContactSearchViewController ();
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
                MimeKit.MailboxAddress mailbox;
                if (!MimeKit.MailboxAddress.TryParse (address.address, out mailbox) || null == mailbox) {
                    NcAlertView.ShowMessage (NavigationController.TopViewController, "Invalid Email Address", String.Format ("Sorry, the email address you provided does not appear to be valid."));
                } else {
                    if (Chat != null) {
                        Chat = null;
                        ReloadMessages ();
                        HeaderView.ToView.Clear ();
                        foreach (var participant in ParticipantsByEmailId.Values) {
                            var participantAddress = new NcEmailAddress (NcEmailAddress.Kind.To, participant.EmailAddress);
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

        #region Compose Delegate

        public void ChatComposeDidSend (ChatMessageComposeView composeView)
        {
            Send ();
        }

        public void ChatComposeWantsAttachment (ChatMessageComposeView composeView)
        {
            Attach ();
        }

        public void ChatComposeShowAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
            ShowAttachment (attachment);
        }

        public void ChatComposeDidRemoveAttachment (ChatMessageComposeView composeView, McAttachment attachment)
        {
            RemoveAttachment (attachment);
        }

        public bool ChatComposeCanSend (ChatMessageComposeView composeView)
        {
            return CanSend;
        }

        public void ChatComposeChangedHeight (ChatMessageComposeView composeView)
        {
            SubviewChangedHeight ();
        }

        #endregion

        public void ChatMessageViewNeedsLoad (ChatView chatView, McEmailMessage message)
        {
        }

        public void ChatViewDidSelectMessage (ChatView chatView, int index)
        {
        }
    }

    public class ChatMessagesHeaderView : UIView, IUcAddressBlockDelegate
    {

        public readonly ComposeFieldLabel FromView;
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
        UIView FromBorderView;
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
            DisclosureIndicator.UserInteractionEnabled = true;
            var x = Bounds.Width - DisclosureIndicator.Frame.X;
            ParticipantsLabel = new UILabel (new CGRect(x, 0.0f, Bounds.Width - 2.0f * x, Bounds.Height));
            ParticipantsLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            ParticipantsLabel.Font = A.Font_AvenirNextDemiBold17;
            ParticipantsLabel.TextAlignment = UITextAlignment.Center;
            ParticipantsLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            ParticipantsLabel.TextColor = A.Color_NachoGreen;
            ParticipantsLabel.Lines = 1;
            ParticipantsLabel.UserInteractionEnabled = true;
            FromView = new ComposeFieldLabel (new CGRect (0, 0, Bounds.Width, 42.0f));
            FromView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            FromView.NameLabel.Font = A.Font_AvenirNextMedium14;
            FromView.ValueLabel.Font = FromView.NameLabel.Font;
            FromView.NameLabel.TextColor = A.Color_NachoDarkText;
            FromView.ValueLabel.TextColor = FromView.ValueLabel.TextColor;
            FromView.NameLabel.Text = "From: ";
            FromView.Action = SelectFrom;
            FromView.LeftPadding = 15.0f;
            FromView.RightPadding = 15.0f;
            FromView.SetNeedsLayout ();
            FromView.Hidden = true;
            FromBorderView = new UIView (new CGRect (0.0f, Bounds.Height - 1.0f, Bounds.Size.Width, 1.0f));
            FromBorderView.BackgroundColor = A.Color_NachoBorderGray;
            FromBorderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleTopMargin;
            FromBorderView.Hidden = true;
            AddSubview (FromView);
            AddSubview (FromBorderView);
            AddSubview (ToView);
            AddSubview (DisclosureIndicator);
            AddSubview (ParticipantsLabel);
            AddSubview (BottomBorderView);
            ParticipantsLabel.AddGestureRecognizer (new UITapGestureRecognizer (TapPartcipantLabel));
            DisclosureIndicator.AddGestureRecognizer (new UITapGestureRecognizer (TapPartcipantLabel));
        }

        void TapPartcipantLabel ()
        {
            if (!EditingEnabled){
                ChatViewController.ShowParticipantDetails ();
            }
        }

        void SelectFrom ()
        {
            ChatViewController.ChooseAccount ();
        }

        bool ShowFromSelector {
            get {
                return NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified;
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
                preferredHeight = (nfloat)Math.Max (43.0f, ToView.Frame.Size.Height + BottomBorderView.Frame.Height);
                ToView.Hidden = false;
                ParticipantsLabel.Hidden = true;
                DisclosureIndicator.Hidden = true;
                if (ShowFromSelector) {
                    var frame = FromBorderView.Frame;
                    frame.Y = ToView.Frame.Y + ToView.Frame.Height;
                    FromBorderView.Frame = frame;
                    FromView.Frame = new CGRect (0.0f, FromBorderView.Frame.Y + FromBorderView.Frame.Height, Bounds.Width, FromView.Frame.Height);
                    FromView.Hidden = false;
                    preferredHeight += FromView.Frame.Height + FromBorderView.Frame.Height;
                } else {
                    FromView.Hidden = true;
                }
            } else {
                ToView.Hidden = true;
                ParticipantsLabel.Hidden = false;
                DisclosureIndicator.Hidden = false;
                FromView.Hidden = true;
            }
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            if ((Math.Abs (preferredHeight - previousPreferredHeight) > 0.5) && ChatViewController != null) {
                ChatViewController.SubviewChangedHeight ();
            }
        }
    }

}

