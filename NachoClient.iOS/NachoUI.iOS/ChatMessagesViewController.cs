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
    public class ChatMessagesViewController : NcUIViewControllerNoLeaks, INachoContactChooserDelegate
    {

        public McAccount Account;
        public McChat Chat;
        UITableView TableView;
        ChatMessagesHeaderView HeaderView;
        ChatMessageComposeView ComposeView;
        ChatMessageTableSource Source;
        UIStoryboard mainStorybaord;
        UIStoryboard MainStoryboard {
            get {
                if (mainStorybaord == null) {
                    mainStorybaord = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                }
                return mainStorybaord;
            }

        }

        public ChatMessagesViewController ()
        {
            HidesBottomBarWhenPushed = true;
        }

        protected override void CreateViewHierarchy ()
        {
            CGRect headerFrame = new CGRect (0.0f, 0.0f, View.Bounds.Width, 44.0f);
            CGRect composeFrame = new CGRect (0.0f, View.Bounds.Height - 44.0f, View.Bounds.Width, 44.0f);
            CGRect tableFrame = new CGRect (0.0f, headerFrame.Height, View.Bounds.Width, View.Bounds.Height - headerFrame.Height - composeFrame.Height);
            HeaderView = new ChatMessagesHeaderView (headerFrame);
            HeaderView.ChatViewController = this;
            HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            TableView = new UITableView (tableFrame);
            TableView.AutoresizingMask = UIViewAutoresizing.FlexibleHeight | UIViewAutoresizing.FlexibleWidth;
            ComposeView = new ChatMessageComposeView (composeFrame);
            ComposeView.ChatViewController = this;
            ComposeView.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            View.AddSubview (HeaderView);
            View.AddSubview (TableView);
            View.AddSubview (ComposeView);
            if (Chat == null) {
                HeaderView.EditingEnabled = true;
            } else {
                HeaderView.EditingEnabled = false;
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (Source == null && Chat != null) {
                TableView.Source = Source = new ChatMessageTableSource (Chat, this);
                TableView.ReloadData ();
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Account != null) {
                if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified || NcApplication.Instance.Account.Id == s.Account.Id) {
                    if (s.Status.SubKind == NcResult.SubKindEnum.Info_ChatMessageSetChanged) {
                        // TODO: show new messages
                    }
                }
            }
        }

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
                // TODO: update header to be locked
                // TODO: load messages (might have some if we matched an existing chat)
            }
            var text = ComposeView.GetMessage ();
            ChatMessageComposer.SendChatMessage (Chat, text, null, (McEmailMessage message) => {
                // TODO: this message is in the outbox.  It will be deleted and replaced by a message from the sent folder
                Chat.AddMessage (message);
            });
            // TODO: progress?
            // TODO: add message to table view
        }

        protected override void OnKeyboardChanged ()
        {
            base.OnKeyboardChanged ();
            ComposeView.Frame = new CGRect (
                ComposeView.Frame.X,
                View.Bounds.Height - keyboardHeight - ComposeView.Frame.Height,
                ComposeView.Frame.Width,
                ComposeView.Frame.Height
            );
            SubviewChangedHeight ();
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }

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

        public void SubviewChangedHeight ()
        {
            var y = HeaderView.Frame.Top + HeaderView.Frame.Height;
            TableView.Frame = new CGRect (
                TableView.Frame.X,
                y,
                TableView.Frame.Width,
                ComposeView.Frame.Top - y
            );
        }
    }

    public class ChatMessagesHeaderView : UIView, IUcAddressBlockDelegate
    {

        public readonly UcAddressBlock ToView;
        public bool EditingEnabled;
        public ChatMessagesViewController ChatViewController;

        public ChatMessagesHeaderView (CGRect frame) : base (frame)
        {
            ToView = new UcAddressBlock (this, "To:", null, Bounds.Width);
            ToView.SetCompact (false, -1);
            ToView.ConfigureView ();
            ToView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            AddSubview (ToView);
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
            base.LayoutSubviews ();
            var preferredHeight = (nfloat)Math.Max(42.0f, ToView.Frame.Size.Height);
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            if ((Math.Abs (preferredHeight - previousPreferredHeight) > 0.5) && ChatViewController != null) {
                ChatViewController.SubviewChangedHeight ();
            }
        }
    }

    public class ChatMessageComposeView : UIView
    {

        public ChatMessagesViewController ChatViewController;
        UITextField MessageField;
        UIButton SendButton;

        public ChatMessageComposeView (CGRect frame) : base (frame)
        {
            BackgroundColor = UIColor.Red;
            MessageField = new UITextField (Bounds);
            MessageField.Placeholder = "Type your message here...";
            SendButton = new UIButton (UIButtonType.Custom);
            SendButton.SetTitle ("Send", UIControlState.Normal);
            SendButton.TouchUpInside += Send;
            AddSubview (MessageField);
            AddSubview (SendButton);
        }

        void Send (object sender, EventArgs e)
        {
            ChatViewController.Send ();
        }

        public string GetMessage ()
        {
            return MessageField.Text;
        }

        public override void LayoutSubviews ()
        {
            SendButton.SizeToFit ();
            SendButton.Frame = new CGRect (
                Bounds.Width - SendButton.Frame.Size.Width,
                0.0f,
                SendButton.Frame.Size.Width,
                Bounds.Height
            );
            MessageField.Frame = new CGRect (
                0.0f,
                0.0f,
                SendButton.Frame.X,
                Bounds.Height
            );
        }

    }

    public class ChatMessageTableSource : UITableViewSource
    {

        McChat Chat;
        ChatMessagesViewController ChatViewController;
        List<McEmailMessage> Messages;
        const string MessageCellIdentifier = "message";

        public ChatMessageTableSource (McChat chat, ChatMessagesViewController viewController)
        {
            Chat = chat;
            ChatViewController = viewController;
            Messages = McChatMessage.GetChatMessages (Chat.Id);
            Messages.Reverse ();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return Messages.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (MessageCellIdentifier) as ChatMessageTableViewCell;
            if (cell == null) {
                cell = new ChatMessageTableViewCell (MessageCellIdentifier);
            }
            var message = Messages [indexPath.Row];
            if (!message.IsRead) {
                EmailHelper.MarkAsRead (message, true);
            }
            cell.Message = message;
            return cell;
        }
    }

    public class ChatMessageTableViewCell : UITableViewCell
    {
        McEmailMessage _Message;
        public McEmailMessage Message {
            get {
                return _Message;
            }
            set {
                if (_Message == null || value == null || _Message.Id != value.Id) {
                    _Message = value;
                    Update ();
                }
            }
        }

        public ChatMessageTableViewCell (string identifier) : base (UITableViewCellStyle.Default, identifier)
        {
        }

        public void Update ()
        {
            var bundle = new NcEmailMessageBundle (Message);
            if (bundle.NeedsUpdate) {
                TextLabel.Text = "!! " + Message.BodyPreview;  
            } else {
                TextLabel.Text = bundle.TopText;
            }
        }

    }
}

