//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;
using CoreGraphics;
using NachoCore.Index;

namespace NachoClient.iOS
{
    [Foundation.Register ("ChatsViewController")]
    public class ChatsViewController : NachoWrappedTableViewController, NachoSearchControllerDelegate
    {

        #region Properties

        const string ChatCellIdentifier = "ChatCellIdentifier";

        McAccount Account;
        List<McChat> Chats;
        Dictionary<int, int> UnreadCountsByChat;

        SwitchAccountButton SwitchAccountButton;
        UIBarButtonItem NewChatButton;
        UIBarButtonItem SearchButton;
        ChatsSearchResultsViewController SearchResultsViewController;
        NachoSearchController SearchController;
        bool IsListeningForStatusInd;
        bool HasAppearedOnce;

        bool IsReloading;
        bool NeedsReload;

        #endregion

        #region Constructors

        public ChatsViewController () : base(UITableViewStyle.Plain)
        {
            AutomaticallyAdjustsScrollViewInsets = false;
            Account = NcApplication.Instance.Account;
            SearchButton = new UIBarButtonItem (UIBarButtonSystemItem.Search, ShowSearch);
            NewChatButton = new UIBarButtonItem (UIImage.FromBundle ("chat-newmsg"), UIBarButtonItemStyle.Plain, NewChat);
            NavigationItem.LeftBarButtonItem = SearchButton;
            NavigationItem.RightBarButtonItem = NewChatButton;
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "Chats";
            DefinesPresentationContext = true;

            Chats = new List<McChat> ();
            UnreadCountsByChat = new Dictionary<int, int> ();
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof(ChatTableViewCell), ChatCellIdentifier);
            TableView.RowHeight = ChatTableViewCell.HEIGHT;
            TableView.SeparatorInset = new UIEdgeInsets (0.0f, TableView.RowHeight, 0.0f, 0.0f);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            SwitchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = SwitchAccountButton;
            SwitchToAccount (NcApplication.Instance.Account);
            Reload ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (Account.Id != NcApplication.Instance.Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            if (HasAppearedOnce) {
                Reload ();
            }
            StartListeningForStatusInd ();
            SwitchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            HasAppearedOnce = true;
        }

        public override void ViewWillDisappear (bool animated)
        {
            StopListeningForStatusInd ();
            base.ViewWillDisappear (animated);
        }

        public override void Cleanup ()
        {
            // Clean up nav bar
            SearchButton.Clicked -= ShowSearch;
            NewChatButton.Clicked -= NewChat;

            // Clean up search
            if (SearchController != null) {
                SearchController.Delegate = null;
            }
            if (SearchResultsViewController != null) {
                SearchResultsViewController.Cleanup ();
                SearchResultsViewController = null;
            }

            base.Cleanup ();
        }

        #endregion

        #region Table View Delegate & Data Source


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
            cell.Chat = chat;
            int unreadCount = 0;
            UnreadCountsByChat.TryGetValue (chat.Id, out unreadCount);
            cell.ShowUnreadIndicator = unreadCount > 0;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ShowChat (chat);
        }

        #endregion

        #region System Events

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                IsListeningForStatusInd = false;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Account != null) {
                if (NcApplication.Instance.Account.AccountType == McAccount.AccountTypeEnum.Unified || NcApplication.Instance.Account.Id == s.Account.Id) {
                    if (s.Status.SubKind == NcResult.SubKindEnum.Info_ChatSetChanged || s.Status.SubKind == NcResult.SubKindEnum.Info_ChatMessageAdded) {
                        SetNeedsReload ();
                    }
                }
            }
        }

        #endregion

        #region Reloading Data

        protected void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        void Reload ()
        {
            Sync ();
            if (!IsReloading) {
                NcTask.Run (() => {
                    List<McChat> chats;
                    Dictionary<int, int> unreadCounts;
                    if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                        chats = McChat.LastestChats ();
                        unreadCounts = McChat.UnreadCountsByChat ();
                    } else {
                        chats = McChat.LastestChatsForAccount (Account.Id);
                        unreadCounts = McChat.UnreadCountsByChat (Account.Id);
                    }
                    BeginInvokeOnMainThread(() => {
                        HandleReloadResults (chats, unreadCounts);
                        IsReloading = false;
                    });
                }, "ChatsViewController_Reload");
            }
        }

        void HandleReloadResults (List<McChat> chats, Dictionary<int, int> unreadCounts)
        {
            Chats = chats;
            UnreadCountsByChat = unreadCounts;
            TableView.ReloadData ();
        }

        void Sync ()
        {
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                EmailHelper.SyncUnified ();
                EmailHelper.SyncUnifiedSent ();
            } else {
                var inbox = McFolder.GetDefaultInboxFolder (Account.Id);
                if (inbox != null) {
                    BackEnd.Instance.SyncCmd (inbox.AccountId, inbox.Id);
                }
                var sent = McFolder.GetDefaultInboxFolder (Account.Id);
                if (sent != null) {
                    BackEnd.Instance.SyncCmd (sent.AccountId, sent.Id);
                }
            }
        }

        #endregion

        #region User Actions

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void NewChat (object sender, EventArgs args)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = NcApplication.Instance.DefaultEmailAccount;
            NavigationController.PushViewController (messagesViewController, true);
        }

        void ShowSearch (object sender, EventArgs args)
        {
            if (SearchController == null) {
                SearchResultsViewController = new ChatsSearchResultsViewController () { IsLongLived = true };
                SearchController = new NachoSearchController (SearchResultsViewController);
                SearchController.Delegate = this;
            }
            SearchResultsViewController.PrepareForSearching ();
            SearchController.PresentOverViewController (this);
        }

        #endregion

        #region Search

        public void DidChangeSearchText (NachoSearchController searchController, string text)
        {
            SearchResultsViewController.SearchForText (text);
        }

        public void DidSelectSearch (NachoSearchController searchController)
        {
        }

        public void DidEndSearch (NachoSearchController searchController)
        {
            SearchResultsViewController.EndSearching ();
        }

        #endregion

        #region Private Helpers

        void ShowChat (McChat chat)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = McAccount.QueryById<McAccount> (chat.AccountId);
            messagesViewController.Chat = chat;
            NavigationController.PushViewController (messagesViewController, true);
        }

        void SwitchToAccount (McAccount account)
        {
            Account = account;
            Chats.Clear ();
            UnreadCountsByChat.Clear ();
            NewChatButton.Enabled = account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender);
        }

        #endregion

    }

    public class ChatsSearchResultsViewController : NachoTableViewController
    {

        NSObject KeyboardWillShowNotificationToken;
        NSObject KeyboardWillHideNotificationToken;

        ChatSearcher Searcher;

        const string ChatCellIdentifier = "ChatCellIdentifier";
        List<McChat> Chats;

        public ChatsSearchResultsViewController () : base(UITableViewStyle.Plain)
        {
            Searcher = new ChatSearcher (UpdateChats);
            Chats = new List<McChat> ();
        }

        public override void Cleanup ()
        {
            Searcher = null;
            base.Cleanup ();
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof(ChatTableViewCell), ChatCellIdentifier);
            TableView.RowHeight = ChatTableViewCell.HEIGHT;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            TableView.RowHeight = ChatTableViewCell.HEIGHT;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (!NavigationController.NavigationBarHidden) {
                NavigationController.SetNavigationBarHidden (true, true);
            }
            KeyboardWillShowNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, KeyboardWillShow);
            KeyboardWillHideNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, KeyboardWillHide);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (NcKeyboardSpy.Instance.keyboardShowing) {
                AdjustInsetsForKeyboard ();
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillShowNotificationToken);
            NSNotificationCenter.DefaultCenter.RemoveObserver (KeyboardWillHideNotificationToken);
            base.ViewDidDisappear (animated);
        }

        void KeyboardWillShow (NSNotification notification)
        {
            if (IsViewLoaded && View.Window != null) {
                AdjustInsetsForKeyboard ();
            }
        }

        void KeyboardWillHide (NSNotification notification)
        {
            if (IsViewLoaded) {
                AdjustInsetsForKeyboard ();
            }
        }

        void AdjustInsetsForKeyboard ()
        {
            nfloat keyboardHeight = NcKeyboardSpy.Instance.KeyboardHeightInView (View);
            TableView.ContentInset = new UIEdgeInsets (TableView.ContentInset.Top, 0.0f, keyboardHeight, 0.0f);
            TableView.ScrollIndicatorInsets = new UIEdgeInsets (TableView.ScrollIndicatorInsets.Top, TableView.ScrollIndicatorInsets.Left, keyboardHeight, TableView.ScrollIndicatorInsets.Right);
        }

        public void PrepareForSearching ()
        {
            Searcher.Prepare ();
        }

        public void EndSearching ()
        {
            Searcher.End ();
        }

        public void SearchForText (string searchText)
        {
            Searcher.SearchForText (searchText);
        }

        public void UpdateChats (List<McChat> chats)
        {
            Chats = chats;
            TableView.ReloadData ();
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
            cell.Chat = chat;
            cell.ShowUnreadIndicator = false;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var chat = Chats [indexPath.Row];
            ShowChat (chat);
        }

        void ShowChat (McChat chat)
        {
            var messagesViewController = new ChatMessagesViewController ();
            messagesViewController.Account = McAccount.QueryById<McAccount> (chat.AccountId);
            messagesViewController.Chat = chat;
            NavigationController.PushViewController (messagesViewController, true);
            NavigationController.SetNavigationBarHidden (false, true);
        }

    }

    public class ChatSearcher : NSObject
    {

        NcIndex SearchIndex;
        McAccount Account;
        Action<List<McChat>> ResultsCallback;

        Dictionary<int, McChat> ChatsByMessageId;

        string SearchText;
        object SearchLockObject = new object();
        bool IsSearching;
        bool IsSearchCanceled;

        public ChatSearcher (Action<List<McChat>> resultsCallback)
        {
            ResultsCallback = resultsCallback;
        }

        public void Prepare ()
        {
            Account = NcApplication.Instance.Account;
            SearchIndex = new NcIndex (NcModel.Instance.GetIndexPath (Account.Id));
            ChatsByMessageId = null;
            IsSearchCanceled = false;
        }

        void PopulateChatsCache ()
        {
            List<McChat> allChats;
            if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                allChats = McChat.LastestChats ();
            } else {
                allChats = McChat.LastestChatsForAccount (Account.Id);
            }

            var chatsById = new Dictionary<int, McChat> ();
            foreach (var chat in allChats) {
                chatsById.Add (chat.Id, chat);
            }
            ChatsByMessageId = new Dictionary<int, McChat> ();
            foreach (var chatMessage in NcModel.Instance.Db.Table<McChatMessage>()) {
                McChat chat = null;
                chatsById.TryGetValue (chatMessage.ChatId, out chat);
                ChatsByMessageId.Add (chatMessage.MessageId, chat);
            }
        }

        public void End ()
        {
            lock (SearchLockObject) {
                IsSearchCanceled = true;
            }
            if (ChatsByMessageId != null) {
                ChatsByMessageId.Clear ();
            }
        }

        public void SearchForText (string text)
        {
            lock (SearchLockObject) {
                SearchText = text;
                if (!IsSearching) {
                    IsSearching = true;
                    NcTask.Run (SearchTask, "ChatSearcher_Search");
                }
            }
        }

        void StartSearch ()
        {
        }

        void SearchTask ()
        {
            bool shouldSearchAgain = false;
            string searchText;

            if (ChatsByMessageId == null) {
                PopulateChatsCache ();
            }

            do {
                lock (SearchLockObject) {
                    searchText = SearchText;
                }

                var foundChatsById = new Dictionary<int, McChat> ();
                var chats = new List<McChat> ();
                if (!String.IsNullOrEmpty (searchText)) {
                    var results = SearchIndex.SearchAllEmailMessageFields (searchText);
                    foreach (var result in results) {
                        McChat chat = null;
                        ChatsByMessageId.TryGetValue (int.Parse (result.Id), out chat);
                        if (chat != null && !foundChatsById.ContainsKey(chat.Id)) {
                            chats.Add (chat);
                            foundChatsById.Add (chat.Id, chat);
                        }
                    }
                }

                if (!IsSearchCanceled){
                    BeginInvokeOnMainThread(() => {
                        ResultsCallback (chats);
                    });
                }

                lock (SearchLockObject) {
                    shouldSearchAgain = !IsSearchCanceled && SearchText != searchText;
                    IsSearching = shouldSearchAgain;
                }
            } while (shouldSearchAgain);

        }

    }

    public class ChatTableViewCell : SwipeTableViewCell
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

        public ChatTableViewCell (IntPtr handle) : base (handle)
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

