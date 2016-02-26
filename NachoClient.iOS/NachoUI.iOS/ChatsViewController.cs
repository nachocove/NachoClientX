//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using CoreGraphics;

namespace NachoClient.iOS
{
    [Foundation.Register ("ChatsViewController")]
    public class ChatsViewController : NcUITableViewController
    {

        ChatsTableViewSource Source;
        SwitchAccountButton SwitchAccountButton;
        UIBarButtonItem NewChatButton;
        bool needsReload;

        public ChatsViewController (IntPtr ptr) : base(ptr)
        {
            using (var image = UIImage.FromBundle ("chat-newmsg")) {
                NavigationItem.RightBarButtonItem = NewChatButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, NewChat);
            }
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "Chats";
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            Util.ConfigureNavBar (false, NavigationController);

            SwitchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = SwitchAccountButton;

            SwitchToAccount (NcApplication.Instance.Account);

            TableView.RowHeight = ChatTableViewCell.HEIGHT;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (Source.Account.Id != NcApplication.Instance.Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            if (needsReload) {
                Reload ();
                needsReload = false;
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            SwitchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            needsReload = true;
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Account != null) {
                if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified || NcApplication.Instance.Account.Id == s.Account.Id) {
                    if (s.Status.SubKind == NcResult.SubKindEnum.Info_ChatSetChanged || s.Status.SubKind == NcResult.SubKindEnum.Info_ChatMessageAdded) {
                        Reload ();
                    }
                }
            }
        }

        void Reload ()
        {
            Source.Reset ();
            TableView.ReloadData ();
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            if (Source == null) {
                TableView.Source = Source = new ChatsTableViewSource (account, this);
            } else {
                Source.Account = account;
                Reload ();
            }
            NewChatButton.Enabled = account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender);
        }

        void NewChat (object sender, EventArgs args)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = NcApplication.Instance.DefaultEmailAccount;
            NavigationController.PushViewController (messagesViewController, true);
        }

        public void ChatSelected (McChat chat, Foundation.NSIndexPath indexPath)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = NcApplication.Instance.DefaultEmailAccount;
            messagesViewController.Chat = chat;
            NavigationController.PushViewController (messagesViewController, true);
        }

    }

    public class ChatsTableViewSource : UITableViewSource
    {
        const string ChatCellIdentifier = "Chat";
        public List<McChat> Chats { get; private set; }
        ChatsViewController ViewController;
        public McAccount Account;

        public ChatsTableViewSource (McAccount account, ChatsViewController viewController) : base ()
        {
            ViewController = viewController;
            Account = account;
            Reset ();
        }

        public void Reset ()
        {
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                Chats = McChat.LastestChats ();
            } else {
                Chats = McChat.LastestChatsForAccount (Account.Id);
            }
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return Chats.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            // TODO: might need to query for chat if outside previously queried range
            var chat = Chats [indexPath.Row];
            var cell = tableView.DequeueReusableCell (ChatCellIdentifier) as ChatTableViewCell;
            if (cell == null) {
                cell = new ChatTableViewCell (ChatCellIdentifier);
            }
            cell.Chat = chat;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ViewController.ChatSelected (chat, indexPath);
        }

    }

    public class ChatTableViewCell : UITableViewCell
    {
        McChat _Chat;
        public McChat Chat {
            get {
                return _Chat;
            }
            set {
                _Chat = value;
                Update ();
            }
        }

        UILabel ParticipantsLabel;
        UILabel DateLabel;
        UILabel MessageLabel;
        UIView PhotoContainerView;
        PortraitView PortraitView1;
        PortraitView PortraitView2;
        nfloat RightSpacing = 7.0f;
        public static nfloat HEIGHT = 72.0f;

        public ChatTableViewCell (string reuseIdentifier) : base (UITableViewCellStyle.Default, reuseIdentifier)
        {
            CGSize photoSize = new CGSize (40.0, 40.0);
            nfloat photoSpacing = (HEIGHT - photoSize.Height) / 2.0f;
            var participantFont = A.Font_AvenirNextDemiBold17;
            var messageFont = A.Font_AvenirNextRegular14;
            var dateFont = A.Font_AvenirNextRegular14;
            var topSpacing = (HEIGHT - participantFont.LineHeight - messageFont.LineHeight * 2.0f) / 2.0f;
            PhotoContainerView = new UIView (new CGRect(photoSpacing, photoSpacing, photoSize.Width, photoSize.Height));
            var dateBaselineAdjust = participantFont.Ascender - dateFont.Ascender;
            DateLabel = new UILabel (new CGRect(Bounds.Width - 40.0f - RightSpacing, topSpacing + dateBaselineAdjust, 40.0f, 20.0f));
            var x = PhotoContainerView.Frame.X + PhotoContainerView.Frame.Width + photoSpacing;
            ParticipantsLabel = new UILabel (new CGRect(x, topSpacing, DateLabel.Frame.X - x, participantFont.LineHeight));
            MessageLabel = new UILabel (new CGRect(x, ParticipantsLabel.Frame.Y + ParticipantsLabel.Frame.Height, Bounds.Width - RightSpacing - x, messageFont.LineHeight * 2.0f));

            ParticipantsLabel.Font = participantFont;
            ParticipantsLabel.TextColor = A.Color_NachoGreen;
            ParticipantsLabel.Lines = 1;
            ParticipantsLabel.LineBreakMode = UILineBreakMode.TailTruncation;

            MessageLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            MessageLabel.Font = messageFont;
            MessageLabel.TextColor = A.Color_NachoTextGray;
            MessageLabel.Lines = 2;
            ParticipantsLabel.LineBreakMode = UILineBreakMode.TailTruncation;

            DateLabel.Font = dateFont;
            DateLabel.TextColor = A.Color_NachoTextGray;

            PortraitView1 = new PortraitView (PhotoContainerView.Bounds);
            PortraitView2 = new PortraitView (PhotoContainerView.Bounds);
            PortraitView2.Layer.BorderColor = UIColor.White.CGColor;
            PortraitView2.Layer.BorderWidth = 1.0f;

            PhotoContainerView.AddSubview (PortraitView1);
            PhotoContainerView.AddSubview (PortraitView2);

            ContentView.AddSubview (PhotoContainerView);
            ContentView.AddSubview (ParticipantsLabel);
            ContentView.AddSubview (MessageLabel);
            ContentView.AddSubview (DateLabel);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            DateLabel.SizeToFit ();
            var frame = MessageLabel.Frame;
            MessageLabel.SizeToFit ();
            frame.Height = MessageLabel.Frame.Height;
            MessageLabel.Frame = frame;
            DateLabel.Frame = new CGRect (Bounds.Width - DateLabel.Frame.Width - RightSpacing, DateLabel.Frame.Y, DateLabel.Frame.Width, DateLabel.Frame.Height);
            ParticipantsLabel.Frame = new CGRect (ParticipantsLabel.Frame.X, ParticipantsLabel.Frame.Y, DateLabel.Frame.X - ParticipantsLabel.Frame.X, ParticipantsLabel.Frame.Height);
            if (Chat == null || Chat.ParticipantCount <= 1) {
                PortraitView1.Frame = PhotoContainerView.Bounds;
            } else {
                var size = new CGSize (PhotoContainerView.Bounds.Width * 0.67, PhotoContainerView.Bounds.Width * 0.67);
                PortraitView1.Frame = new CGRect (0.0f, 0.0f, size.Width, size.Height);
                PortraitView2.Frame = new CGRect (PhotoContainerView.Bounds.Width - size.Width, PhotoContainerView.Bounds.Height - size.Height, size.Width, size.Height);
            }
        }

        void Update ()
        {
            if (Chat == null) {
                ParticipantsLabel.Text = "";
                MessageLabel.Text = "";
                DateLabel.Text = "";
                PortraitView1.SetPortrait (0, 1, "");
                PortraitView2.SetPortrait (0, 1, "");
                PortraitView2.Hidden = true;
            } else {
                if (Chat.ParticipantCount <= 1) {
                    PortraitView1.SetPortrait (Chat.CachedPortraitId1, Chat.CachedColor1, Chat.CachedInitials1);
                    PortraitView2.SetPortrait (0, 1, "");
                    PortraitView2.Hidden = true;
                } else {
                    PortraitView1.SetPortrait (Chat.CachedPortraitId2, Chat.CachedColor2, Chat.CachedInitials2);
                    PortraitView2.SetPortrait (Chat.CachedPortraitId1, Chat.CachedColor1, Chat.CachedInitials1);
                    PortraitView2.Hidden = false;
                }
                ParticipantsLabel.Text = Chat.CachedParticipantsLabel;
                DateLabel.Text = Pretty.TimeWithDecreasingPrecision (Chat.LastMessageDate);
                if (Chat.LastMessagePreview == null) {
                    MessageLabel.Text = "";
                }else{
                    MessageLabel.Text = Chat.LastMessagePreview;
                }
            }
            SetNeedsLayout ();
        }
    }

}

