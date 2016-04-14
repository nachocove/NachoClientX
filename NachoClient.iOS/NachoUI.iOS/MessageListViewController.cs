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
using NachoCore.Index;

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoCalendarItemEditorParent, INachoFolderChooserParent, MessageTableViewSourceDelegate
    {
        MessageTableViewSource messageSource;
        MessageTableViewSource searchResultsSource;
        EmailSearch emailSearcher;
        protected UIBarButtonItem composeMailButton;
        protected UIBarButtonItem multiSelectButton;
        protected UIBarButtonItem cancelSelectedButton;
        protected UIBarButtonItem archiveButton;
        protected UIBarButtonItem deleteButton;
        protected UIBarButtonItem searchButton;
        protected UIBarButtonItem moveButton;
        protected UIBarButtonItem backButton;
        protected UIBarButtonItem filterButton;

        protected UISearchBar searchBar;
        protected UISearchDisplayController searchDisplayController;

        public bool HasAccountSwitcher;
        public bool PopsWhenEmpty;

        SwitchAccountButton switchAccountButton;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected bool threadsNeedsRefresh;
        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        bool StatusIndCallbackIsSet = false;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads, "No messages");
        }

        public MessageListViewController () : base ()
        {
            messageSource = new MessageTableViewSource (this);
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            messageSource = new MessageTableViewSource (this);
        }

        public override void LoadView ()
        {
            base.LoadView ();
            if (TableView == null) {
                TableView = new UITableView (new CGRect (0.0f, 0.0f, 320.0f, 320.0f), UITableViewStyle.Plain);
                View = TableView;
            }
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NavigationController.NavigationBar.Translucent = false;

            if (HasAccountSwitcher) {
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
                ComposeMessage ();
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
                MoveSelected ();
            };

            filterButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (filterButton, "gen-read-list");
            filterButton.AccessibilityLabel = "Filter";
            filterButton.Clicked += (object sender, EventArgs e) => {
                var messages = messageSource.GetNachoEmailMessages ();
                var actions = new List<NcAlertAction> ();
                foreach (var value in messages.PossibleFilterSettings) {
                    actions.Add (new NcAlertAction (Folder_Helpers.FilterShortString (value), () => {
                        SetFilter (value);
                    }));
                }
                actions.Add (new NcAlertAction ("Cancel", NcAlertActionStyle.Cancel, null));
                NcActionSheet.Show (filterButton, this, "Message Filter", null, actions.ToArray ());
            };

            searchButton = new NcUIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.AccessibilityLabel = "Search";
            searchButton.Clicked += onClickSearchButton;

            TableView.SeparatorColor = A.Color_NachoBackgroundGray;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            TableView.Source = messageSource.GetTableViewSource ();

            CustomizeBackButton ();
            MultiSelectToggle (messageSource, false);

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
            emailSearcher = new EmailSearch ((string searchString, List<McEmailMessageThread> results) => {
                UpdateSearchResults ();
            });
            searchResultsSource = new MessageTableViewSource (this);
            searchResultsSource.SetEmailMessages (emailSearcher, "");
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

        void SetFilter (FolderFilterOptions value)
        {
            var messages = messageSource.GetNachoEmailMessages ();
            messages.FilterSetting = value;
            RefreshThreadsIfVisible ();
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
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        protected virtual void CustomizeBackButton ()
        {
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
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
                    if (messageSource.GetNachoEmailMessages ().HasFilterSemantics ()) {
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                            searchButton,
                            filterButton,
                        };
                    } else {
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                            searchButton,
                        };
                    }
                } else {
                    NavigationItem.HidesBackButton = true;
                    if (messageSource.GetNachoEmailMessages ().HasFilterSemantics ()) {
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                            backButton,
                            searchButton,
                            filterButton,
                        };
                    } else {
                        NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                            backButton,
                            searchButton,
                        };
                    }
                }
            }
        }

        public void MultiSelectChange (MessageTableViewSource source, int count, bool multipleAccounts)
        {
            archiveButton.Enabled = (count != 0);
            deleteButton.Enabled = (count != 0);
            moveButton.Enabled = (count != 0) && !multipleAccounts;
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
                using (NcAbate.UIAbatement ()) {
                    threadsNeedsRefresh = false;
                    ReloadCapture.Start ();
                    List<int> adds;
                    List<int> deletes;
                    if (messageSource.RefreshEmailMessages (out adds, out deletes)) {
                        Util.UpdateTable (TableView, adds, deletes);
                        refreshVisibleCells = false;
                    }
                    if (messageSource.NoMessageThreads ()) {
                        if (PopsWhenEmpty && NavigationController.TopViewController == this) {
                            NavigationController.PopViewController (true);
                        }
                    }
                    if (searchDisplayController.Active) {
                        UpdateSearchResults ();
                        refreshVisibleCells = false;
                    }
                    ReloadCapture.Stop ();
                }
            }
            if (refreshVisibleCells) {
                messageSource.ReconfigureVisibleCells (TableView);
            }
        }

        McAccount currentAccount;

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            // Account switched
            if ((null == currentAccount) || (currentAccount.Id != NcApplication.Instance.Account.Id)) {
                if (searchDisplayController.Active) {
                    searchDisplayController.Active = false;
                }
                CancelSearchIfActive ();
                if (HasAccountSwitcher) {
                    SwitchToAccount (NcApplication.Instance.Account);
                }
            }
            currentAccount = NcApplication.Instance.Account;
                
            if (HasAccountSwitcher) {
                switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
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
                // KLUDGE - always handle for unified account
                if (McAccount.GetUnifiedAccount ().Id != NcApplication.Instance.Account.Id) {
                    var m = messageSource.GetNachoEmailMessages ();
                    if ((null == m) || !m.IsCompatibleWithAccount (s.Account)) {
                        return;
                    }
                }
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: {0}", s.Status.SubKind);

            }
            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
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
            }
        }

        public void MoveThread (McEmailMessageThread thread)
        {
            var vc = new FoldersViewController ();
            var message = thread.FirstMessage ();
            if (message != null) {
                vc.SetOwner (this, true, message.AccountId, thread);
                PresentViewController (vc, true, null);
            }
        }

        void MoveSelected ()
        {
            var vc = new FoldersViewController ();
            var accountId = messageSource.MultiSelectAccount (TableView);
            NcAssert.False (0 == accountId);
            vc.SetOwner (this, true, accountId, TableView);
            PresentViewController (vc, true, null);
        }

        ///  IMessageTableViewSourceDelegate
        public void MessageThreadSelected (McEmailMessageThread messageThread)
        {
            var msg = messageSource.GetNachoEmailMessages ();
            if (msg.HasDraftsSemantics ()) {
                ComposeDraft (messageThread.SingleMessageSpecialCase ());
            } else if (msg.HasOutboxSemantics ()) {
                DealWithThreadInOutbox (messageThread);
            } else if (messageThread.HasMultipleMessages ()) {
                ShowThread (messageThread);
            } else {
                ShowMessage (messageThread);
            }
        }

        void ShowThread (McEmailMessageThread thread)
        {
            var vc = new MessageThreadViewController ();
            vc.SetEmailMessages (messageSource.GetNachoEmailMessages ().GetAdapterForThread (thread));
            NavigationController.PushViewController (vc, true);
        }

        void ShowMessage (McEmailMessageThread thread)
        {
            var messageViewController = new MessageViewController ();
            messageViewController.SetSingleMessageThread (thread);
            NavigationController.PushViewController (messageViewController, true);
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
                ComposeDraft (copy);
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
                    ComposeDraft (copy);
                    return;
                }));
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
            vc.SetOwner (null, false, 0, null);
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
            searchBar.Hidden = false;
            searchBar.BecomeFirstResponder ();
            emailSearcher.EnterSearchMode (NcApplication.Instance.Account);
        }

        [Export ("searchBar:textDidChange:")]
        public void TextChanged (UISearchBar searchBar, string searchText)
        {
            emailSearcher.SearchFor (searchBar.Text);
        }

        [Export ("searchBarSearchButtonClicked:")]
        public void SearchButtonClicked (UISearchBar searchBar)
        {
            if (null == NcApplication.Instance.Account) {
                return;
            }
            emailSearcher.StartServerSearch ();
        }

        [Export ("searchBarCancelButtonClicked:")]
        public void CancelButtonClicked (UISearchBar searchBar)
        {
            emailSearcher.ExitSearchMode ();
            searchDisplayController.Active = false;
            searchBar.Hidden = true;
        }

        // After status ind
        protected void UpdateSearchResults ()
        {
            List<int> adds;
            List<int> deletes;
            NcAssert.NotNull (searchResultsSource, "UpdateSearchResults: searchResultsSource is null");
            searchResultsSource.RefreshEmailMessages (out adds, out deletes);
            NcAssert.NotNull (searchDisplayController, "UpdateSearchResults: searchDisplayController is null");
            if (null != searchDisplayController.SearchResultsTableView) {
                searchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        protected void CancelSearchIfActive ()
        {
            emailSearcher.ExitSearchMode ();
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
            switchAccountButton.SetAccountImage (account);
            SetEmailMessages (GetNachoEmailMessages (account.Id));
            MultiSelectToggle (messageSource, false);
            List<int> adds;
            List<int> deletes;
            messageSource.RefreshEmailMessages (out adds, out deletes);
            threadsNeedsRefresh = false;
            TableView.ReloadData ();
        }

        void ComposeMessage ()
        {
            var composeViewController = new MessageComposeViewController (NcApplication.Instance.DefaultEmailAccount);
            composeViewController.Present ();
        }

        void ComposeDraft (McEmailMessage draft)
        {
            var account = McAccount.EmailAccountForMessage (draft);
            var composeViewController = new MessageComposeViewController (account);
            composeViewController.Composer.Message = draft;
            composeViewController.Present ();
        }

        public void RespondToMessageThread (McEmailMessageThread thread, EmailHelper.Action action)
        {
            ComposeResponse (thread, action);
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
    }

}
