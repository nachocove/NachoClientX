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

namespace NachoClient.iOS
{
    public partial class MessageListViewController : NcUITableViewController, IUISearchDisplayDelegate, IUISearchBarDelegate, INachoMessageEditorParent, INachoCalendarItemEditorParent, INachoFolderChooserParent, IMessageTableViewSourceDelegate, INachoDateControllerParent
    {
        MessageTableViewSource messageSource;
        MessageTableViewSource searchResultsSource;
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

        protected const string UICellReuseIdentifier = "UICell";
        protected const string EmailMessageReuseIdentifier = "EmailMessage";

        protected bool threadsNeedsRefresh;
        protected NcCapture ReloadCapture;
        private string ReloadCaptureName;

        public void SetEmailMessages (INachoEmailMessages messageThreads)
        {
            this.messageSource.SetEmailMessages (messageThreads);
        }

        public MessageListViewController (IntPtr handle) : base (handle)
        {
            messageSource = new MessageTableViewSource ();
        }
            
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            ReloadCaptureName = "MessageListViewController.Reload";
            NcCapture.AddKind (ReloadCaptureName);
            ReloadCapture = NcCapture.Create (ReloadCaptureName);

            composeMailButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (composeMailButton, "contact-newemail");
            composeMailButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("MessageListToCompose", this);
            };

            multiSelectButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (multiSelectButton, "folder-edit");
            multiSelectButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectEnable (TableView);
            };

            cancelSelectedButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (cancelSelectedButton, "gen-close");
            cancelSelectedButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectCancel (TableView);
            };

            archiveButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (archiveButton, "gen-archive");
            archiveButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectArchive (TableView);
            };

            deleteButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (deleteButton, "gen-delete-all");
            deleteButton.Clicked += (object sender, EventArgs e) => {
                messageSource.MultiSelectDelete (TableView);
            };

            moveButton = new UIBarButtonItem ();
            Util.SetAutomaticImageForButton (moveButton, "folder-move");
            moveButton.Clicked += (object sender, EventArgs e) => {
                var h = new SegueHolder (TableView);
                PerformSegue ("MessageListToFolders", h);
            };

            searchButton = new UIBarButtonItem (UIBarButtonSystemItem.Search);
            searchButton.Clicked += onClickSearchButton;

            TableView.RowHeight = 126;
            TableView.SeparatorColor = A.Color_NachoBackgroundGray;
            NavigationController.NavigationBar.Translucent = false;
            Util.HideBlackNavigationControllerLine (NavigationController.NavigationBar);

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            messageSource.owner = this;
            TableView.Source = messageSource;

            CustomizeBackButton ();
            MultiSelectToggle (messageSource, false);

            TableView.TableHeaderView = null; // beta 1

            RefreshControl = new UIRefreshControl ();
            RefreshControl.Hidden = true;
            RefreshControl.TintColor = A.Color_NachoGreen;
            RefreshControl.AttributedTitle = new NSAttributedString ("Refreshing...");
            RefreshControl.ValueChanged += (object sender, EventArgs e) => {
                RefreshControl.BeginRefreshing ();
                messageSource.StartSync ();
                RefreshThreadsIfVisible ();
                new NcTimer ("MessageListViewController refresh", refreshCallback, null, 2000, 0);
            };

            searchBar = new UISearchBar ();
            searchBar.Delegate = this;
            searchDisplayController = new UISearchDisplayController (searchBar, this);
            searchResultsMessages = new NachoMessageSearchResults ();
            searchResultsSource = new MessageTableViewSource ();
            searchResultsSource.SetEmailMessages (searchResultsMessages);
            searchResultsSource.owner = this;
            searchDisplayController.SearchResultsSource = searchResultsSource;
            searchDisplayController.SearchResultsTableView.RowHeight = 126;

            View.AddSubview (searchBar);

            Util.ConfigureNavBar (false, this.NavigationController);


            // Load when view becomes visible
            threadsNeedsRefresh = true;
        }

        protected void refreshCallback (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                RefreshControl.EndRefreshing ();
            });
        }

        protected virtual void CustomizeBackButton ()
        {
        }

        public void MultiSelectToggle (MessageTableViewSource source, bool enabled)
        {
            if (enabled) {
                NavigationItem.RightBarButtonItems = new UIBarButtonItem[] {
                    deleteButton,
                    moveButton,
                    archiveButton,
                };
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

        public void MultiSelectChange (MessageTableViewSource source, int count)
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

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

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
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            NavigationItem.Title = messageSource.GetDisplayName ();

            MaybeRefreshThreads ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            CancelSearchIfActive ();
            // In case we exit during scrolling
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListViewController ViewWillDisappear");
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                RefreshThreadsIfVisible ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded == s.Status.SubKind) {
                RefreshThreadsIfVisible ();
            }
            if (NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded == s.Status.SubKind) {
                RefreshThreadsIfVisible ();
            }
            if (NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback: Info_EmailSearchCommandSucceeded");
                UpdateSearchResultsFromServer (s.Status.GetValue<List<NcEmailMessageIndex>> ());
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
                vc.SetEmailMessages (messageSource.GetAdapterForThread (thread.GetThreadId ()));
                return;
            }
            if (segue.Identifier == "NachoNowToMessagePriority") {
                var holder = (SegueHolder)sender;
                var thread = (McEmailMessageThread)holder.value;
                var vc = (INachoDateController)segue.DestinationViewController;
                vc.Setup (this, thread, DateControllerType.Defer);
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
            if (messageThread.HasMultipleMessages ()) {
                PerformSegue ("SegueToMessageThreadView", new SegueHolder (messageThread));
            } else {
                PerformSegue ("NachoNowToMessageView", new SegueHolder (messageThread));
            }
        }

        /// <summary>
        /// INachoMessageControl delegate
        /// </summary>
        public void DismissChildMessageEditor (INachoMessageEditor vc)
        {
            vc.SetOwner (null);
            vc.DismissMessageEditor (false, null);
        }

        public void DateSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DeferThread (thread, request, selectedDate);
        }

        public void DismissChildDateController (INachoDateController vc)
        {
            vc.Setup (null, null, DateControllerType.None);
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
//                backButton = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, onClickBackButton);
                var button = UIButton.FromType (UIButtonType.System);
                button.Frame = new CGRect (0, 0, 70, 30);
                button.SetTitle ("Mail", UIControlState.Normal);
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
            searchResultsMessages.UpdateMatches (null);
            searchResultsMessages.UpdateServerMatches (null);
            Search (searchBar);
        }

        protected void Search (UISearchBar searchBar)
        {
            // Ask the server
            KickoffSearchApi (0, searchBar.Text);
            // On-device index
            var indexPath = NcModel.Instance.GetIndexPath (NcApplication.Instance.Account.Id);
            var index = new NachoCore.Index.NcIndex (indexPath);
            var match = searchBar.Text;
            var pattern = String.Format ("to:\"{0}\" subject:\"{0}\"  body:\"{0}\"", match);
            var matches = index.Search (pattern);
            searchResultsMessages.UpdateMatches (matches);
            List<int> adds;
            List<int> deletes;
            searchResultsSource.RefreshEmailMessages (out adds, out deletes);
            if (null != searchDisplayController.SearchResultsTableView) {
                searchDisplayController.SearchResultsTableView.ReloadData ();
            }
        }

        protected void KickoffSearchApi (int forSearchOption, string forSearchString)
        {
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
                BackEnd.Instance.Cancel (NcApplication.Instance.Account.Id, searchToken);
                searchToken = null;
            }
        }
    }

}
