// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public partial class NachoNowViewController : NachoWrappedTableViewController, SwipeActionsViewDelegate, MessagesSyncManagerDelegate
    {
        #region Constants

        const string MessageCellIdentifier = "MessageCellIdentifier";
        const string ActionCellIdentifier = "ActionCellIdentifier";
        public const string HotMessageRefreshTaskName = "NachoNowViewController_RefreshHotMessages";

        #endregion

        #region Properties

        UIBarButtonItem NewMessageItem;
        UIBarButtonItem NewMeetingItem;

        McAccount Account;
        SwitchAccountButton SwitchAccountButton;

        HotEventView HotEventView;
        McEvent HotEvent;
        NcTimer CalendarUpdateTimer;

        NachoEmailMessages HotMessages;
        MessagesSyncManager SyncManager;

        EmptyHotView EmptyView;

        bool IsListeningForStatusInd;
        bool HasAppearedOnce = false;
        bool HasLoadedOnce = false;

        int NumberOfMessagePreviewLines = 2;
        int SectionCount = 0;
        int HotMessagesSection;
        int MaximumNumberOfHotMessages = 4;
        int HotSectionRows;

        #endregion

        #region Constructors

        public NachoNowViewController () : base (UITableViewStyle.Grouped)
        {
            SyncManager = new MessagesSyncManager ();
            SyncManager.Delegate = this;

            AutomaticallyAdjustsScrollViewInsets = false;

            using (var image = UIImage.FromBundle ("contact-newemail")) {
                NewMessageItem = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, NewEmailMessage);
                NewMessageItem.AccessibilityLabel = "New message";
            }
            using (var image = UIImage.FromBundle ("cal-add")) {
                NewMeetingItem = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, NewMeeting);
                NewMeetingItem.AccessibilityLabel = "New meeting";
            }
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { NewMessageItem, NewMeetingItem };

            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";

            HotMessages = NcEmailManager.PriorityInbox (NcApplication.Instance.Account.Id);
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.RegisterClassForCellReuse (typeof(MessageCell), MessageCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof(ActionCell), ActionCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            SwitchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            Account = NcApplication.Instance.Account;
            SwitchAccountButton.SetAccountImage (Account);
            NavigationItem.TitleView = SwitchAccountButton;

            HotEventView = new HotEventView (new CGRect (0, 0, View.Frame.Width, HotEventView.PreferredHeight));
            HotEventView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            HotEventView.Action = ShowHotEvent;
            HotEventView.SwipeView.Delegate = this;
            View.AddSubview (HotEventView);

            TableView.Frame = new CGRect (0.0f, HotEventView.Frame.Height, View.Bounds.Width, View.Bounds.Height - HotEventView.Frame.Height);
            TableView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;

            // Have the event manager keep the McEvents accurate for at least the next seven days.
            NcEventManager.AddEventWindow (this, new TimeSpan (7, 0, 0, 0));

            EmptyView = new EmptyHotView (TableView.Frame);
            EmptyView.TintColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.5f);
            EmptyView.ImageView.TintColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.25f);
            EmptyView.AutoresizingMask = TableView.AutoresizingMask;
            EmptyView.Hidden = true;
            View.AddSubview (EmptyView);

            ReloadHotMessages ();
            ReloadCalendar ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (RefreshControl == null) {
                EnableRefreshControl ();
            }
            if (NcApplication.Instance.Account.Id != Account.Id) {
                SwitchToAccount (NcApplication.Instance.Account);
            }
            if (SyncManager.IsSyncing) {
                SyncManager.ResumeEvents ();
            }
            StartListeningForStatusInd ();
            HotMessages.RefetchSyncTime ();
            if (HasAppearedOnce) {
                ReloadCalendar ();
                ReloadHotMessages ();
            }
            HasAppearedOnce = true;
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            PermissionManager.DealWithNotificationPermission ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
        }

        public override void ViewDidDisappear (bool animated)
        {
            SyncManager.PauseEvents ();
            StopListeningForStatusInd ();
            HotEventView.CancelAutomaticDateUpdate ();
            if (CalendarUpdateTimer != null) {
                CalendarUpdateTimer.Dispose ();
                CalendarUpdateTimer = null;
            }
            base.ViewDidDisappear (animated);
        }

        #endregion

        #region User Actions

        protected override void HandleRefreshControlEvent (object sender, EventArgs e)
        {
            RefreshIndicator.StartAnimating ();
            StartSync ();
        }

        void NewEmailMessage (object sender, EventArgs e)
        {
            ComposeMessage ();
        }

        void NewMeeting (object sender, EventArgs e)
        {
            EditEvent (null);
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void ShowHotEvent ()
        {
            ShowEvent (HotEvent);
        }

        void MarkMessageAsRead (NSIndexPath indexPath)
        {
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            if (message != null) {
                EmailHelper.MarkAsRead (message, true);
                message.IsRead = true;
                var cell = TableView.CellAt (indexPath) as MessageCell;
                if (cell != null) {
                    cell.SetMessage (message);
                }
            }
        }

        void MarkMessageAsUnread (NSIndexPath indexPath)
        {
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            if (message != null) {
                EmailHelper.MarkAsUnread (message, true);
                message.IsRead = false;
                var cell = TableView.CellAt (indexPath) as MessageCell;
                if (cell != null) {
                    cell.SetMessage (message);
                }
            }
        }

        void MarkMessageAsHot (NSIndexPath indexPath)
        {
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            if (message != null) {
                message.UserAction = NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                var cell = TableView.CellAt (indexPath) as MessageCell;
                if (cell != null) {
                    cell.SetMessage (message);
                }
            }
        }

        void MarkMessageAsUnhot (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            if (message != null) {
                message.UserAction = NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
                var cell = TableView.CellAt (indexPath) as MessageCell;
                if (cell != null) {
                    cell.SetMessage (message);
                }
            }
        }

        void DeleteMessage (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            var thread = HotMessages.GetEmailThread (indexPath.Row);
            if (message != null) {
                NcAssert.NotNull (thread);
                NcEmailArchiver.Delete (thread);
            }
        }

        void ArchiveMessage (NSIndexPath indexPath)
        {
            DidEndSwiping (TableView, indexPath);
            var message = HotMessages.GetCachedMessage (indexPath.Row);
            var thread = HotMessages.GetEmailThread (indexPath.Row);
            if (message != null) {
                NcAssert.NotNull (thread);
                NcEmailArchiver.Archive (thread);
            }
        }

        #endregion

        #region Calendar Event

        void ReloadCalendar ()
        {
            DateTime nextUpdateTime;
            HotEvent = CalendarHelper.CurrentOrNextEvent (out nextUpdateTime);
            if (null != HotEvent && !HotEvent.IsValid ()) {
                HotEvent = null;
            }
            HotEventView.Event = HotEvent;

            // set timer to update when the next event will happen
            var timeUntilNextUpdate = nextUpdateTime - DateTime.UtcNow;
            if (timeUntilNextUpdate < TimeSpan.Zero) {
                timeUntilNextUpdate = TimeSpan.Zero;
            }
            if (CalendarUpdateTimer != null) {
                CalendarUpdateTimer.Dispose ();
            }
            CalendarUpdateTimer = new NcTimer ("NachoNow_UpdateHotEventView", CalendarUpdateTimerFired, null, timeUntilNextUpdate, TimeSpan.Zero);
        }

        void CalendarUpdateTimerFired (object state)
        {
            CalendarUpdateTimer = null;
            BeginInvokeOnMainThread (ReloadCalendar);
        }

        public List<SwipeAction> ActionsForViewSwipingRight (SwipeActionsView view)
        {
            if (view == HotEventView.SwipeView) {
                if (HotEvent != null && !String.IsNullOrEmpty (HotEvent.OrganizerEmail)) {
                    return new List<SwipeAction> (new SwipeAction[] {
                        new BasicSwipeAction("I'm late", UIImage.FromBundle(A.File_NachoSwipeLate), A.Color_NachoSwipeLate, SendImLateMessage) 
                    });
                }
            }
            return null;
        }

        public List<SwipeAction> ActionsForViewSwipingLeft (SwipeActionsView view)
        {
            if (view == HotEventView.SwipeView) {
                if (HotEvent != null && !String.IsNullOrEmpty (HotEvent.OrganizerEmail)) {
                    return new List<SwipeAction> (new SwipeAction[] {
                        new BasicSwipeAction("Forward", UIImage.FromBundle(A.File_NachoSwipeForward), A.Color_NachoeSwipeForward, ForwardHotEvent) 
                    });
                }
            }
            return null;
        }

        public void SwipeViewWillBeginShowingActions (SwipeActionsView view)
        {
        }

        public void SwipeViewDidEndShowingActions (SwipeActionsView view)
        {
        }

        public void SwipeViewDidSelectAction (SwipeActionsView view, SwipeAction action)
        {
            if (view == HotEventView.SwipeView) {
                (action as BasicSwipeAction).Action ();
            }
        }

        #endregion

        #region Dealing with Notifications
       
        // Called from NachoTabBarController
        // if we need to handle a notification.
        public void HandleNotifications ()
        {
            NavigationController.PopToViewController (this, false);
            // If we have a pending notification, bring up the event detail view
            var eventNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EventNotificationKey);
            var eventNotification = eventNotifications.FirstOrDefault ();
            if (null != eventNotification) {
                var eventId = int.Parse (eventNotification.Value);
                var e = McEvent.QueryById<McEvent> (eventId);
                eventNotification.Delete ();
                if (null != e) {
                    if (MaybeSwitchToNotificationAccount (e)) {
                        ShowEvent (e);
                    }
                }
            }
            var emailNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.EmailNotificationKey);
            var emailNotification = emailNotifications.FirstOrDefault ();
            if (null != emailNotification) {
                var messageId = int.Parse (emailNotification.Value);
                var m = McEmailMessage.QueryById<McEmailMessage> (messageId);
                emailNotification.Delete ();
                if (null != m) {
                    if (MaybeSwitchToNotificationAccount (m)) {
                        ShowMessage (m);
                    }
                }
                return;
            }
            var chatNotifications = McMutables.Get (McAccount.GetDeviceAccount ().Id, NachoClient.iOS.AppDelegate.ChatNotificationKey);
            var chatNotification = chatNotifications.FirstOrDefault ();
            if (null != chatNotification) {
                var parts = chatNotification.Value.Split (',');
                var chatId = int.Parse (parts [0]);
                var messageId = int.Parse (parts [1]);
                var chat = McChat.QueryById<McChat> (chatId);
                var message = McChatMessage.EmailMessageInChat (chatId, messageId);
                chatNotification.Delete ();
                if (null != chat && null != message) {
                    if (MaybeSwitchToNotificationAccount (message)) {
                        var chatViewController = new ChatMessagesViewController ();
                        chatViewController.Chat = chat;
                        chatViewController.Account = McAccount.QueryById<McAccount> (chat.AccountId);
                        NavigationController.PushViewController (chatViewController, true);
                    }
                }
                return;
            }
        }

        bool MaybeSwitchToNotificationAccount (McAbstrObjectPerAcc obj)
        {
            var notificationAccount = McAccount.QueryById<McAccount> (obj.AccountId);
            if (null == notificationAccount) {
                Log.Error (Log.LOG_UI, "MaybeSwitchToNotificationAccount: no account for {0}", obj.Id);
                return false;
            }
            if (NcApplication.Instance.Account.ContainsAccount (notificationAccount.Id)) {
                return true;
            }
            NcApplication.Instance.Account = notificationAccount;
            SwitchToAccount (notificationAccount);
            return true;
        }

        #endregion

        #region Reload Data

        void ReloadHotMessages ()
        {
            HotMessages.ClearCache ();
            if (HotMessages.HasBackgroundRefresh ()) {
                HotMessages.BackgroundRefresh (HandleReloadHotMessagesResults);
            } else {
                NcTask.Run (() => {
                    List<int> adds;
                    List<int> deletes;
                    bool changed = HotMessages.Refresh (out adds, out deletes);
                    BeginInvokeOnMainThread(() => {
                        HandleReloadHotMessagesResults (changed, adds, deletes);
                    });
                }, HotMessageRefreshTaskName);
            }
        }

        void HandleReloadHotMessagesResults (bool changed, List<int> adds, List<int> deletes)
        {
            if (IsShowingRefreshIndicator && !SyncManager.IsSyncing) {
                EndRefreshing ();
            }
            SectionCount = 0;
            if (HotMessages.Count () > 0) {
                SectionCount = 1;
                HotMessagesSection = 0;
            }
            if (!HasLoadedOnce) {
                TableView.ReloadData ();
                HotSectionRows = (int)RowsInSection (TableView, HotMessagesSection);
                HasLoadedOnce = true;
            }else{
                if (changed) {
                    int rowsBeforeUpdate = HotSectionRows;
                    int messageRowsBeforeUpate = Math.Min (rowsBeforeUpdate, MaximumNumberOfHotMessages);
                    HotSectionRows = (int)RowsInSection (TableView, HotMessagesSection);
                    int messageRows = Math.Min (HotSectionRows, MaximumNumberOfHotMessages);

                    var addedIndexPaths = new List<NSIndexPath> ();
                    var deletedIndexPaths = new List<NSIndexPath> ();

                    // Figure out how many of the adds will actually be added to our limited table
                    foreach (var index in adds){
                        if (index < MaximumNumberOfHotMessages) {
                            addedIndexPaths.Add (NSIndexPath.FromRowSection (index, HotMessagesSection));
                        }
                    }

                    // If the newly added rows put us over the row limit, remove rows from the end as necessary
                    int messageRowsAfterUpdate = messageRowsBeforeUpate + addedIndexPaths.Count;
                    int deleteIndex = messageRowsBeforeUpate - 1;

                    while (messageRowsAfterUpdate > messageRows) {
                        deletedIndexPaths.Add (NSIndexPath.FromRowSection (deleteIndex, HotMessagesSection));
                        --deleteIndex;
                        --messageRowsAfterUpdate;
                    }

                    // If any of the deletes are from the rows not yet deleted, remove them
                    foreach (var index in deletes){
                        if (index <= deleteIndex){
                            deletedIndexPaths.Add (NSIndexPath.FromRowSection (index, HotMessagesSection));
                            --messageRowsAfterUpdate;
                        }
                    }

                    var insertIndex = messageRowsAfterUpdate;

                    // If the deletes left us short of the new count, add rows to the end
                    while (messageRowsAfterUpdate < messageRows) {
                        addedIndexPaths.Add (NSIndexPath.FromRowSection (insertIndex, HotMessagesSection));
                        ++messageRowsAfterUpdate;
                        ++insertIndex;
                    }

                    // Finally, add or remove the action row if it has changed
                    if (rowsBeforeUpdate > MaximumNumberOfHotMessages && HotSectionRows <= MaximumNumberOfHotMessages) {
                        deletedIndexPaths.Add (NSIndexPath.FromRowSection (MaximumNumberOfHotMessages, HotMessagesSection));
                    } else if (rowsBeforeUpdate <= MaximumNumberOfHotMessages && HotSectionRows > MaximumNumberOfHotMessages) {
                        addedIndexPaths.Add (NSIndexPath.FromRowSection (MaximumNumberOfHotMessages, HotMessagesSection));
                    }

                    if (addedIndexPaths.Count > 0 || deletedIndexPaths.Count > 0) {
                        TableView.BeginUpdates ();
                        TableView.DeleteRows (deletedIndexPaths.ToArray(), UITableViewRowAnimation.Fade);
                        TableView.InsertRows (addedIndexPaths.ToArray(), UITableViewRowAnimation.Top);
                        TableView.EndUpdates ();
                    }
                }
                UpdateVisibleRows ();
            }
            EmptyView.Hidden = HotMessages.Count () > 0;
        }

        void UpdateVisibleRows ()
        {
            var indexPaths = TableView.IndexPathsForVisibleRows;
            if (indexPaths != null) {
                foreach (var indexPath in indexPaths) {
                    if (indexPath.Section == HotMessagesSection) {
                        if (indexPath.Row < MaximumNumberOfHotMessages) {
                            var message = HotMessages.GetCachedMessage (indexPath.Row);
                            var cell = TableView.CellAt (indexPath) as MessageCell;
                            if (cell != null && message != null) {
                                cell.SetMessage (message);
                            }
                        } else {
                            TableView.ReloadRows (new NSIndexPath[] { indexPath }, UITableViewRowAnimation.None);
                        }
                    }
                    // Needed to tell our custom table group cells to redraw corners
                    WillDisplay (TableView, TableView.CellAt (indexPath), indexPath);
                }
            }
        }

        #endregion

        #region Table Delegate & Data Source

        private InsetLabelView _HotMessagesHeader;
        private InsetLabelView HotMessagesHeader {
            get {
                if (_HotMessagesHeader == null) {
                    _HotMessagesHeader = new InsetLabelView ();
                    _HotMessagesHeader.LabelInsets = new UIEdgeInsets (20.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _HotMessagesHeader.Label.Text = "Hot Messages";
                    _HotMessagesHeader.Label.Font = A.Font_AvenirNextRegular14;
                    _HotMessagesHeader.Label.TextColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.6f);
                    _HotMessagesHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                }
                return _HotMessagesHeader;
            }
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return SectionCount;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == HotMessagesSection) {
                var messageCount = HotMessages.Count ();
                if (messageCount > MaximumNumberOfHotMessages) {
                    return MaximumNumberOfHotMessages + 1;
                }
                return messageCount;
            }
            return 0;
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            if (section == HotMessagesSection) {
                return HotMessagesHeader.PreferredHeight;
            }
            return 0.0f;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (section == HotMessagesSection) {
                return HotMessagesHeader;
            }
            return null;
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == HotMessagesSection) {
                if (indexPath.Row < MaximumNumberOfHotMessages) {
                    return MessageCell.PreferredHeight (NumberOfMessagePreviewLines, A.Font_AvenirNextMedium17, A.Font_AvenirNextMedium14);
                } else {
                    return ActionCell.PreferredHeight;
                }
            }
            return 44.0f;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == HotMessagesSection) {
                if (indexPath.Row < MaximumNumberOfHotMessages) {
                    var cell = tableView.DequeueReusableCell (MessageCellIdentifier) as MessageCell;
                    var message = HotMessages.GetCachedMessage (indexPath.Row);
                    cell.NumberOfPreviewLines = NumberOfMessagePreviewLines;
                    cell.SetMessage (message);
                    return cell;
                } else {
                    var cell = tableView.DequeueReusableCell (ActionCellIdentifier) as ActionCell;
                    cell.TextLabel.Text = String.Format ("See all {0} hot messages", HotMessages.Count());
                    if (!(cell.AccessoryView is DisclosureAccessoryView)) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                }
            }
            return null;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == HotMessagesSection) {
                if (indexPath.Row < MaximumNumberOfHotMessages) {
                    var message = HotMessages.GetCachedMessage (indexPath.Row);
                    ShowMessage (message);
                } else {
                    ShowAllHotMessages ();
                }
            }
        }

        public override List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == HotMessagesSection) {
                if (indexPath.Row < MaximumNumberOfHotMessages) {
                    var message = HotMessages.GetCachedMessage (indexPath.Row);
                    var actions = new List<SwipeTableRowAction> ();
                    if (message.IsRead) {
                        actions.Add (new SwipeTableRowAction ("Unread", UIImage.FromBundle ("gen-unread-msgs"), UIColor.FromRGB (0x00, 0xC8, 0x9D), MarkMessageAsUnread));
                    } else {
                        actions.Add (new SwipeTableRowAction ("Read", UIImage.FromBundle ("gen-unread-msgs"), UIColor.FromRGB (0x00, 0xC8, 0x9D), MarkMessageAsRead));
                    }
                    if (message.isHot ()) {
                        actions.Add (new SwipeTableRowAction ("Not Hot", UIImage.FromBundle ("email-not-hot"), UIColor.FromRGB (0xE6, 0x59, 0x59), MarkMessageAsUnhot));
                    } else {
                        actions.Add (new SwipeTableRowAction ("Hot", UIImage.FromBundle ("email-hot"), UIColor.FromRGB (0xE6, 0x59, 0x59), MarkMessageAsHot));
                    }
                    return actions;
                }
            }
            return null;
        }

        public override List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == HotMessagesSection) {
                if (indexPath.Row < MaximumNumberOfHotMessages) {
                    var actions = new List<SwipeTableRowAction> ();
                    actions.Add (new SwipeTableRowAction ("Delete", UIImage.FromBundle ("email-delete-swipe"), UIColor.FromRGB (0xd2, 0x47, 0x47), DeleteMessage));
                    actions.Add (new SwipeTableRowAction ("Archive", UIImage.FromBundle ("email-archive-swipe"), UIColor.FromRGB (0x01, 0xb2, 0xcd), ArchiveMessage));
                    return actions;
                }
            }
            return null;
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


            switch (s.Status.SubKind){
            case NcResult.SubKindEnum.Info_EventSetChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                ReloadCalendar ();
                break;
            case NcResult.SubKindEnum.Info_ExecutionContextChanged:
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    ReloadCalendar ();
                }
                break;
            }

            if (s.AppliesToAccount (Account)) {
                switch (s.Status.SubKind) {
                case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
                case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
                case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
                case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
                case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                    ReloadHotMessages ();
                    break;
                case NcResult.SubKindEnum.Error_SyncFailed:
                case NcResult.SubKindEnum.Info_SyncSucceeded:
                    HotMessages.RefetchSyncTime ();
                    break;
                }
            }
        }

        #endregion

        #region Private Helpers

        void StartSync ()
        {
            if (!SyncManager.SyncEmailMessages (HotMessages)) {
                ReloadHotMessages ();
            }
        }

        protected void CancelSyncing ()
        {
            SyncManager.Cancel ();
            EndRefreshing ();
        }

        public void MessagesSyncDidComplete (MessagesSyncManager manager)
        {
            EndRefreshing ();
        }

        public void MessagesSyncDidTimeOut (MessagesSyncManager manager)
        {
            EndRefreshing ();
        }

        void ShowAllHotMessages ()
        {
            var viewController = new MessageListViewController ();
            var messages = NcEmailManager.PriorityInbox (NcApplication.Instance.Account.Id);
            viewController.SetEmailMessages (messages);
            NavigationController.PushViewController (viewController, true);
        }

        void SendImLateMessage ()
        {
            var calendarInvite = CalendarHelper.GetMcCalendarRootForEvent (HotEvent.Id);
            if (null != calendarInvite) {
                if (!String.IsNullOrEmpty (calendarInvite.OrganizerEmail)) {
                    var account = McAccount.EmailAccountForCalendar (calendarInvite);
                    var message = McEmailMessage.MessageWithSubject (account, calendarInvite.Subject);
                    message.To = calendarInvite.OrganizerEmail;
                    var composeViewController = new MessageComposeViewController (account);
                    composeViewController.Composer.Message = message;
                    composeViewController.Composer.InitialText = "Running late";
                    composeViewController.Present ();
                }
            }
        }

        void ForwardHotEvent ()
        {
            var calendarInvite = CalendarHelper.GetMcCalendarRootForEvent (HotEvent.Id);
            if (null != calendarInvite) {
                var account = McAccount.EmailAccountForCalendar (calendarInvite);
                var composeViewController = new MessageComposeViewController (account);
                composeViewController.Composer.RelatedCalendarItem = calendarInvite;
                composeViewController.Composer.Message = McEmailMessage.MessageWithSubject (account, "Fwd: " + calendarInvite.Subject);
                composeViewController.Present ();

            }
        }

        private void ComposeMessage ()
        {
            var composeViewController = new MessageComposeViewController (NcApplication.Instance.DefaultEmailAccount);
            composeViewController.Present ();
        }

        void EditEvent (McCalendar calendarEvent)
        {
            var vc = new EditEventViewController ();
            vc.SetCalendarItem (calendarEvent);
            var navigationController = new UINavigationController (vc);
            Util.ConfigureNavBar (false, navigationController);
            PresentViewController (navigationController, true, null);
        }

        void ShowMessage (McEmailMessage message)
        {
            var messageViewController = new MessageViewController ();
            messageViewController.Message = message;
            NavigationController.PushViewController (messageViewController, true);
        }

        void ShowEvent (McEvent calendarEvent)
        {
            var vc = new EventViewController ();
            vc.SetCalendarItem (calendarEvent);
            NavigationController.PushViewController (vc, true);
        }

        void SwitchToAccount (McAccount account)
        {
            if (SwipingIndexPath != null) {
                EndSwiping ();
            }
            CancelSyncing ();
            Account = account;
            SwitchAccountButton.SetAccountImage (account);
            HotMessages = NcEmailManager.PriorityInbox (NcApplication.Instance.Account.Id);
            TableView.ReloadData (); // to clear table so we don't show stale data from other account
            HasLoadedOnce = false;
            // Relying on ViewWillAppear to do any reloading
        }

        private void ComposeResponse (McEmailMessageThread thread, EmailHelper.Action action)
        {
            var message = thread.FirstMessageSpecialCase ();
            var account = McAccount.EmailAccountForMessage (message);
            var composeViewController = new MessageComposeViewController (account);
            composeViewController.Composer.Kind = action;
            composeViewController.Composer.RelatedThread = thread;
            composeViewController.Present ();
        }

        protected override void PrepareRefreshIndicator ()
        {
            UpdateLastSyncLabel ();
        }

        void UpdateLastSyncLabel ()
        {
            if (RefreshControl != null) {
                DateTime? lastSyncDate = null;
                if (HotMessages != null) {
                    lastSyncDate = HotMessages.LastSuccessfulSyncTime ();
                }
                if (lastSyncDate.HasValue) {
                    var diff = DateTime.UtcNow - lastSyncDate.Value;
                    if (diff.TotalSeconds < 60) {
                        RefreshLabel.Text = "Last updated just now";
                    } else {
                        RefreshLabel.Text = "Last updated " + Pretty.TimeWithDecreasingPrecision (lastSyncDate.Value);
                    }
                } else {
                    RefreshLabel.Text = "";
                }
            }
        }

        #endregion

        #region Private Classes

        private class DisclosureAccessoryView : ImageAccessoryView
        {
            public DisclosureAccessoryView () : base ("gen-more-arrow")
            {
            }
        }

        private class ActionCell : SwipeTableViewCell
        {

            public static nfloat PreferredHeight = 44.0f;

            public ActionCell (IntPtr handle) : base (handle)
            {
                TextLabel.Font = A.Font_AvenirNextRegular14;
                TextLabel.TextColor = A.Color_NachoGreen;
            }
        }

        private class EmptyHotView : UIView 
        {

            public readonly UILabel TextLabel;
            public readonly UIImageView ImageView;
            nfloat Padding = 30.0f;
            nfloat ImageSpacing = 30.0f;

            public EmptyHotView (CGRect frame) : base (frame)
            {
                UserInteractionEnabled = false;
                TextLabel = new UILabel ();
                TextLabel.UserInteractionEnabled = false;
                TextLabel.Lines = 0;
                TextLabel.Font = A.Font_AvenirNextRegular14;
                TextLabel.LineBreakMode = UILineBreakMode.WordWrap;
                TextLabel.TextAlignment = UITextAlignment.Center;
                TextLabel.Text = "Your most important items will show up here automatically as Nacho Mail identifies them.\n\nAdditionally, you can always add any item of your choice by marking it as hot.";

                using (var image = UIImage.FromBundle("empty-hot")){
                    ImageView = new UIImageView (image.ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate));
                }

                AddSubview(ImageView);
                AddSubview(TextLabel);
            }

            public override void TintColorDidChange ()
            {
                base.TintColorDidChange ();
                TextLabel.TextColor = TintColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var size = TextLabel.SizeThatFits (new CGSize (Bounds.Width - 2.0f * Padding, 0.0f));
                size.Width = (nfloat)Math.Ceiling (size.Width);
                size.Height = (nfloat)Math.Ceiling (size.Height);
                TextLabel.Frame = new CGRect ((Bounds.Width - size.Width) / 2.0f, (Bounds.Height - size.Height) / 2.0f, size.Width, size.Height);
                ImageView.Center = new CGPoint (Bounds.Width / 2.0f, TextLabel.Frame.Top - ImageSpacing - ImageView.Frame.Size.Height / 2.0f);
            }
        }

        #endregion
    }
        
}
