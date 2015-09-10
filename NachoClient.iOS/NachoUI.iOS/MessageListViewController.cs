// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;

//using MonoTouch.CoreGraphics;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoCore.Brain;
using NachoPlatform;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, IMessageTableViewSourceDelegate, INachoDateControllerParent
    {
        IMessageTableViewSource messageSource;
        IMessageTableViewSource searchResultsSource;
        NachoMessageSearchResults searchResultsMessages;
        protected UIBarButtonItem composeMailButton;
        protected UIBarButtonItem multiSelectButton;
        protected UIBarButtonItem cancelSelectedButton;
        protected UIBarButtonItem archiveButton;
        protected UIBarButtonItem deleteButton;
        protected UIBarButtonItem searchButton;
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem backButton;

        protected string searchToken;
        protected UISearchBar searchBar;
        protected UISearchDisplayController searchDisplayController;

        SwitchAccountButton switchAccountButton;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected bool threadsNeedsRefresh;
        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        protected SearchHelper searcher;

        bool StatusIndCallbackIsSet = false;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads, "No messages");
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            messageSource = new MessageTableViewSource (this);
            searcher = new SearchHelper ("MessageListViewController", (searchString) => {
                if (String.IsNullOrEmpty (searchString)) {
                    searchResultsMessages.UpdateMatches (null);
                    searchResultsMessages.UpdateServerMatches (null);
                    return; 
                }
                // On-device index
                int curVersion = searcher.Version;
                var indexPath = NcModel.Instance.GetIndexPath (NcApplication.Instance.Account.Id);
                var index = new NachoCore.Index.NcIndex (indexPath);
                int maxResults = 1000;
                if (String.IsNullOrEmpty (searchString) || (4 > searchString.Length)) {
                    maxResults = 20;
                }
                var matches = index.SearchAllEmailMessageFields (searchString, maxResults);

                // Cull low scores
                var maxScore = 0f;
                foreach (var m in matches) {
                    maxScore = Math.Max (maxScore, m.Score);
                }
                matches.RemoveAll (x => x.Score < (maxScore / 2));

                if (curVersion == searcher.Version) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResultsMessages.UpdateMatches (matches);
                        List<int> adds;
                        List<int> deletes;
                        searchResultsSource.RefreshEmailMessages (out adds, out deletes);
                        if (null != searchDisplayController.SearchResultsTableView) {
                            searchDisplayController.SearchResultsTableView.ReloadData ();
                        }
                    });
                }
            });
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NavigationController.NavigationBar.Translucent = false;

            if (HasAccountSwitcher ()) {
                switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
                NavigationItem.TitleView = switchAccountButton;
                switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
            }

            TableView.AccessibilityLabel = "Message list";

            ReloadCaptureName = "MessageListViewController.Reload";
            NcCapture.AddKind (ReloadCaptureName);
            ReloadCapture = NcCapture.Create (ReloadCaptureName);

            composeMailButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (composeMailButton, "contact-newemail");
            composeMailButton.AccessibilityLabel = "New message";
            composeMailButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageListToCompose", this);
            };

            multiSelectButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (multiSelectButton, "folder-edit");
            multiSelectButton.AccessibilityLabel = "Folder edit";
            multiSelectButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectEnable (TableView);
            };

            cancelSelectedButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelSelectedButton, "gen-close");
            cancelSelectedButton.AccessibilityLabel = "Close";
            cancelSelectedButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectCancel (TableView);
            };

            archiveButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (archiveButton, "gen-archive");
            archiveButton.AccessibilityLabel = "Archive";
            archiveButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectArchive (TableView);
            };

            deleteButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (deleteButton, "gen-delete-all");
            deleteButton.AccessibilityLabel = "Delete";
            deleteButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectDelete (TableView);
            };

            moveButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (moveButton, "folder-move");
            moveButton.AccessibilityLabel = "Move";
            moveButton.Clicked += (object sender, EventArgs e) => {
                var h = new SegueHolder (TableView);
                PerformSegue ("MessageListToFolders", h);
            };

            searchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.AccessibilityLabel = "Search";
            searchButton.Clicked += onClickSearchButton;

            TableView.SeparatorColor = A.Color_NachoBackgroundGray;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            TableView.Source = messageSource.GetTableViewSource ();

            CustomizeBackButton ();
            MultiSelectToggle (messageSource, false);

            TableView.TableHeaderView = null; // beta 1

            RefreshControl = new UIRefreshControl ();
            RefreshControl.Hidden = true;
            RefreshControl.TintColor = A.Color_NachoGreen;
            RefreshControl.AttributedTitle = new NSAttributedString ("Refreshing...");
            RefreshControl.ValueChanged += (object sender, EventArgs e) => {
                var nr = messageSource.GetNachoEmailMessages ().StartSync ();
                rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
                RefreshControl.BeginRefreshing ();
            };

            searchBar = new UISearchBar ();
            searchBar.Delegate = this;
            searchDisplayController = new UISearchDisplayController (searchBar, this);
            searchResultsMessages = new NachoMessageSearchResults (NcApplication.Instance.Account.Id);
            searchResultsSource = new MessageTableViewSource (this);
            searchResultsSource.SetEmailMessages (searchResultsMessages, "");
            searchDisplayController.SearchResultsSource = searchResultsSource.GetTableViewSource ();
            searchDisplayController.SearchResultsTableView.RowHeight = 126;
            searchDisplayController.SearchResultsTableView.SeparatorColor = A.Color_NachoBackgroundGray;
            searchDisplayController.SearchResultsTableView.BackgroundColor = A.Color_NachoBackgroundGray;

            View.AddSubview (searchBar);

            Util.ConfigureNavBar (false, this.NavigationController);

            SetRowHeight ();

            StatusIndCallbackIsSet = true;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            // Load when view becomes visible
            threadsNeedsRefresh = true;
        }

        protected virtual void SetRowHeight ()
        {
            TableView.RowHeight = MessageTableViewConstants.NORMAL_ROW_HEIGHT;
            searchDisplayController.SearchResultsTableView.RowHeight = MessageTableViewConstants.NORMAL_ROW_HEIGHT;
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (RefreshControl.Refreshing) {
                    RefreshControl.EndRefreshing ();
                }
            });
        }

        NcTimer refreshTimer;

        void rearmRefreshTimer (int seconds)
        {
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
            refreshTimer = new NcTimer ("MessageListViewController refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (RefreshControl.Refreshing) {
                RefreshControl.EndRefreshing ();
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        protected virtual void CustomizeBackButton ()
        {
        }

        public void MultiSelectToggle (IMessageTableViewSource source, bool enabled)
        {
            if (enabled) {
                var msg = messageSource.GetNachoEmailMessages ();
                if (msg.HasOutboxSemantics () || msg.HasDraftsSemantics ()) {
                    NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                        deleteButton,
                    };
                } else {
                    NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                        deleteButton,
                        moveButton,
                        archiveButton,
                    };
                }
                NavigationItem.HidesBackButton = true;
                NavigationItem.SetLeftBarButtonItem (cancelSelectedButton, false);
            } else {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    composeMailButton,
                    multiSelectButton,
                };
                if (null == backButton) {
                    NavigationItem.HidesBackButton = false;
                    NavigationItem.LeftItemsSupplementBackButton = true;
                    NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                        searchButton,
                    };
                } else {
                    NavigationItem.HidesBackButton = true;
                    NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                        backButton,
                        searchButton,
                    };
                }
            }
        }

        public void MultiSelectChange (IMessageTableViewSource source, int count)
        {
            archiveButton.Enabled = (count != 0);
            deleteButton.Enabled = (count != 0);
            moveButton.Enabled = (count != 0);
        }

        public int GetFirstVisibleRow ()
        {       
            var paths = TableView.IndexPathsForVisibleRows; // Must be on UI thread
            if (null == paths) {
                return -1;
            }
            var path = paths.FirstOrDefault ();
            if (null == path) {
                return -1;
            }
            return path.Row;
        }

        protected void RefreshMessage (int id)
        {
            messageSource.EmailMessageChanged (TableView, id);
        }

        protected void RefreshThreadsIfVisible ()
        {
            threadsNeedsRefresh = true;
            if (!this.IsVisible ()) {
                return;
            }
            if (searchDisplayController.Active) {
                return;
            }
            MaybeRefreshThreads ();
        }

        protected void MaybeRefreshThreads ()
        {
            bool refreshVisibleCells = true;

            if (threadsNeedsRefresh) {
                threadsNeedsRefresh = false;
                NachoCore.Utils.NcAbate.HighPriority ("MessageListViewController MaybeRefreshThreads");
                ReloadCapture.Start ();
                List<int> adds;
                List<int> deletes;
                if (messageSource.RefreshEmailMessages (out adds, out deletes)) {
                    Util.UpdateTable (TableView, adds, deletes);
                    refreshVisibleCells = false;
                }
                if (messageSource.NoMessageThreads ()) {
                    refreshVisibleCells = !MaybeDismissView ();
                }
                if (searchDisplayController.Active) {
                    UpdateSearchResults ();
                    refreshVisibleCells = false;
                }
                ReloadCapture.Stop ();
                NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController MaybeRefreshThreads");
            }
            if (refreshVisibleCells) {
                messageSource.ReconfigureVisibleCells (TableView);
            }
        }

        public virtual bool MaybeDismissView ()
        {
            return false;
        }

        public virtual bool HasAccountSwitcher ()
        {
            return false;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            // Account switched
            if (!messageSource.GetNachoEmailMessages ().IsCompatibleWithAccount (NcApplication.Instance.Account)) {
                if (searchDisplayController.Active) {
                    searchDisplayController.Active = false;
                }
                CancelSearchIfActive ();
                if (HasAccountSwitcher ()) {
                    SwitchToAccount (NcApplication.Instance.Account);
                } else {
                    NavigationController.PopViewController (true);
                    return;
                }
            }

            if (!StatusIndCallbackIsSet) {
                StatusIndCallbackIsSet = true;
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }

            // TODO: Figure this out
            // When this view is loaded directly from the tab bar,
            // the first time the view is displayed, the content
            // offset is set such that the refresh controller is
            // visible.  The second time this view is presented
            // the content offset is set to properly.
            if (0 > TableView.ContentOffset.Y) {
                TableView.ContentOffset = new CGPoint (0, 0);
            }
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            NavigationItem.Title = messageSource.GetNachoEmailMessages ().DisplayName ();

            MaybeRefreshThreads ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            cancelRefreshTimer ();
            CancelSearchIfActive ();
            // In case we exit during scrolling
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController ViewWillDisappear");
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (this.IsViewLoaded && null == this.NavigationController) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                StatusIndCallbackIsSet = false;
                threadsNeedsRefresh = true;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (null != s.Account) {
                var m = messageSource.GetNachoEmailMessages ();
                if ((null == m) || !m.IsCompatibleWithAccount (s.Account)) {
                    return;
                }
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: {0} {1}", s.Status.SubKind, m.DisplayName ());

            }
            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshThreadsIfVisible ();
                break;
            case NcResult.SubKindEnum.Info_EmailMessageChanged:
                if (s.Status.Value is int) {
                    RefreshMessage ((int)s.Status.Value);
                }
                break;
            case NcResult.SubKindEnum.Error_SyncFailed:
            case NcResult.SubKindEnum.Info_SyncSucceeded:
                cancelRefreshTimer ();
                break;
            case NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded:
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: Info_EmailSearchCommandSucceeded");
                UpdateSearchResultsFromServer (s.Status.GetValue<List<NcEmailMessageIndex>> ());
                break;
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            var blurry = segue.DestinationViewController as BlurryViewController;
            if (null != blurry) {
                blurry.CaptureView (this.View);
            }
            if (segue.Identifier == "NachoNowToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                if (null == h) {
                    // Composing a message
                    vc.SetAction (null, null);
                } else {
                    vc.SetAction ((McEmailMessageThread)h.value2, (string)h.value);
                }
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "SegueToNachoNow") {
                return;
            }
            if (segue.Identifier == "MessageListToCompose") {
                return;
            }
            if (segue.Identifier == "DraftsToCompose") {
                var vc = (MessageComposeViewController)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetDraft ((McEmailMessage)h.value);
                vc.SetOwner (this);
                return;
            }
            if (segue.Identifier == "NachoNowToMessageView") {
                var vc = (INachoMessageViewer)segue.DestinationViewController;
                var holder = (SegueHolder)sender;
                var thread = holder.value as McEmailMessageThread;
                vc.SetSingleMessageThread (thread);
                return;
            }
            if (segue.Identifier == "SegueToMessageThreadView") {
                var holder = (SegueHolder)sender;
                var thread = (McEmailMessageThread)holder.value;
                var vc = (MessageListViewController)segue.DestinationViewController;
                vc.SetEmailMessages (messageSource.GetNachoEmailMessages ().GetAdapterForThread (thread.GetThreadId ()));
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var holder = (SegueHolder)sender;
                var thread = (McEmailMessageThread)holder.value;
                var vc = (INachoDateController)segue.DestinationViewController;
                vc.Setup (this, thread, NcMessageDeferral.MessageDateType.Defer);
                return;
            }
            if (segue.Identifier == "MessageListToFolders") {
                var vc = (INachoFolderChooser)segue.DestinationViewController;
                var h = sender as SegueHolder;
                vc.SetOwner (this, true, h);
                return;
            }
            if (segue.Identifier == "NachoNowToEditEvent") {
                var vc = (EditEventViewController)segue.DestinationViewController;
                var holder = sender as SegueHolder;
                var e = holder.value as McCalendar;
                vc.SetCalendarItem (e);
                vc.SetOwner (this);
                return;
            }

            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        ///  IMessageTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

   
        ///  IMessageTableViewSourceDelegate
        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            var msg = messageSource.GetNachoEmailMessages ();
            if (msg.HasDraftsSemantics ()) {
                PerformSegue ("DraftsToCompose", new SegueHolder (messageThread.SingleMessageSpecialCase ()));
            } else if (msg.HasOutboxSemantics ()) {
                DealWithThreadInOutbox (messageThread);
            } else if (messageThread.HasMultipleMessages ()) {
                PerformSegue ("SegueToMessageThreadView", new SegueHolder (messageThread));
            } else {
                PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
            }
        }

        public void DealWithThreadInOutbox (McEmailMessageThread messageThread)
        {
            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                return;
            }

            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if ((null == pending) || (NcResult.KindEnum.Error != pending.ResultKind)) {
                var copy = EmailHelper.MoveFromOutboxToDrafts (message);
                PerformSegue ("DraftsToCompose", new SegueHolder (copy));
                return;
            }

            string errorString;
            if (!ErrorHelper.ErrorStringForSubkind (pending.ResultSubKind, out errorString)) {
                errorString = String.Format ("(ErrorCode={0}", pending.ResultSubKind);
            }
            var messageString = "There was a problem sending this message.  You can resend this message or open it in the drafts folder.";
            var alertString = String.Format ("{0}\n{1}", messageString, errorString);
            NcAlertView.Show (this, "Edit Message", alertString,
                new NcAlertAction ("OK", NcAlertActionStyle.Cancel, () => {
                    var copy = EmailHelper.MoveFromOutboxToDrafts (message);
                    PerformSegue ("DraftsToCompose", new SegueHolder (copy));
                    return;
                }));
        }


        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }

        public void DateSelected (NcMessageDeferral.MessageDateType type, MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DateSelected (type, thread, request, selectedDate);
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.DismissDateController (false, null);
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateTaskForEmailMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.FirstMessageSpecialCase ();
            if (null != m) {
                var t = CalendarHelper.CreateTask (m);
                vc.SetOwner (null);
                vc.DismissMessageEditor (false, new Action (delegate {
                    PerformSegue ("", new SegueHolder (t));
                }));
            }
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void CreateMeetingEmailForMessage (INachoMessageEditor vc, McEmailMessageThread thread)
        {
            var m = thread.FirstMessageSpecialCase ();
            if (null != m) {
                var c = CalendarHelper.CreateMeeting (m);
                vc.DismissMessageEditor (false, new Action (delegate {
                    PerformSegue ("NachoNowToEditEvent", new SegueHolder (c));
                }));
            }
        }

        /// <summary>
        /// INachoCalendarItemEditorParent Delegate
        /// </summary>
        public void DismissChildCalendarItemEditor (INachoCalendarItemEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissCalendarItemEditor (true, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void DismissChildFolderChooser (INachoFolderChooser vc)
        {
            vc.SetOwner (null, false, null);
            vc.DismissFolderChooser (false, null);
        }

        /// <summary>
        /// INachoFolderChooser Delegate
        /// </summary>
        public void FolderSelected (INachoFolderChooser vc, McFolder folder, object cookie)
        {
            if (null != messageSource) {
                messageSource.FolderSelected (vc, folder, cookie);
            }
            vc.DismissFolderChooser (true, null);
        }

        protected void BackShouldSwitchToFolders ()
        {
            using (var image = UIImage.FromBundle ("nav-backarrow")) {
//                backButton = new NcUIBarButtonItem (image, UIBarButtonItemStyle.Plain, onClickBackButton);
                var button = UIButton.FromType (UIButtonType.System);
                button.Frame = new CGRect (0, 0, 70, 30);
                button.SetTitle ("Mail", UIControlState.Normal);
                button.AccessibilityLabel = "Back";
                button.SetTitleColor (UIColor.White, UIControlState.Normal);
                button.SetImage (image, UIControlState.Normal);
                button.Font = UINavigationBar.Appearance.TitleTextAttributes.Font;
                backButton = new UIBarButtonItem (button);
                button.TouchUpInside += onClickBackButton;
            }
        }

        protected void onClickBackButton (object sender, EventArgs e)
        {
            var nachoTabBar = Util.GetActiveTabBar ();
            nachoTabBar.SwitchToFolders ();
        }

        protected void onClickSearchButton (object sender, EventArgs e)
        {
            searchBar.BecomeFirstResponder ();
        }

        [Export ("searchBar:textDidChange:")]
        public void TextChanged (UISearchBar searchBar, string searchText)
        {
            Search (searchBar);
        }

        [Foundation.Export ("searchBarSearchButtonClicked:")]
        public void SearchButtonClicked (UIKit.UISearchBar searchBar)
        {
            if (null == NcApplication.Instance.Account) {
                return;
            }
            Search (searchBar);
        }

        protected void Search (UISearchBar searchBar)
        {
            if (String.IsNullOrEmpty (searchBar.Text)) {
                searchResultsMessages.UpdateServerMatches (null);
            } else {
                // Ask the server
                KickoffSearchApi (0, searchBar.Text);
            }
            searcher.Search (searchBar.Text);
        }

        protected void KickoffSearchApi (int forSearchOption, string forSearchString)
        {
            if (String.IsNullOrEmpty (forSearchString) || (4 > forSearchString.Length)) {
                searchResultsMessages.UpdateServerMatches (null);
                return;
            }
            if (String.IsNullOrEmpty (searchToken)) {
                searchToken = BackEnd.Instance.StartSearchEmailReq (NcApplication.Instance.Account.Id, forSearchString, null).GetValue<string> ();
            } else {
                BackEnd.Instance.SearchEmailReq (NcApplication.Instance.Account.Id, forSearchString, null, searchToken);
            }
        }

        protected void UpdateSearchResultsFromServer (List<NcEmailMessageIndex> indexList)
        {
            var threadList = new List<McEmailMessageThread> ();
            foreach (var i in indexList) {
                var thread = new McEmailMessageThread ();
                thread.FirstMessageId = i.Id;
                thread.MessageCount = 1;
                threadList.Add (thread);
            }
            searchResultsMessages.UpdateServerMatches (threadList);
            List<int> adds;
            List<int> deletes;
            searchResultsSource.RefreshEmailMessages (out adds, out deletes);
            if (null != searchDisplayController.SearchResultsTableView) {
                searchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        // After status ind
        protected void UpdateSearchResults ()
        {
            searchResultsMessages.UpdateResults ();
            List<int> adds;
            List<int> deletes;
            searchResultsSource.RefreshEmailMessages (out adds, out deletes);
            if (null != searchDisplayController.SearchResultsTableView) {
                searchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        protected void CancelSearchIfActive ()
        {
            if (!String.IsNullOrEmpty (searchToken)) {
                McPending.Cancel (NcApplication.Instance.Account.Id, searchToken);
                searchToken = null;
            }
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        protected virtual INachoEmailMessages GetNachoEmailMessages (int accountId)
        {
            return NcEmailManager.Inbox (accountId);
        }

        void SwitchToAccount (McAccount account)
        {
            if (searchDisplayController.Active) {
                searchDisplayController.Active = false;
            }
            messageSource.MultiSelectCancel (TableView);
            MultiSelectToggle (messageSource, false);
            switchAccountButton.SetAccountImage (account);
            SetEmailMessages (GetNachoEmailMessages (account.Id));
            List<int> adds;
            List<int> deletes;
            messageSource.RefreshEmailMessages (out adds, out deletes);
            threadsNeedsRefresh = false;
            TableView.ReloadData ();
        }
    }

}
