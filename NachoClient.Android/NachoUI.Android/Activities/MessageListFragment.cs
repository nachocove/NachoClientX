
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

        LinearLayoutManager layoutManager;
        SwipeMenuRecyclerView recyclerView;
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

            recyclerView = view.FindViewById<SwipeMenuRecyclerView> (Resource.Id.recyclerView);

            layoutManager = new LinearLayoutManager (Activity);
            recyclerView.SetLayoutManager (layoutManager);

            ClearCache ();
            SetupMessageListAdapter (view);

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            searchResultsMessages = new NachoMessageSearchResults (NcApplication.Instance.Account.Id);

            searcher = new SearchHelper ("MessageListViewController", (searchString) => {
                if (String.IsNullOrEmpty (searchString)) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResultsMessages.UpdateMatches (null);
                        searchResultsMessages.UpdateServerMatches (null);
                        messageListAdapter.RefreshSearchMatches ();
                    });
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
                    lateItem.setBackground (A.Drawable_NachoSwipeCalendarLate (Activity));
                    lateItem.setWidth (dp2px (90));
                    lateItem.setTitle ("I'm Late");
                    lateItem.setTitleSize (14);
                    lateItem.setTitleColor (A.Color_White);
                    lateItem.setIcon (A.Id_NachoSwipeCalendarLate);
                    lateItem.setId (LATE_TAG);
                    menu.addMenuItem (lateItem, SwipeMenu.SwipeSide.LEFT);

                    SwipeMenuItem forwardItem = new SwipeMenuItem (Activity.ApplicationContext);
                    forwardItem.setBackground (A.Drawable_NachoSwipeCalendarForward (Activity));
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
            messageListAdapter.onMessageClick += MessageListAdapter_OnMessageClick;

            if (MessageListAdapter.CARDVIEW_STYLE == parent.ShowListStyle ()) {
                recyclerView.AddOnScrollListener (new MessageListScrollListener ());
            }

            recyclerView.SetAdapter (messageListAdapter);

            recyclerView.setMenuCreator ((menu) => {
                if (MessageListAdapter.SUMMARY_STYLE == menu.getViewType ()) {
                    return;
                }
                if (!(messages.HasDraftsSemantics () || messages.HasOutboxSemantics ())) {
                    SwipeMenuItem deferItem = new SwipeMenuItem (Activity.ApplicationContext);
                    deferItem.setBackground (A.Drawable_NachoSwipeEmailDefer (Activity));
                    deferItem.setWidth (dp2px (90));
                    deferItem.setTitle ("Defer");
                    deferItem.setTitleSize (14);
                    deferItem.setTitleColor (A.Color_White);
                    deferItem.setIcon (A.Id_NachoSwipeEmailDefer);
                    deferItem.setId (DEFER_TAG);
                    menu.addMenuItem (deferItem, SwipeMenu.SwipeSide.LEFT);
                    SwipeMenuItem moveItem = new SwipeMenuItem (Activity.ApplicationContext);
                    moveItem.setBackground (A.Drawable_NachoSwipeEmailMove (Activity));
                    moveItem.setWidth (dp2px (90));
                    moveItem.setTitle ("Move");
                    moveItem.setTitleSize (14);
                    moveItem.setTitleColor (A.Color_White);
                    moveItem.setIcon (A.Id_NachoSwipeEmailMove);
                    moveItem.setId (SAVE_TAG);
                    menu.addMenuItem (moveItem, SwipeMenu.SwipeSide.LEFT);
                    SwipeMenuItem archiveItem = new SwipeMenuItem (Activity.ApplicationContext);
                    archiveItem.setBackground (A.Drawable_NachoSwipeEmailArchive (Activity));
                    archiveItem.setWidth (dp2px (90));
                    archiveItem.setTitle ("Archive");
                    archiveItem.setTitleSize (14);
                    archiveItem.setTitleColor (A.Color_White);
                    archiveItem.setIcon (A.Id_NachoSwipeEmailArchive);
                    archiveItem.setId (ARCHIVE_TAG);
                    menu.addMenuItem (archiveItem, SwipeMenu.SwipeSide.RIGHT);
                }
                SwipeMenuItem deleteItem = new SwipeMenuItem (Activity.ApplicationContext);
                deleteItem.setBackground (A.Drawable_NachoSwipeEmailDelete (Activity));
                deleteItem.setWidth (dp2px (90));
                deleteItem.setTitle ("Delete");
                deleteItem.setTitleSize (14);
                deleteItem.setTitleColor (A.Color_White);
                deleteItem.setIcon (A.Id_NachoSwipeEmailDelete);
                deleteItem.setId (DELETE_TAG);
                menu.addMenuItem (deleteItem, SwipeMenu.SwipeSide.RIGHT);
            }
            );

            recyclerView.setOnMenuItemClickListener (( position, menu, index) => {
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

            recyclerView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            recyclerView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });
        }

        void SwipeRefreshLayout_Refresh (object sender, EventArgs e)
        {
            var nr = messages.StartSync ();
            rearmRefreshTimer (NachoSyncResult.DoesNotSync (nr) ? 3 : 10);
        }

        class MessageListScrollListener : RecyclerView.OnScrollListener
        {
            // Positive means pushing up
            int lastDy;
            bool userInitiated;

            public override void OnScrollStateChanged (RecyclerView recyclerView, int newState)
            {
                var swipeMenuRecyclerView = (SwipeMenuRecyclerView)recyclerView;
                switch (newState) {
                case RecyclerView.ScrollStateDragging:
                case RecyclerView.ScrollStateSettling:
                    swipeMenuRecyclerView.EnableSwipe (false);
                    userInitiated |= (RecyclerView.ScrollStateDragging == newState);
                    if (!NcApplication.Instance.IsBackgroundAbateRequired) {
                        NachoCore.Utils.NcAbate.HighPriority ("MessageListFragment ScrollStateChanged");
                    }
                    break;
                case RecyclerView.ScrollStateIdle:
                    swipeMenuRecyclerView.EnableSwipe (true);
                    if (NcApplication.Instance.IsBackgroundAbateRequired) {
                        NachoCore.Utils.NcAbate.RegularPriority ("MessageListFragment ScrollStateChanged");
                    }
                    break;
                }
                if ((RecyclerView.ScrollStateSettling != newState) && (RecyclerView.ScrollStateIdle != newState)) {
                    return;
                }
                if (!userInitiated) {
                    return; // Prevent jitter
                }
                userInitiated = false;

                var adapter = (WrapperRecyclerAdapter)swipeMenuRecyclerView.GetAdapter ();
                var messageListAdapter = (MessageListAdapter)adapter.GetWrappedAdapter ();
                if (MessageListAdapter.CARDVIEW_STYLE != messageListAdapter.currentStyle) {
                    return;
                }
                var layoutManager = (LinearLayoutManager)swipeMenuRecyclerView.GetLayoutManager ();

                int scrollToPosition;

                if (0 < lastDy) {
                    scrollToPosition = layoutManager.FindLastVisibleItemPosition ();
                } else {
                    scrollToPosition = layoutManager.FindFirstVisibleItemPosition ();
                }
                if (RecyclerView.NoPosition != scrollToPosition) {
                    recyclerView.SmoothScrollToPosition (scrollToPosition);
                }
            }

            public override void OnScrolled (RecyclerView recyclerView, int dx, int dy)
            {
                lastDy = dy;
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

        void MessageListAdapter_OnMessageClick (object sender, int position)
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
            if (multiSelectActive) {
                if (MultiSelectSet.Contains (position)) {
                    MultiSelectSet.Remove (position);
                } else {
                    MultiSelectSet.Add (position);
                }
                RefreshVisibleMessageCells ();
                return;
            }

            var thread = messageListAdapter [position];

            if (1 == thread.MessageCount) {
                if (null != onMessageClick) {
                    onMessageClick (this, thread);
                }
            } else {
                var threadMessages = messages.GetAdapterForThread (thread);
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
                StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity, NcApplication.Instance.EffectiveEmailAccount.Id));
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
                recyclerView.EnableSwipe (false);
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
                recyclerView.EnableSwipe (true);
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
            searchResultsMessages.UpdateMatches (null);
            searchResultsMessages.UpdateServerMatches (null);

            messageListAdapter.StartSearch ();
            messageListAdapter.RefreshSearchMatches ();

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
            if (searching) {
                var searchString = searchEditText.Text;
                if (String.IsNullOrEmpty (searchString)) {
                    searchResultsMessages.UpdateServerMatches (null);
                    messageListAdapter.RefreshSearchMatches ();
                } else if (4 > searchString.Length) {
                    KickoffSearchApi (0, searchString);
                }
                searcher.Search (searchString);
            }
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
            var folderFragment = ChooseFolderFragment.newInstance (messageThread.FirstMessage().AccountId, messageThread);
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
            if (null != recyclerView) {
                SetupMessageListAdapter (View);
            }
            MaybeDisplayNoMessagesView (View);
        }

        void RefreshVisibleMessageCells ()
        {
            ClearCache ();
            var a = layoutManager.FindFirstVisibleItemPosition ();
            if (RecyclerView.NoPosition != a) {
                var z = layoutManager.FindLastVisibleItemPosition ();
                messageListAdapter.NotifyItemRangeChanged (a, 1 + z - a);
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
            if (!NcApplication.Instance.Account.ContainsAccount(s.Account.Id)) {
                return;
            }

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageChanged:
            case NcResult.SubKindEnum.Info_SystemTimeZoneChanged:
                RefreshVisibleMessageCells ();
                break;
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageScoreUpdated:
            case NcResult.SubKindEnum.Info_EmailMessageSetFlagSucceeded:
            case NcResult.SubKindEnum.Info_EmailMessageClearFlagSucceeded:
                RefreshIfVisible ();
                RefreshVisibleMessageCells ();
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

        void NotifyChanges (List<int> adds, List<int> deletes)
        {
            ClearCache ();
            messageListAdapter.NotifyDataSetChanged ();

//            if (null == adds && null == deletes) {
//                messageListAdapter.NotifyDataSetChanged ();
//                return;
//            }
//            NcAssert.False (null != adds && null != deletes);
//
//            var list = (null == adds) ? deletes : adds;
//            var firstIndex = list [0];
//
//            list.Sort ();
//            list.Reverse ();
//
//            foreach (var position in list) {
//                if (null == adds) {
//                    messageListAdapter.NotifyItemRemoved (position);
//                } else {
//                    messageListAdapter.NotifyItemInserted (position);
//                }
//            }
//            // Force re-bind to update menu swipe 'position' field
//            var range = messageListAdapter.ItemCount - firstIndex;
//            messageListAdapter.NotifyItemRangeChanged (firstIndex, range);
        }

        public void RefreshIfVisible ()
        {
            List<int> adds;
            List<int> deletes;
            NachoCore.Utils.NcAbate.HighPriority ("MessageListFragment RefreshIfVisible");
            if (messages.Refresh (out adds, out deletes)) {
                NotifyChanges (adds, deletes);
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
                NotifyChanges (adds, deletes);
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
                if (MessageListAdapter.LISTVIEW_STYLE == messageListAdapter.currentStyle) {
                    view.FindViewById<TextView> (Resource.Id.no_messages).Visibility = (0 == messages.Count () ? ViewStates.Visible : ViewStates.Gone);
                }
            }
        }
    }
}

