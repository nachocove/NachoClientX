
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;

//using Android.Util;
using Android.Views;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using Android.Graphics.Drawables;
using NachoCore.Brain;
using NachoPlatform;
using Android.Widget;
using Android.Views.InputMethods;
using Android.Webkit;

namespace NachoClient.AndroidClient
{
    public interface MessageListDelegate
    {
        void ListIsEmpty ();

        bool ShowHotEvent ();

        int ShowListStyle ();

        void SetActiveImage (View view);
    }

    public class MessageListFragment : Fragment
    {
        private const int ARCHIVE_TAG = 1;
        private const int SAVE_TAG = 2;
        private const int DELETE_TAG = 3;
        private const int DEFER_TAG = 4;

        private const int LATE_TAG = 1;
        private const int FORWARD_TAG = 2;

        SwipeMenuListView listView;
        MessageListAdapter messageListAdapter;
        HotEventAdapter hotEventAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        bool searching;
        string searchToken;
        SearchHelper searcher;
        Android.Widget.EditText searchEditText;

        public INachoEmailMessages messages;
        public NachoMessageSearchResults searchResultsMessages;

        public bool multiSelectActive = false;
        public HashSet<long> MultiSelectSet = null;

        Android.Widget.ImageView leftButton1;
        Android.Widget.ImageView rightButton1;
        Android.Widget.ImageView rightButton2;
        Android.Widget.ImageView rightButton3;

        public event EventHandler<McEvent> onEventClick;
        public event EventHandler<INachoEmailMessages> onThreadClick;
        public event EventHandler<McEmailMessageThread> onMessageClick;

        public void Initialize (INachoEmailMessages messages, EventHandler<McEvent> eventClickHandler, EventHandler<INachoEmailMessages> threadClickHandler, EventHandler<McEmailMessageThread> messageClickHandler)
        {
            this.messages = messages;
            if (null != eventClickHandler) {
                onEventClick += eventClickHandler;
            }
            if (null != threadClickHandler) {
                onThreadClick += threadClickHandler;
            }
            if (null != messageClickHandler) {
                onMessageClick += messageClickHandler;
            }
        }

        public void Initialize (INachoEmailMessages messages, EventHandler<McEmailMessageThread> messageClickHandler)
        {
            Initialize (messages, null, null, messageClickHandler);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var parent = (MessageListDelegate)Activity;
            var view = inflater.Inflate (Resource.Layout.MessageListFragment, container, false);

            if (Activity is NcTabBarActivity) {
                var activity = (NcTabBarActivity)this.Activity;
                activity.HookNavigationToolbar (view);
            } else {
                var navToolbar = view.FindViewById<View> (Resource.Id.navigation_toolbar);
                navToolbar.Visibility = ViewStates.Gone;
            }

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += SwipeRefreshLayout_Refresh;

            leftButton1 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.left_button1);
            leftButton1.Click += LeftButton1_Click;

            rightButton1 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            rightButton1.Click += RightButton1_Click;

            rightButton2 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button2);
            rightButton2.Click += RightButton2_Click;

            rightButton3 = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button3);
            rightButton3.Click += RightButton3_Click;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.ScrollStateChanged += ListView_ScrollStateChanged;
            listView.ItemClick += ListView_ItemClick;

            listView.DividerHeight = 0;

            SetupMessageListAdapter (view);

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            searchResultsMessages = new NachoMessageSearchResults (NcApplication.Instance.Account.Id);

            searcher = new SearchHelper ("MessageListViewController", (searchString) => {
                if (String.IsNullOrEmpty (searchString)) {
                    searchResultsMessages.UpdateMatches (null);
                    searchResultsMessages.UpdateServerMatches (null);
                    messageListAdapter.RefreshSearchMatches ();
                    return; 
                }
                // On-device index
                var indexPath = NcModel.Instance.GetIndexPath (NcApplication.Instance.Account.Id);
                var index = new NachoCore.Index.NcIndex (indexPath);
                int maxResults = 1000;
                if (4 > searchString.Length) {
                    maxResults = 20;
                }
                var matches = index.SearchAllEmailMessageFields (searchString, maxResults);

                // Cull low scores
                var maxScore = 0f;
                foreach (var m in matches) {
                    maxScore = Math.Max (maxScore, m.Score);
                }
                matches.RemoveAll (x => x.Score < (maxScore / 2));

                InvokeOnUIThread.Instance.Invoke (() => {
                    searchResultsMessages.UpdateMatches (matches);
                    messageListAdapter.RefreshSearchMatches ();
                });
            });
                
            var hotEvent = view.FindViewById<View> (Resource.Id.hot_event);

            if (parent.ShowHotEvent ()) {
                hotEvent.Visibility = ViewStates.Visible;
                var hoteventListView = view.FindViewById<SwipeMenuListView> (Resource.Id.hotevent_listView);
                hotEventAdapter = new HotEventAdapter ();
                hoteventListView.Adapter = hotEventAdapter;
                var hoteventEmptyView = view.FindViewById<View> (Resource.Id.hot_event_empty);
                hoteventListView.EmptyView = hoteventEmptyView;

                hoteventListView.ItemClick += HoteventListView_ItemClick;

                hoteventListView.setMenuCreator ((menu) => {
                    SwipeMenuItem lateItem = new SwipeMenuItem (Activity.ApplicationContext);
                    lateItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarLate));
                    lateItem.setWidth (dp2px (90));
                    lateItem.setTitle ("I'm Late");
                    lateItem.setTitleSize (14);
                    lateItem.setTitleColor (A.Color_White);
                    lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                    lateItem.setId (LATE_TAG);
                    menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                    SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                    forwardItem.setBackground (new ColorDrawable (A.Color_NachoSwipeCalendarForward));
                    forwardItem.setWidth (dp2px (90));
                    forwardItem.setTitle ("Forward");
                    forwardItem.setTitleSize (14);
                    forwardItem.setTitleColor (A.Color_White);
                    forwardItem.setIcon (A.Id_NachoSwipeCalendarForward);
                    forwardItem.setId (FORWARD_TAG);
                    menu.addMenuItem (forwardItem, SwipeMenu.SwipeSide.RIGHT);
                });

                hoteventListView.setOnMenuItemClickListener (( position, menu, index) => {
                    var cal = CalendarHelper.GetMcCalendarRootForEvent (hotEventAdapter [position].Id);
                    switch (index) {
                    case LATE_TAG:
                        if (null != cal) {
                            var outgoingMessage = McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Re: " + cal.GetSubject ());
                            outgoingMessage.To = cal.OrganizerEmail;
                            StartActivity (MessageComposeActivity.InitialTextIntent (this.Activity, outgoingMessage, "Running late."));
                        }
                        break;
                    case FORWARD_TAG:
                        if (null != cal) {
                            StartActivity (MessageComposeActivity.ForwardCalendarIntent (
                                this.Activity, cal.Id, McEmailMessage.MessageWithSubject (NcApplication.Instance.Account, "Fwd: " + cal.GetSubject ())));
                        }
                        break;
                    default:
                        throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                    }
                    return false;
                });
            } else {
                hotEvent.Visibility = ViewStates.Gone;
            }
                
            ConfigureButtons ();
            parent.SetActiveImage (view);

            MaybeDisplayNoMessagesView (view);

            return view;
        }

        void SetupMessageListAdapter (View view)
        {
            var parent = (MessageListDelegate)Activity;
            messageListAdapter = new MessageListAdapter (this, parent.ShowListStyle ());

            listView.Adapter = messageListAdapter;

            listView.setMenuCreator ((menu) => {
                if (!(messages.HasDraftsSemantics () || messages.HasOutboxSemantics ())) {
                    SwipeMenuItem deferItem = new SwipeMenuItem (Activity.ApplicationContext);
                    deferItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailDefer));
                    deferItem.setWidth (dp2px (90));
                    deferItem.setTitle ("Defer");
                    deferItem.setTitleSize (14);
                    deferItem.setTitleColor (A.Color_White);
                    deferItem.setIcon (A.Id_NachoSwipeEmailDefer);
                    deferItem.setId (DEFER_TAG);
                    menu.addMenuItem (deferItem, SwipeMenu.SwipeSide.LEFT);
                    SwipeMenuItem moveItem = new SwipeMenuItem (Activity.ApplicationContext);
                    moveItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailMove));
                    moveItem.setWidth (dp2px (90));
                    moveItem.setTitle ("Move");
                    moveItem.setTitleSize (14);
                    moveItem.setTitleColor (A.Color_White);
                    moveItem.setIcon (A.Id_NachoSwipeEmailMove);
                    moveItem.setId (SAVE_TAG);
                    menu.addMenuItem (moveItem, SwipeMenu.SwipeSide.LEFT);
                    SwipeMenuItem archiveItem = new SwipeMenuItem (Activity.ApplicationContext);
                    archiveItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailArchive));
                    archiveItem.setWidth (dp2px (90));
                    archiveItem.setTitle ("Archive");
                    archiveItem.setTitleSize (14);
                    archiveItem.setTitleColor (A.Color_White);
                    archiveItem.setIcon (A.Id_NachoSwipeEmailArchive);
                    archiveItem.setId (ARCHIVE_TAG);
                    menu.addMenuItem (archiveItem, SwipeMenu.SwipeSide.RIGHT);
                }
                SwipeMenuItem deleteItem = new SwipeMenuItem (Activity.ApplicationContext);
                deleteItem.setBackground (new ColorDrawable (A.Color_NachoSwipeEmailDelete));
                deleteItem.setWidth (dp2px (90));
                deleteItem.setTitle ("Delete");
                deleteItem.setTitleSize (14);
                deleteItem.setTitleColor (A.Color_White);
                deleteItem.setIcon (A.Id_NachoSwipeEmailDelete);
                deleteItem.setId (DELETE_TAG);
                menu.addMenuItem (deleteItem, SwipeMenu.SwipeSide.RIGHT);
            }
            );

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                var messageThread = messages.GetEmailThread (position);
                switch (index) {
                case SAVE_TAG:
                    ShowFolderChooser (messageThread);
                    break;
                case DEFER_TAG:
                    ShowPriorityChooser (messageThread);
                    break;
                case ARCHIVE_TAG:
                    ArchiveThisMessage (messageThread);
                    break;
                case DELETE_TAG:
                    DeleteThisMessage (messageThread);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
                }
                return false;
            });

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });
        }

        void SwipeRefreshLayout_Refresh (object sender, EventArgs e)
        {
            var nr = messages.StartSync ();
            rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
        }

        void ListView_ScrollStateChanged (object sender, AbsListView.ScrollStateChangedEventArgs e)
        {
            switch (e.ScrollState) {
            case ScrollState.TouchScroll:
            case ScrollState.Fling:
                listView.EnableSwipe (false);
                if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                    NachoCore.Utils.NcAbate.HighPriority ("MessageListFragment ScrollStateChanged");
                }
                break;
            case ScrollState.Idle:
                listView.EnableSwipe (true);
                if (NcApplication.Instance.IsBackgroundAbateRequired) {
                    NachoCore.Utils.NcAbate.RegularPriority ("MessageListFragment ScrollStateChanged");
                }
                break;
            }
        }

        public override void OnResume ()
        {
            base.OnResume ();
            RefreshIfNeeded ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            CancelSearchIfActive ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListFragment OnPause");
        }

        void HoteventListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            if (null != onEventClick) {
                var currentEvent = hotEventAdapter [0];
                if (null != currentEvent) {
                    onEventClick (this, currentEvent);
                }
            }
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            if (multiSelectActive) {
                if (MultiSelectSet.Contains (e.Position)) {
                    MultiSelectSet.Remove (e.Position);
                } else {
                    MultiSelectSet.Add (e.Position);
                }
                RefreshVisibleMessageCells ();
                return;
            }

            var thread = messageListAdapter [e.Position];

            if (1 == thread.MessageCount) {
                if (null != onMessageClick) {
                    onMessageClick (this, thread);
                }
            } else {
                var threadMessages = messages.GetAdapterForThread (thread.GetThreadId ());
                if (null != onThreadClick) {
                    onThreadClick (this, threadMessages);
                }
            }
        }


        // Search or disable multi-select (multi-select)
        void LeftButton1_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectCancel ();
            } else {
                SearchButton_Click (sender, e);
            }
        }

        // Compose or delete (multi-select)
        void RightButton1_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectDelete ();
            } else {
                StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity));
            }
        }

        // Enable multi-select or save to folder (multi-select)
        void RightButton2_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                ShowFolderChooser (null);
            } else {
                MultiSelectSet = new HashSet<long> ();
                multiSelectActive = true;
                ConfigureButtons ();
            }
        }

        // Blank or archive (multi-select)
        void RightButton3_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectArchive ();
            }
        }

        void ConfigureButtons ()
        {
            if (multiSelectActive) {
                listView.EnableSwipe (false);
                if (messages.HasDraftsSemantics () || messages.HasOutboxSemantics ()) {
                    leftButton1.SetImageResource (Resource.Drawable.gen_close);
                    leftButton1.Visibility = ViewStates.Visible;
                    rightButton1.SetImageResource (Resource.Drawable.gen_delete_all);
                    rightButton1.Visibility = ViewStates.Visible;
                    rightButton2.Visibility = ViewStates.Invisible;
                    rightButton3.Visibility = ViewStates.Invisible;
                } else {
                    leftButton1.SetImageResource (Resource.Drawable.gen_close);
                    leftButton1.Visibility = ViewStates.Visible;
                    rightButton1.SetImageResource (Resource.Drawable.gen_delete_all);
                    rightButton1.Visibility = ViewStates.Visible;
                    rightButton2.SetImageResource (Resource.Drawable.folder_move);
                    rightButton2.Visibility = ViewStates.Visible;
                    rightButton3.SetImageResource (Resource.Drawable.gen_archive);
                    rightButton3.Visibility = ViewStates.Visible;
                }
            } else {
                listView.EnableSwipe (true);
                leftButton1.SetImageResource (Resource.Drawable.nav_search);
                leftButton1.Visibility = ViewStates.Visible;
                rightButton1.SetImageResource (Resource.Drawable.contact_newemail);
                rightButton1.Visibility = ViewStates.Visible;
                rightButton2.SetImageResource (Resource.Drawable.folder_edit);
                rightButton2.Visibility = ViewStates.Visible;
                rightButton3.Visibility = ViewStates.Invisible;
            }
            RefreshVisibleMessageCells ();
        }

        void MultiSelectCancel ()
        {
            multiSelectActive = false;
            MultiSelectSet = null;
            ConfigureButtons ();
        }

        protected void EndRefreshingOnUIThread (object sender)
        {
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (mSwipeRefreshLayout.Refreshing) {
                    mSwipeRefreshLayout.Refreshing = false;
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
            refreshTimer = new NcTimer ("MessageListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
        }

        void cancelRefreshTimer ()
        {
            if (mSwipeRefreshLayout.Refreshing) {
                EndRefreshingOnUIThread (null);
            }
            if (null != refreshTimer) {
                refreshTimer.Dispose ();
                refreshTimer = null;
            }
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

        void SearchButton_Click (object sender, EventArgs e)
        {
            if (0 != messages.Count ()) {
                StartSearching ();
            }
        }


        void CancelButton_Click (object sender, EventArgs e)
        {
            if (searching) {
                CancelSearch ();
            }
        }

        void StartSearching ()
        {
            searching = true;
            messageListAdapter.StartSearch ();

            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Visible;
            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Gone;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Gone;

            var parent = (MessageListDelegate)Activity;
            var hotEvent = View.FindViewById<View> (Resource.Id.hot_event);
            hotEvent.Visibility = ViewStates.Gone;

            searchEditText.RequestFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.ShowSoftInput (searchEditText, ShowFlags.Implicit);
        }

        void CancelSearch ()
        {
            searching = false;
            messageListAdapter.CancelSearch ();

            searchEditText.ClearFocus ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            searchEditText.Text = "";

            var navbar = View.FindViewById (Resource.Id.navigation_bar);
            navbar.Visibility = ViewStates.Visible;
            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
            navtoolbar.Visibility = ViewStates.Visible;
            var search = View.FindViewById (Resource.Id.search);
            search.Visibility = ViewStates.Gone;

            var parent = (MessageListDelegate)Activity;
            var hotEvent = View.FindViewById<View> (Resource.Id.hot_event);
            if (parent.ShowHotEvent ()) {
                hotEvent.Visibility = ViewStates.Visible;
            }
        }

        protected void CancelSearchIfActive ()
        {
            if (!String.IsNullOrEmpty (searchToken)) {
                McPending.Cancel (NcApplication.Instance.Account.Id, searchToken);
                searchToken = null;
            }
        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            var searchString = searchEditText.Text;
            if (String.IsNullOrEmpty (searchString)) {
                searchResultsMessages.UpdateServerMatches (null);
                messageListAdapter.RefreshSearchMatches ();
            } else if (4 > searchString.Length) {
                KickoffSearchApi (0, searchString);
            }
            searcher.Search (searchString);
        }

        // Ask the server
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
            messageListAdapter.RefreshSearchMatches ();
        }

        public void OnBackPressed ()
        {
            if (searching) {
                CancelSearch ();
            }
        }

        public void DeleteThisMessage (McEmailMessageThread messageThread)
        {
            if (messages.HasOutboxSemantics ()) {
                EmailHelper.DeleteEmailThreadFromOutbox (messageThread);
                return;
            }
            if (messages.HasDraftsSemantics ()) {
                EmailHelper.DeleteEmailThreadFromDrafts (messageThread);
                return;
            }
            NcAssert.NotNull (messageThread);
            Log.Debug (Log.LOG_UI, "DeleteThisMessage");
            NcEmailArchiver.Delete (messageThread);
        }

        public void ArchiveThisMessage (McEmailMessageThread messageThread)
        {
            NcAssert.NotNull (messageThread);
            NcEmailArchiver.Archive (messageThread);
        }

        public void MultiSelectDelete ()
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Delete (messageList);
            MultiSelectCancel ();
        }

        public void MultiSelectMove (McFolder folder)
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Move (messageList, folder);
            MultiSelectCancel ();
        }

        public void MultiSelectArchive ()
        {
            var messageList = GetSelectedMessages ();
            NcEmailArchiver.Archive (messageList);
            MultiSelectCancel ();
        }

        public void ShowPriorityChooser (McEmailMessageThread messageThread)
        {
            Log.Info (Log.LOG_UI, "ShowPriorityChooser: {0}", messageThread);
            var deferralFragment = ChooseDeferralFragment.newInstance (messageThread);
            deferralFragment.setOnDeferralSelected (OnDeferralSelected);
            deferralFragment.Show (FragmentManager, "ChooseDeferralFragment");
        }

        public void ShowFolderChooser (McEmailMessageThread messageThread)
        {
            Log.Info (Log.LOG_UI, "ShowFolderChooser: {0}", messageThread);
            var folderFragment = ChooseFolderFragment.newInstance (messageThread);
            folderFragment.SetOnFolderSelected (OnFolderSelected);
            folderFragment.Show (FragmentManager, "ChooseFolderFragment");
        }

        public void OnDeferralSelected (MessageDeferralType request, McEmailMessageThread thread, DateTime selectedDate)
        {
            NcMessageDeferral.DateSelected (NcMessageDeferral.MessageDateType.Defer, thread, request, selectedDate);
        }

        public void OnFolderSelected (McFolder folder, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "OnFolderSelected: {0}", thread);
            if (multiSelectActive) {
                MultiSelectMove (folder);
            } else {
                NcEmailArchiver.Move (thread, folder);
            }
        }

        public void MaybeSwitchStyle (int style)
        {
            if (style != messageListAdapter.currentStyle) {
                SwitchAccount (messages);
            }
        }

        public void SwitchAccount (INachoEmailMessages newMessages)
        {
            ClearCache ();
            messages = newMessages;
            if (null != listView) {
                SetupMessageListAdapter (View);
            }
            MaybeDisplayNoMessagesView (View);
        }

        void RefreshVisibleMessageCells ()
        {
            ClearCache ();
            for (var i = listView.FirstVisiblePosition; i <= listView.LastVisiblePosition; i++) {
                var cell = listView.GetChildAt (i - listView.FirstVisiblePosition);
                if (null != cell) {
                    messageListAdapter.GetView (i, cell, listView);
                }
            }
        }

        public List<McEmailMessage> GetSelectedMessages ()
        {
            var messageList = new List<McEmailMessage> ();

            foreach (var messageThreadIndex in MultiSelectSet) {
                var messageThread = messages.GetEmailThread ((int)messageThreadIndex);
                foreach (var message in messageThread) {
                    messageList.Add (message);
                }
            }
            return messageList;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (null == s.Account) {
                return;
            }
            if (NcApplication.Instance.Account.Id != s.Account.Id) {
                return;
            }

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshVisibleMessageCells ();
                break;
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
                RefreshIfVisible ();
                break;
            case NcResult.SubKindEnum.Info_EmailSearchCommandSucceeded:
                UpdateSearchResultsFromServer (s.Status.GetValue<List<NcEmailMessageIndex>> ());
                break;
            case NcResult.SubKindEnum.Error_SyncFailed:
            case NcResult.SubKindEnum.Info_SyncSucceeded:
                cancelRefreshTimer ();
                break;
            }
        }

        public void RefreshIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            NachoCore.Utils.NcAbate.HighPriority ("MessageListFragment RefreshIfVisible");
            if (messages.Refresh (out adds, out deletes)) {
                ClearCache ();
                messageListAdapter.NotifyDataSetChanged ();
            }
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListFragment RefreshIfVisible");
            if (0 == messages.Count ()) {
                ((MessageListDelegate)Activity).ListIsEmpty ();
            }
            MaybeDisplayNoMessagesView (View);
        }

        public void RefreshIfNeeded ()
        {
            List<int> adds;
            List<int> deletes;
            NachoCore.Utils.NcAbate.HighPriority ("MessageListFragment RefreshIfNeeded");
            if (NcEmailSingleton.RefreshIfNeeded (messages, out adds, out deletes)) {
                ClearCache ();
                messageListAdapter.NotifyDataSetChanged ();
            } else {
                RefreshVisibleMessageCells ();
            }
            NachoCore.Utils.NcAbate.RegularPriority ("MessageListFragment RefreshIfNeeded");
            if (0 == messages.Count ()) {
                ((MessageListDelegate)Activity).ListIsEmpty ();
            }
            MaybeDisplayNoMessagesView (View);
        }

        int[] first = new int[3];
        List<McEmailMessage>[] cache = new List<McEmailMessage>[3];
        const int CACHEBLOCKSIZE = 32;

        void ClearCache ()
        {
            for (var i = 0; i < first.Length; i++) {
                first [i] = -1;
            }
        }

        public McEmailMessage GetCachedMessage (int i)
        {
            var block = i / CACHEBLOCKSIZE;
            var cacheIndex = block % 3;

            if (block != first [cacheIndex]) {
                MaybeReadBlock (block);
            } else {
                MaybeReadBlock (block - 1);
                MaybeReadBlock (block + 1);
            }

            var index = i % CACHEBLOCKSIZE;
            return cache [cacheIndex] [index];
        }

        void MaybeReadBlock (int block)
        {
            if (0 > block) {
                return;
            }
            var cacheIndex = block % 3;
            if (block == first [cacheIndex]) {
                return;
            }
            var start = block * CACHEBLOCKSIZE;
            var finish = (messages.Count () < (start + CACHEBLOCKSIZE)) ? messages.Count () : start + CACHEBLOCKSIZE;
            var indexList = new List<int> ();
            for (var i = start; i < finish; i++) {
                indexList.Add (messages.GetEmailThread (i).FirstMessageSpecialCaseIndex ());
            }
            cache [cacheIndex] = new List<McEmailMessage> ();
            var resultList = McEmailMessage.QueryForSet (indexList);
            // Reorder the list, add in nulls for missing entries
            foreach (var i in indexList) {
                var result = resultList.Find (x => x.Id == i);
                cache [cacheIndex].Add (result);
            }
            first [cacheIndex] = block;
            // Get portraits
            var fromAddressIdList = new List<int> ();
            foreach (var message in cache[cacheIndex]) {
                if (null != message) {
                    if ((0 != message.FromEmailAddressId) && !fromAddressIdList.Contains (message.FromEmailAddressId)) {
                        fromAddressIdList.Add (message.FromEmailAddressId);
                    }
                }
            }
            // Assign matching portrait ids to email messages
            var portraitIndexList = McContact.QueryForPortraits (fromAddressIdList);
            foreach (var portraitIndex in portraitIndexList) {
                foreach (var message in cache[cacheIndex]) {
                    if (null != message) {
                        if (portraitIndex.EmailAddress == message.FromEmailAddressId) {
                            message.cachedPortraitId = portraitIndex.PortraitId;
                        }
                    }
                }
            }
        }

        protected bool MaybeUpdateMessageInCache (int id)
        {
            foreach (var c in cache) {
                if (null == c) {
                    continue;
                }
                for (int i = 0; i < c.Count; i++) {
                    var m = c [i];
                    if (null != m) {
                        if (m.Id == id) {
                            c [i] = McEmailMessage.QueryById<McEmailMessage> (id);
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        void MaybeDisplayNoMessagesView (View view)
        {
            if (null != view) {
                view.FindViewById<TextView> (Resource.Id.no_messages).Visibility = (0 == messages.Count () ? ViewStates.Visible : ViewStates.Gone);
            }
        }
    }

    public class MessageListAdapter : Android.Widget.BaseAdapter<McEmailMessageThread>, MessageDownloadDelegate
    {
        public const int LISTVIEW_STYLE = 0;
        public const int CARDVIEW_STYLE = 1;

        public int currentStyle;
        MessageListFragment owner;

        bool searching;

        public MessageListAdapter (MessageListFragment owner, int style)
        {
            this.owner = owner;
            currentStyle = style;
        }

        public void StartSearch ()
        {
            searching = true;
            NotifyDataSetChanged ();
        }

        public void CancelSearch ()
        {
            if (searching) {
                searching = false;
                NotifyDataSetChanged ();
            }
        }

        public void RefreshSearchMatches ()
        {
            NotifyDataSetChanged ();
        }

        public override int ViewTypeCount {
            get {
                return 2;
            }
        }

        public override int GetItemViewType (int position)
        {
            return currentStyle;
        }

        public override bool HasStableIds {
            get {
                return true;
            }
        }

        public override long GetItemId (int position)
        {
            if (searching) {
                return owner.searchResultsMessages.GetEmailThread (position).FirstMessageId;
            } else {
                return owner.messages.GetEmailThread (position).FirstMessageId;
            }
        }

        public override int Count {
            get {
                if (searching) {
                    return owner.searchResultsMessages.Count ();
                } else {
                    return owner.messages.Count ();
                }
            }
        }

        public override McEmailMessageThread this [int position] {  
            get { 
                if (searching) {
                    return owner.searchResultsMessages.GetEmailThread (position);
                } else {
                    return owner.messages.GetEmailThread (position);
                }
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            switch (currentStyle) {
            case LISTVIEW_STYLE:
                return GetListView (position, convertView, parent);
            case CARDVIEW_STYLE:
                return GetCardView (position, convertView, parent);
            default:
                return null;
            }
        }

        View GetListView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCell, parent, false);
                var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
                chiliView.Click += ChiliView_Click;
            }
            McEmailMessageThread thread;
            McEmailMessage message;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
                message = thread.FirstMessageSpecialCase ();
            } else {
                thread = owner.messages.GetEmailThread (position);
                message = owner.GetCachedMessage (position);
            }
            var isDraft = owner.messages.HasDraftsSemantics () || owner.messages.HasOutboxSemantics ();
            Bind.BindMessageHeader (thread, message, view, isDraft);

            NcBrain.MessageNotificationStatusUpdated (message, DateTime.UtcNow, 60);

            // Preview label view                
            var previewView = view.FindViewById<Android.Widget.TextView> (Resource.Id.preview);
            if (null == message) {
                previewView.Text = "";
            } else {
                var cookedPreview = EmailHelper.AdjustPreviewText (message.GetBodyPreviewOrEmpty ());
                previewView.SetText (Android.Text.Html.FromHtml (cookedPreview), Android.Widget.TextView.BufferType.Spannable);
            }

            var multiSelectView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.selected);
            if (owner.multiSelectActive) {
                multiSelectView.Visibility = ViewStates.Visible;
                if (owner.MultiSelectSet.Contains (position)) {
                    multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                } else {
                    multiSelectView.SetImageResource (Resource.Drawable.gen_checkbox);
                }
            } else {
                multiSelectView.Visibility = ViewStates.Invisible;
            }

            var chiliTagView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
            chiliTagView.Tag = position;

            return view;
        }

        void MessageFromSender (object sender, out McEmailMessageThread thread, out McEmailMessage message)
        {
            var view = (View)sender;
            var position = (int)view.Tag;
            if (searching) {
                thread = owner.searchResultsMessages.GetEmailThread (position);
            } else {
                thread = owner.messages.GetEmailThread (position);
            }
            message = thread.FirstMessageSpecialCase ();
        }

        void ChiliView_Click (object sender, EventArgs e)
        {
            McEmailMessage message;
            McEmailMessageThread thread;
            var chiliView = (Android.Widget.ImageView)sender;
            MessageFromSender (sender, out thread, out message);
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, chiliView);
        }

        class MessageDownloaderWithWebView : MessageDownloader
        {
            public WebView webView;
        }

        View GetCardView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageCard, parent, false);
                var chiliView = view.FindViewById<Android.Widget.ImageView> (Resource.Id.chili);
                AttachListeners (view);
                chiliView.Click += ChiliView_Click;
            }
                
            var thread = owner.messages.GetEmailThread (position);
            var message = owner.GetCachedMessage (position);

            var webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
 
            webView.Clickable = false;
            webView.LongClickable = false;
            webView.Focusable = false;
            webView.FocusableInTouchMode = false;
            webView.SetOnTouchListener (new IgnoreTouchListener ());

            BindValues (view, thread, message);

            view.FindViewById (Resource.Id.chili).Tag = position;
            view.FindViewById (Resource.Id.reply).Tag = position;
            view.FindViewById (Resource.Id.reply_all).Tag = position;
            view.FindViewById (Resource.Id.forward).Tag = position;
            view.FindViewById (Resource.Id.archive).Tag = position;
            view.FindViewById (Resource.Id.delete).Tag = position;

            view.SetMinimumHeight (parent.MeasuredHeight);
            view.LayoutParameters.Height = parent.MeasuredHeight;
            return view;
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            public bool OnTouch (View v, MotionEvent e)
            {
                return false;
            }
        }

        void AttachListeners (View view)
        {
            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click += ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click += ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click += ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click += ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click += DeleteButton_Click;
        }

        void DetachListeners (View view)
        {
            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click -= ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click -= ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click -= ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click -= ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click -= DeleteButton_Click;
        }

        void BindValues (View view, McEmailMessageThread thread, McEmailMessage message)
        {
            Bind.BindMessageHeader (thread, message, view);
            view.FindViewById<TextView> (Resource.Id.subject).SetMaxLines (100);
            BindMeetingRequest (view, message);
            var webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            NcEmailMessageBundle bundle;
            if (message.BodyId != 0) {
                bundle = new NcEmailMessageBundle (message);
            } else {
                bundle = null;
            }
            if (bundle == null || bundle.NeedsUpdate) {
                var messageDownloader = new MessageDownloaderWithWebView ();
                messageDownloader.webView = webView;
                messageDownloader.Bundle = bundle;
                messageDownloader.Delegate = this;
                messageDownloader.Download (message);
            } else {
                RenderBody (webView, bundle);
            }
        }

        void BindMeetingRequest (View view, McEmailMessage message)
        {
            var meeting = message.MeetingRequest;

            if (null == meeting) {
                view.FindViewById<View> (Resource.Id.event_in_message).Visibility = ViewStates.Gone;
                return;
            }

            var calendarItem = McCalendar.QueryByUID (message.AccountId, meeting.GetUID ());

            var whenView = view.FindViewById<TextView> (Resource.Id.event_when_label);
            whenView.Text = NcEventDetail.GetDateString (meeting);

            var durationView = view.FindViewById<TextView> (Resource.Id.event_duration_label);
            durationView.Text = NcEventDetail.GetDurationString (meeting);

            var recurrenceView = view.FindViewById<TextView> (Resource.Id.event_recurrence_label);
            if (0 == meeting.recurrences.Count) {
                recurrenceView.Visibility = ViewStates.Gone;
            } else {
                recurrenceView.Text = NcEventDetail.GetRecurrenceString (meeting);
            }

            string location = meeting.GetLocation ();
            if (string.IsNullOrEmpty (location)) {
                view.FindViewById<View> (Resource.Id.location_view).Visibility = ViewStates.Gone;
            } else {
                view.FindViewById<TextView> (Resource.Id.event_location_label).Text = location;
            }

            var organizerAddress = NcEmailAddress.ParseMailboxAddressString (meeting.Organizer);
            if (!message.IsMeetingResponse && null != organizerAddress && null != organizerAddress.Address) {
                string email = organizerAddress.Address;
                string name = organizerAddress.Name;
                var organizerEmailLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_email_label);
                organizerEmailLabel.Text = email;
                if (string.IsNullOrEmpty (name)) {
                    foreach (var contact in McContact.QueryByEmailAddress (meeting.AccountId, email)) {
                        if (!string.IsNullOrEmpty (contact.DisplayName)) {
                            name = contact.DisplayName;
                            break;
                        }
                    }
                }
                string initials;
                if (!string.IsNullOrEmpty (name)) {
                    var organizerNameLabel = view.FindViewById<TextView> (Resource.Id.event_organizer_label);
                    organizerNameLabel.Text = name;
                    initials = ContactsHelper.NameToLetters (name);
                } else {
                    initials = ContactsHelper.NameToLetters (email);
                }
                var color = Util.ColorResourceForEmail (email);
                var imageView = view.FindViewById<ContactPhotoView> (Resource.Id.event_organizer_initials);
                imageView.SetEmailAddress (meeting.AccountId, email, initials, color);
            } else {
                view.FindViewById<View> (Resource.Id.event_organizer_view).Visibility = ViewStates.Gone;
            }

            if (!message.IsMeetingRequest) {
                view.FindViewById<View> (Resource.Id.event_attendee_view).Visibility = ViewStates.Gone;
            } else {
                var attendeesFromMessage = NcEmailAddress.ParseAddressListString (Pretty.Join (message.To, message.Cc, ", "));
                for (int a = 0; a < 5; ++a) {
                    var attendeePhotoView = AttendeeInitialsView (view, a);
                    var attendeeNameView = AttendeeNameView (view, a);
                    if (4 == a && 5 < attendeesFromMessage.Count) {
                        attendeePhotoView.SetPortraitId (0, string.Format ("+{0}", attendeesFromMessage.Count - a), Resource.Drawable.UserColor0);
                        attendeeNameView.Text = "";
                    } else if (a < attendeesFromMessage.Count) {
                        var attendee = attendeesFromMessage [a] as MimeKit.MailboxAddress;
                        var initials = ContactsHelper.NameToLetters (attendee.Name);
                        var color = Util.ColorResourceForEmail (attendee.Address);
                        attendeePhotoView.SetEmailAddress (message.AccountId, attendee.Address, initials, color);
                        attendeeNameView.Text = GetFirstName (attendee.Name);
                    } else {
                        attendeePhotoView.Visibility = ViewStates.Gone;
                        attendeeNameView.Visibility = ViewStates.Gone;
                    }
                }
            }

            // The Hot view cards never use the Attend/Maybe/Decline buttons.  They always use the message instead.
            view.FindViewById (Resource.Id.event_rsvp_view).Visibility = ViewStates.Gone;
            view.FindViewById (Resource.Id.event_message_view).Visibility = ViewStates.Visible;
            if (message.IsMeetingResponse) {
                ShowAttendeeResponseBar (view, message);
            } else if (message.IsMeetingCancelation) {
                ShowCancellationBar (view);
            } else {
                ShowRequestChoicesBar (view, calendarItem);
            }
        }

        void ShowRequestChoicesBar (View view, McCalendar calendarItem)
        {
            var iconView = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            var textView = view.FindViewById<TextView> (Resource.Id.event_message_text);

            NcResponseType status = NcResponseType.None;
            if (null != calendarItem && calendarItem.ResponseTypeIsSet) {
                status = calendarItem.ResponseType;
            }
            switch (status) {
            case NcResponseType.Accepted:
                iconView.SetImageResource (Resource.Drawable.event_attend_active);
                textView.Text = "You accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                iconView.SetImageResource (Resource.Drawable.event_maybe_active);
                textView.Text = "You tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                iconView.SetImageResource (Resource.Drawable.event_decline_active);
                textView.Text = "You declined the meeting.";
                break;
            default:
                iconView.Visibility = ViewStates.Gone;
                textView.Text = "You have not yet responded to the meeting.";
                break;
            }
        }

        void ShowCancellationBar (View view)
        {
            var iconView = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            var textView = view.FindViewById<TextView> (Resource.Id.event_message_text);

            iconView.Visibility = ViewStates.Gone;
            textView.Text = "The meeting has been canceled.";
        }

        void ShowAttendeeResponseBar (View view, McEmailMessage message)
        {
            int iconResourceId;
            string messageFormat;
            switch (message.MeetingResponseValue) {
            case NcResponseType.Accepted:
                iconResourceId = Resource.Drawable.event_attend_active;
                messageFormat = "{0} accepted the meeting.";
                break;
            case NcResponseType.Tentative:
                iconResourceId = Resource.Drawable.event_maybe_active;
                messageFormat = "{0} tentatively accepted the meeting.";
                break;
            case NcResponseType.Declined:
                iconResourceId = Resource.Drawable.event_decline_active;
                messageFormat = "{0} declined the meeting.";
                break;
            default:
                Log.Warn (Log.LOG_CALENDAR, "Unknown meeting response status: {0}", message.MessageClass);
                iconResourceId = 0;
                messageFormat = "The status of {0} is unknown.";
                break;
            }

            string displayName;
            var responder = NcEmailAddress.ParseMailboxAddressString (message.From);
            if (null == responder) {
                displayName = message.From;
            } else if (!string.IsNullOrEmpty (responder.Name)) {
                displayName = responder.Name;
            } else {
                displayName = responder.Address;
            }

            var icon = view.FindViewById<ImageView> (Resource.Id.event_message_icon);
            if (0 == iconResourceId) {
                icon.Visibility = ViewStates.Gone;
            } else {
                icon.SetImageResource (iconResourceId);
            }
            var text = view.FindViewById<TextView> (Resource.Id.event_message_text);
            text.Text = string.Format (messageFormat, displayName);
        }

        private ContactPhotoView AttendeeInitialsView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<ContactPhotoView> (id);
        }

        private TextView AttendeeNameView (View parent, int attendeeIndex)
        {
            int id;
            switch (attendeeIndex) {
            case 0:
                id = Resource.Id.event_attendee_name_0;
                break;
            case 1:
                id = Resource.Id.event_attendee_name_1;
                break;
            case 2:
                id = Resource.Id.event_attendee_name_2;
                break;
            case 3:
                id = Resource.Id.event_attendee_name_3;
                break;
            case 4:
                id = Resource.Id.event_attendee_name_4;
                break;
            default:
                NcAssert.CaseError (string.Format ("Attendee index {0} is out of range. It must be [0..4]", attendeeIndex));
                return null;
            }
            return parent.FindViewById<TextView> (id);
        }

        private static string GetFirstName (string displayName)
        {
            string[] names = displayName.Split (new char [] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (names [0] == null) {
                return "";
            }
            if (names [0].Length > 1) {
                return char.ToUpper (names [0] [0]) + names [0].Substring (1);
            }
            return names [0].ToUpper ();
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            var bundle = downloader.Bundle;
            var downloaderWithWebView = (MessageDownloaderWithWebView)downloader;
            RenderBody (downloaderWithWebView.webView, bundle);
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: show this inline, possibly with message preview (if available)
        }

        void RenderBody (Android.Webkit.WebView webView, NcEmailMessageBundle bundle)
        {
            if (bundle != null) {
                if (bundle.FullHtmlUrl != null) {
                    webView.LoadUrl (bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = bundle.FullHtml;
                    webView.LoadDataWithBaseURL (bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
        }

        void DoneWithMessage ()
        {
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            NcEmailArchiver.Archive (message);
            DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            NcEmailArchiver.Delete (message);
            DoneWithMessage ();
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ForwardButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.Forward, thread, message);
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.Reply, thread, message);
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyAllButton_Click");
            McEmailMessage message;
            McEmailMessageThread thread;
            MessageFromSender (sender, out thread, out message);
            StartComposeActivity (EmailHelper.Action.ReplyAll, thread, message);
        }

        void StartComposeActivity (EmailHelper.Action action, McEmailMessageThread thread, McEmailMessage message)
        {
            var activity = owner.Activity;
            owner.StartActivity (MessageComposeActivity.RespondIntent (activity, action, thread.FirstMessageId));
        }

    }

}

