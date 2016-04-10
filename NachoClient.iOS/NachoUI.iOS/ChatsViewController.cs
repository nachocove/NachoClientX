//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using CoreGraphics;
using NachoCore.Index;

namespace NachoClient.iOS
{
    [Foundation.Register ("ChatsViewController")]
    public class ChatsViewController : NcUITableViewController, IUISearchResultsUpdating, IUISearchControllerDelegate
    {

        ChatsTableViewSource Source;
        SwitchAccountButton SwitchAccountButton;
        UIBarButtonItem NewChatButton;
        UISearchController SearchController;
        ChatsSearchResultsViewController SearchResultsViewController;
        NcIndex SearchIndex;
        Dictionary<int, McChat> ChatsByMessageId;
        bool needsReload;
        bool hasAppeared;

        public ChatsViewController () : base()
        {
            NavigationItem.LeftBarButtonItem = new UIBarButtonItem (UIBarButtonSystemItem.Search, ShowSearch);
            using (var image = UIImage.FromBundle ("chat-newmsg")) {
                NavigationItem.RightBarButtonItem = NewChatButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, NewChat);
            }
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "Chats";
            SearchResultsViewController = new ChatsSearchResultsViewController (this);
            SearchController = new UISearchController (SearchResultsViewController);
            SearchController.SearchResultsUpdater = this;
            SearchController.Delegate = this;
            DefinesPresentationContext = true;
            hasAppeared = false;
        }

        [Foundation.Export("willPresentSearchController:")]
        public virtual void WillPresentSearchController (UISearchController searchController)
        {
            NavigationController.NavigationBar.Translucent = true;
            var chatsById = new Dictionary<int, McChat> ();
            foreach (var chat in Source.Chats) {
                chatsById.Add (chat.Id, chat);
            }
            ChatsByMessageId = new Dictionary<int, McChat> ();
            foreach (var chatMessage in NcModel.Instance.Db.Table<McChatMessage>()) {
                McChat chat = null;
                chatsById.TryGetValue (chatMessage.ChatId, out chat);
                ChatsByMessageId.Add (chatMessage.MessageId, chat);
            }
        }

        [Foundation.Export("didDismissSearchController:")]
        public virtual void DidDismissSearchController (UISearchController searchController)
        {
            NavigationController.NavigationBar.Translucent = false;
            ChatsByMessageId.Clear ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            Util.ConfigureNavBar (false, NavigationController);

            SwitchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = SwitchAccountButton;

            SwitchToAccount (NcApplication.Instance.Account);

            TableView.RowHeight = ChatTableViewCell.HEIGHT;
            TableView.TableHeaderView = SearchController.SearchBar;
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
            if (!hasAppeared) {
                TableView.ContentOffset = new CGPoint (0.0f, TableView.TableHeaderView.Frame.Height);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            SwitchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            hasAppeared = true;
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
            Sync ();
            Source.Reset ();
            TableView.ReloadData ();
        }

        void Sync ()
        {
            if (Source.Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                EmailHelper.SyncUnified ();
                EmailHelper.SyncUnifiedSent ();
            } else {
                var inbox = McFolder.GetDefaultInboxFolder (Source.Account.Id);
                if (inbox != null) {
                    BackEnd.Instance.SyncCmd (inbox.AccountId, inbox.Id);
                }
                var sent = McFolder.GetDefaultInboxFolder (Source.Account.Id);
                if (sent != null) {
                    BackEnd.Instance.SyncCmd (sent.AccountId, sent.Id);
                }
            }
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            SearchIndex = new NcIndex (NcModel.Instance.GetIndexPath(account.Id));
            if (Source == null) {
                TableView.Source = Source = new ChatsTableViewSource (account, this);
                Sync ();
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

        void ShowSearch (object sender, EventArgs args)
        {
            TableView.SetContentOffset (new CGPoint (0.0f, 0.0f), false);
            SearchController.SearchBar.BecomeFirstResponder ();
        }

        public void ChatSelected (McChat chat)
        {
            NavigationController.NavigationBar.Translucent = false;
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = McAccount.QueryById<McAccount> (chat.AccountId);
            messagesViewController.Chat = chat;
            NavigationController.PushViewController (messagesViewController, true);
        }

        public virtual void UpdateSearchResultsForSearchController (UISearchController searchController)
        {
            var foundChatsById = new Dictionary<int, McChat> ();
            var chats = new List<McChat> ();
            var text = searchController.SearchBar.Text;
            if (!String.IsNullOrEmpty (text)) {
                var results = SearchIndex.SearchAllEmailMessageFields (text);
                foreach (var result in results) {
                    McChat chat = null;
                    ChatsByMessageId.TryGetValue (int.Parse (result.Id), out chat);
                    if (chat != null && !foundChatsById.ContainsKey(chat.Id)) {
                        chats.Add (chat);
                        foundChatsById.Add (chat.Id, chat);
                    }
                }
            }
            SearchResultsViewController.UpdateChats (chats);
        }

    }

    public class ChatsSearchResultsViewController : NcUITableViewController
    {

        ChatsSearchResultsTableViewSource Source;
        ChatsViewController ChatsViewController;

        public ChatsSearchResultsViewController (ChatsViewController viewController) : base()
        {
            AutomaticallyAdjustsScrollViewInsets = false;
            ChatsViewController = viewController;
        }

        public override void LoadView ()
        {
            TableView = new UITableView ();
            TableView.Source = Source = new ChatsSearchResultsTableViewSource (this);
            TableView.ContentInset = new UIEdgeInsets (64.0f, 0.0f, 50.0f, 0.0f);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            TableView.RowHeight = ChatTableViewCell.HEIGHT;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public void ChatSelected (McChat chat)
        {
            ChatsViewController.ChatSelected (chat);
        }

        public void UpdateChats (List<McChat> chats)
        {
            Source.Chats = chats;
            TableView.ReloadData ();
        }

    }

    public class ChatsSearchResultsTableViewSource : UITableViewSource
    {
        const string ChatCellIdentifier = "Chat";
        public List<McChat> Chats { get; set; }
        ChatsSearchResultsViewController ViewController;

        public ChatsSearchResultsTableViewSource (ChatsSearchResultsViewController viewController) : base ()
        {
            Chats = new List<McChat> ();
            ViewController = viewController;
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
            var chat = Chats [indexPath.Row];
            var cell = tableView.DequeueReusableCell (ChatCellIdentifier) as ChatTableViewCell;
            if (cell == null) {
                cell = new ChatTableViewCell (ChatCellIdentifier);
            }
            cell.Chat = chat;
            cell.ShowUnreadIndicator = false;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ViewController.ChatSelected (chat);
        }

    }

    public class ChatsTableViewSource : UITableViewSource
    {
        const string ChatCellIdentifier = "Chat";
        public List<McChat> Chats { get; private set; }
        public Dictionary<int, int> UnreadCountsByChat { get; private set; }
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
                UnreadCountsByChat = McChat.UnreadCountsByChat ();
            } else {
                Chats = McChat.LastestChatsForAccount (Account.Id);
                UnreadCountsByChat = McChat.UnreadCountsByChat (Account.Id);
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
            var chat = Chats [indexPath.Row];
            var cell = tableView.DequeueReusableCell (ChatCellIdentifier) as ChatTableViewCell;
            if (cell == null) {
                cell = new ChatTableViewCell (ChatCellIdentifier);
            }
            cell.Chat = chat;
            int unreadCount = 0;
            UnreadCountsByChat.TryGetValue (chat.Id, out unreadCount);
            cell.ShowUnreadIndicator = unreadCount > 0;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ViewController.ChatSelected (chat);
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
        public bool ShowUnreadIndicator {
            get {
                return !UnreadIndicator.Hidden;
            }
            set {
                UnreadIndicator.Hidden = !value;
            }
        }

        UILabel ParticipantsLabel;
        UILabel DateLabel;
        UILabel MessageLabel;
        UIImageView UnreadIndicator;
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

            using (var image = UIImage.FromBundle ("chat-stat-online")) {
                UnreadIndicator = new UIImageView (image);
            }
            UnreadIndicator.Hidden = true;
            PhotoContainerView.AddSubview (UnreadIndicator);

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
            UnreadIndicator.Frame = new CGRect (PhotoContainerView.Bounds.Width - UnreadIndicator.Frame.Width, 0.0f, UnreadIndicator.Frame.Width, UnreadIndicator.Frame.Height);
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

