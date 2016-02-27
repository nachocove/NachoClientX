
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
using NachoCore.Index;
using Android.Graphics;

namespace NachoClient.AndroidClient
{
    public interface MessageListDelegate
    {
        void ListIsEmpty ();

        bool ShowHotEvent ();

        int ShowListStyle ();

        void SetActiveImage (View view);
    }

    public class MessageListFragment : Fragment, Android.Widget.PopupMenu.IOnMenuItemClickListener
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
        EditText searchEditText;

        INachoEmailMessages messages;
        EmailSearch emailSearcher;

        public bool multiSelectActive = false;
        public HashSet<long> MultiSelectSet = null;
        public Dictionary<int, int> MultiSelectAccounts = null;

        ImageView leftButton1;
        ImageView leftButton2;
        ImageView rightButton1;
        ImageView rightButton2;
        ImageView rightButton3;

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

        public INachoEmailMessages CurrentMessages {
            get {
                return searching ? emailSearcher : messages;
            }
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

            leftButton1 = view.FindViewById<ImageView> (Resource.Id.left_button1);
            leftButton1.Click += LeftButton1_Click;

            leftButton2 = view.FindViewById<ImageView> (Resource.Id.left_button2);
            leftButton2.Click += LeftButton2_Click;

            rightButton1 = view.FindViewById<ImageView> (Resource.Id.right_button1);
            rightButton1.Click += RightButton1_Click;

            rightButton2 = view.FindViewById<ImageView> (Resource.Id.right_button2);
            rightButton2.Click += RightButton2_Click;

            rightButton3 = view.FindViewById<ImageView> (Resource.Id.right_button3);
            rightButton3.Click += RightButton3_Click;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            recyclerView = view.FindViewById<SwipeMenuRecyclerView> (Resource.Id.recyclerView);

            layoutManager = new LinearLayoutManager (Activity);
            recyclerView.SetLayoutManager (layoutManager);

            ClearCache ();
            SetupMessageListAdapter (view);

            searchEditText = view.FindViewById<EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;
            searchEditText.EditorAction += SearchString_Enter;

            emailSearcher = new EmailSearch ((string searchString, List<McEmailMessageThread> results) => {
                messageListAdapter.RefreshSearchMatches ();
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
                            var outgoingMessage = McEmailMessage.MessageWithSubject (NcApplication.Instance.DefaultEmailAccount, "Re: " + cal.GetSubject ());
                            outgoingMessage.To = cal.OrganizerEmail;
                            StartActivity (MessageComposeActivity.InitialTextIntent (this.Activity, outgoingMessage, "Running late."));
                        }
                        break;
                    case FORWARD_TAG:
                        if (null != cal) {
                            StartActivity (MessageComposeActivity.ForwardCalendarIntent (
                                this.Activity, cal.Id, McEmailMessage.MessageWithSubject (NcApplication.Instance.DefaultEmailAccount, "Fwd: " + cal.GetSubject ())));
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

            if (messages.HasFilterSemantics ()) {
                view.FindViewById<View> (Resource.Id.filter_setting_header).Visibility = ViewStates.Visible;
                view.FindViewById<TextView> (Resource.Id.filter_setting).Text = Folder_Helpers.FilterString (messages.FilterSetting);
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

            recyclerView.AddOnScrollListener (new MessageListScrollListener (() => {
                SendFetchHints ();
            }));

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
            Action OnStop;
            IDisposable abatementRequest = null;

            public MessageListScrollListener (Action OnStop) : base ()
            {
                this.OnStop = OnStop;
            }

            public override void OnScrollStateChanged (RecyclerView recyclerView, int newState)
            {
                var swipeMenuRecyclerView = (SwipeMenuRecyclerView)recyclerView;
                switch (newState) {
                case RecyclerView.ScrollStateDragging:
                case RecyclerView.ScrollStateSettling:
                    swipeMenuRecyclerView.EnableSwipe (false);
                    userInitiated |= (RecyclerView.ScrollStateDragging == newState);
                    if (null == abatementRequest) {
                        abatementRequest = NcAbate.UITimedAbatement (TimeSpan.FromSeconds (10));
                    }
                    break;
                case RecyclerView.ScrollStateIdle:
                    swipeMenuRecyclerView.EnableSwipe (true);
                    if (null != OnStop) {
                        OnStop ();
                    }
                    if (null != abatementRequest) {
                        abatementRequest.Dispose ();
                        abatementRequest = null;
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

        void SendFetchHints ()
        {
            if (0 == messages.Count ()) {
                return;
            }

            var a = layoutManager.FindFirstVisibleItemPosition ();
            if (RecyclerView.NoPosition == a) {
                return;
            }
            var z = layoutManager.FindLastVisibleItemPosition ();

            var Ids = new List<Tuple<int,int>> ();

            for (var i = a; i <= z; i++) {
                if (i < messages.Count ()) { // don't fetch footer
                    var message = GetCachedMessage (i);
                    if ((null != message) && (0 == message.BodyId)) {
                        Ids.Add (new Tuple<int, int> (message.AccountId, message.Id));
                    }
                }
            }
            if (0 < Ids.Count) {
                NcTask.Run (() => {
                    BackEnd.Instance.SendEmailBodyFetchHints (Ids);
                }, "SendEmailBodyFetchHints");
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
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
            if (searching) {
                CancelSearch ();
            }
        }

        void SetFilterText (View parentView, FolderFilterOptions filterSetting)
        {
            string text;
            switch (filterSetting) {
            case FolderFilterOptions.All:
                text = "All messages";
                break;
            case FolderFilterOptions.Hot:
                text = "Hot messages";
                break;
            case FolderFilterOptions.Focused:
                text = "Focused messages";
                break;
            case FolderFilterOptions.Unread:
                text = "Unread messages";
                break;
            default:
                text = "Unknown set of messages";
                break;
            }
            parentView.FindViewById<TextView> (Resource.Id.filter_setting).Text = text;
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
                    UpdateMultiSelectAccounts (position, -1);
                } else {
                    MultiSelectSet.Add (position);
                    UpdateMultiSelectAccounts (position, 1);
                }
                RefreshVisibleMessageCells ();
                ConfigureButtons ();
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

        void LeftButton2_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "LeftButton2_Click");

            var view = View.FindViewById (Resource.Id.left_button2);
            var popup = new Android.Widget.PopupMenu (Activity, view);
            popup.SetOnMenuItemClickListener (this);

            popup.Menu.Add (1, 0, 0, "Message Filter");

            var values = messages.PossibleFilterSettings;
            for (int i = 0; i < values.Length; ++i) {
                var item = popup.Menu.Add (0, (int)values [i], i, Folder_Helpers.FilterShortString (values [i]));
                if (messages.FilterSetting == values [i]) {
                    item.SetChecked (true);
                }
            }
            popup.Menu.SetGroupCheckable (0, true, true);
            popup.Show ();
        }

        bool Android.Widget.PopupMenu.IOnMenuItemClickListener.OnMenuItemClick (IMenuItem item)
        {
            if (1 == item.GroupId) {
                // Ignore "Message Filter"
                return true;
            }
            var newFilterSetting = (FolderFilterOptions)item.ItemId;
            messages.FilterSetting = newFilterSetting;
            View.FindViewById<TextView> (Resource.Id.filter_setting).Text = Folder_Helpers.FilterString (newFilterSetting);
            RefreshIfVisible ();
            return true;
        }

        // Compose or delete (multi-select)
        void RightButton1_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                MultiSelectDelete ();
            } else {
                var defaultEmailAccount = NcApplication.Instance.DefaultEmailAccount;
                if (null != defaultEmailAccount) {
                    StartActivity (MessageComposeActivity.NewMessageIntent (this.Activity, defaultEmailAccount.Id));
                }
            }
        }

        // Enable multi-select or save to folder (multi-select)
        void RightButton2_Click (object sender, EventArgs e)
        {
            if (multiSelectActive) {
                ShowFolderChooser (null);
            } else {
                MultiSelectSet = new HashSet<long> ();
                MultiSelectAccounts = new Dictionary<int, int> ();
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

        void SetButtonsToDefault ()
        {
            leftButton1.Visibility = ViewStates.Invisible;
            leftButton2.Visibility = ViewStates.Invisible;
            rightButton1.Visibility = ViewStates.Invisible;
            rightButton2.Visibility = ViewStates.Invisible;
            rightButton3.Visibility = ViewStates.Invisible;
            leftButton1.Enabled = false;
            leftButton2.Enabled = false;
            rightButton1.Enabled = false;
            rightButton2.Enabled = false;
            rightButton3.Enabled = false;
        }

        void SetButtonVisibility (ImageView view, bool enabled)
        {
            view.Enabled = enabled;
            view.Visibility = ViewStates.Visible;
            if (enabled) {
                view.SetColorFilter (null);
            } else {
                view.SetColorFilter (Color.Argb (200, 220, 220, 220));
            }
        }


        void ConfigureButtons ()
        {
            SetButtonsToDefault ();

            if (multiSelectActive) {
                var count = MultiSelectSet.Count;
                recyclerView.EnableSwipe (false);
                if (messages.HasDraftsSemantics () || messages.HasOutboxSemantics ()) {
                    leftButton1.SetImageResource (Resource.Drawable.gen_close);
                    rightButton1.SetImageResource (Resource.Drawable.gen_delete_all);
                    SetButtonVisibility (leftButton1, true);
                    SetButtonVisibility (rightButton1, 0 != count);
                } else {
                    leftButton1.SetImageResource (Resource.Drawable.gen_close);
                    rightButton1.SetImageResource (Resource.Drawable.gen_delete_all);
                    rightButton2.SetImageResource (Resource.Drawable.folder_move);
                    rightButton3.SetImageResource (Resource.Drawable.gen_archive);
                    SetButtonVisibility (leftButton1, true);
                    SetButtonVisibility (rightButton1, 0 != count);
                    SetButtonVisibility (rightButton2, (0 != count) && (1 == MultiSelectAccounts.Count));
                    SetButtonVisibility (rightButton3, 0 != count);
                }
            } else {
                recyclerView.EnableSwipe (true);
                leftButton1.SetImageResource (Resource.Drawable.nav_search);
                SetButtonVisibility (leftButton1, true);
                if (messages.HasFilterSemantics ()) {
                    leftButton2.SetImageResource (Resource.Drawable.gen_read_list);
                    SetButtonVisibility (leftButton2, true);
                    ;
                }
                rightButton1.SetImageResource (Resource.Drawable.contact_newemail);
                rightButton2.SetImageResource (Resource.Drawable.folder_edit);
                SetButtonVisibility (rightButton1, true);
                SetButtonVisibility (rightButton2, true);
            }
            RefreshVisibleMessageCells ();
        }

        void MultiSelectCancel ()
        {
            multiSelectActive = false;
            MultiSelectSet = null;
            MultiSelectAccounts = null;
            ConfigureButtons ();
        }

        void UpdateMultiSelectAccounts (int position, int delta)
        {
            var message = GetCachedMessage (position);
            if (null == message) {
                return;
            }
            int value;
            if (MultiSelectAccounts.TryGetValue (message.AccountId, out value)) {
                value += delta;
                if (0 == value) {
                    MultiSelectAccounts.Remove (message.AccountId);
                }
            } else {
                NcAssert.True (1 == delta);
                MultiSelectAccounts.Add (message.AccountId, delta);
            }
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

            emailSearcher.EnterSearchMode (NcApplication.Instance.Account);

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

            emailSearcher.ExitSearchMode ();

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

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            if (searching) {
                emailSearcher.SearchFor (searchEditText.Text);
            }
        }

        void SearchString_Enter (object sender, TextView.EditorActionEventArgs e)
        {
            if (searching) {
                emailSearcher.StartServerSearch ();
            }
        }

        public bool HandleBackButton ()
        {
            if (searching) {
                CancelSearch ();
                return true;
            }
            return false;
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
            NcAssert.True (1 == MultiSelectAccounts.Count);
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
            int accountId;
            if (null == messageThread) {
                Log.Info (Log.LOG_UI, "ShowFolderChooser: {0}", messageThread);
                var messageList = GetSelectedMessages ();
                HashSet<int> accountIds = new HashSet<int> ();
                foreach (var message in messageList) {
                    accountIds.Add (message.AccountId);
                }
                if (1 != accountIds.Count) {
                    NcAlertView.ShowMessage (this.Activity, "Not yet implemented", "You cannot multi-file from different accounts");
                    return;
                }
                accountId = accountIds.First ();
            } else {
                accountId = messageThread.FirstMessage ().AccountId;
            }
            var folderFragment = ChooseFolderFragment.newInstance (accountId, messageThread);
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
            if ((null != messageListAdapter) && (style != messageListAdapter.currentStyle)) {
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
            if (!NcApplication.Instance.Account.ContainsAccount (s.Account.Id)) {
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
            using (NcAbate.UIAbatement ()) {
                List<int> adds;
                List<int> deletes;
                if (messages.Refresh (out adds, out deletes)) {
                    NotifyChanges (adds, deletes);
                }
                if (0 == messages.Count ()) {
                    ((MessageListDelegate)Activity).ListIsEmpty ();
                }
                MaybeDisplayNoMessagesView (View);
            }
        }

        public void RefreshIfNeeded ()
        {
            using (NcAbate.UIAbatement ()) {
                List<int> adds;
                List<int> deletes;
                if (NcEmailSingleton.RefreshIfNeeded (messages, out adds, out deletes)) {
                    NotifyChanges (adds, deletes);
                } else {
                    RefreshVisibleMessageCells ();
                }
                if (0 == messages.Count ()) {
                    ((MessageListDelegate)Activity).ListIsEmpty ();
                }
                MaybeDisplayNoMessagesView (View);
            }
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
