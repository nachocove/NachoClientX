
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
using Android.Views.InputMethods;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class ChatListFragment : Fragment
    {
        private const int CALL_TAG = 1;
        private const int EMAIL_TAG = 2;

        private const string SAVED_SEARCHING_KEY = "ChatListFragment.searching";

        bool searching;
        Android.Widget.EditText searchEditText;
        SwipeMenuListView listView;
        ChatListAdapter chatListAdapter;

        SwipeRefreshLayout mSwipeRefreshLayout;

        public event EventHandler<McChat> onChatClick;

        public static ChatListFragment newInstance ()
        {
            var fragment = new ChatListFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ChatListFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            var addButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            addButton.SetImageResource (Resource.Drawable.chat_newmsg);
            addButton.Visibility = Android.Views.ViewStates.Visible;
            addButton.Click += AddButton_Click;

//            var searchButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.left_button1);
//            searchButton.SetImageResource (Resource.Drawable.nav_search);
//            searchButton.Visibility = Android.Views.ViewStates.Visible;
//            searchButton.Click += SearchButton_Click;

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            // Highlight the tab bar icon of this activity
//            var chatsImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.contacts_image);
//            chatsImage.SetImageResource (Resource.Drawable.nav_chat_active);

            chatListAdapter = new ChatListAdapter (this);

            MaybeDisplayNoChatsView (view);

            listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = chatListAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
                SwipeMenuItem dialItem = new SwipeMenuItem (Activity.ApplicationContext);
                dialItem.setBackground (A.Drawable_NachoSwipeContactCall (Activity));
                dialItem.setWidth (dp2px (90));
                dialItem.setTitle ("Dial");
                dialItem.setTitleSize (14);
                dialItem.setTitleColor (A.Color_White);
                dialItem.setIcon (A.Id_NachoSwipeContactCall);
                dialItem.setId (CALL_TAG);
                menu.addMenuItem (dialItem, SwipeMenu.SwipeSide.LEFT);
                SwipeMenuItem emailItem = new SwipeMenuItem (Activity.ApplicationContext);
                emailItem.setBackground (A.Drawable_NachoSwipeContactEmail (Activity));
                emailItem.setWidth (dp2px (90));
                emailItem.setTitle ("Email");
                emailItem.setTitleSize (14);
                emailItem.setTitleColor (A.Color_White);
                emailItem.setIcon (A.Id_NachoSwipeContactEmail);
                emailItem.setId (EMAIL_TAG);
                menu.addMenuItem (emailItem, SwipeMenu.SwipeSide.RIGHT);
            }
            );

            listView.setOnMenuItemClickListener (( position, menu, index) => {
//                McContact contact = null; // FIXME
//                if (null != contact) {
//                    switch (index) {
//                    case CALL_TAG:
//                        Util.CallNumber (Activity, contact, null);
//                        break;
//                    case EMAIL_TAG:
//                        Util.SendEmail (Activity, McAccount.EmailAccountForContact (contact).Id, contact, alternateEmailAddress);
//                        break;
//                    default:
//                        throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action index {0}", index));
//                    }
//                }
                return false;
            });

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);
            if (null != savedInstanceState) {
//                searching = savedInstanceState.GetBoolean (SAVED_SEARCHING_KEY, false);
//                if (searching) {
//                    StartSearching ();
//                    chatListAdapter.Search (searchEditText.Text);
//                }
            }
        }

        public override void OnResume ()
        {
            base.OnResume ();
            RefreshVisibleChatCells ();
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
            chatListAdapter.Cleanup ();
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutBoolean (SAVED_SEARCHING_KEY, searching);
        }

        void ListView_ItemClick (object sender, Android.Widget.AdapterView.ItemClickEventArgs e)
        {
            if (null != onChatClick) {
                InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
                imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
                var chat = chatListAdapter [e.Position];
                if (null != chat) {
                    onChatClick (this, chat);
                }
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            if (null != onChatClick) {
                onChatClick (this, null);
            }
        }

        void SearchButton_Click (object sender, EventArgs e)
        {
            StartSearching ();
        }


        void CancelButton_Click (object sender, EventArgs e)
        {
            if (searching) {
                CancelSearch ();
            }
        }

        void StartSearching ()
        {
//            searching = true;
//            chatListAdapter.StartSearch ();
//
//            var search = View.FindViewById (Resource.Id.search);
//            search.Visibility = ViewStates.Visible;
//            var navbar = View.FindViewById (Resource.Id.navigation_bar);
//            navbar.Visibility = ViewStates.Gone;
//            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
//            navtoolbar.Visibility = ViewStates.Gone;
//
//            searchEditText.RequestFocus ();
//            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
//            imm.ShowSoftInput (searchEditText, ShowFlags.Implicit);
        }

        void CancelSearch ()
        {
//            searching = false;
//            chatListAdapter.CancelSearch ();
//
//            searchEditText.ClearFocus ();
//            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
//            imm.HideSoftInputFromWindow (searchEditText.WindowToken, HideSoftInputFlags.NotAlways);
//            searchEditText.Text = "";
//
//            var navbar = View.FindViewById (Resource.Id.navigation_bar);
//            navbar.Visibility = ViewStates.Visible;
//            var navtoolbar = View.FindViewById (Resource.Id.navigation_toolbar);
//            navtoolbar.Visibility = ViewStates.Visible;
//            var search = View.FindViewById (Resource.Id.search);
//            search.Visibility = ViewStates.Gone;
        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
//            var searchString = searchEditText.Text;
//            if (TestMode.Instance.Process (searchString)) {
//                return;
//            }
//            chatListAdapter.Search (searchString);
        }

        public void OnBackPressed ()
        {
            if (searching) {
                CancelSearch ();
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
            refreshTimer = new NcTimer ("ChatListFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
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

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ChatSetChanged:
                RefreshVisibleChatCells ();
                break;
            }
        }

        void RefreshVisibleChatCells ()
        {
            if (MaybeDisplayNoChatsView (View)) {
                return;
            }
            for (var i = listView.FirstVisiblePosition; i <= listView.LastVisiblePosition; i++) {
                var cell = listView.GetChildAt (i - listView.FirstVisiblePosition);
                if (null != cell) {
                    chatListAdapter.GetView (i, cell, listView);
                }
            }
        }

        public bool MaybeDisplayNoChatsView (View view)
        {
            if (null != view) {
                if (null != chatListAdapter) {
                    var showEmpty = !searching && (0 == chatListAdapter.Count);
                    view.FindViewById<Android.Widget.TextView> (Resource.Id.no_chats).Visibility = (showEmpty ? ViewStates.Visible : ViewStates.Gone);
                    return true;
                }
            }
            return false;
        }

        private int dp2px (int dp)
        {
            return (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, (float)dp, Resources.DisplayMetrics);
        }

    }

    public class ChatListAdapter : Android.Widget.BaseAdapter<McChat>
    {
        List<McChat> chats;

        public Dictionary<int, int> unreadCountsByChat { get; private set; }

        ChatListFragment parent;

        McAccount account;

        public ChatListAdapter (ChatListFragment parent)
        {
            this.parent = parent;
            account = NcApplication.Instance.Account;

            RefreshChatsIfVisible ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void Cleanup ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        protected void RefreshChatsIfVisible ()
        {
            if (account.AccountType == McAccount.AccountTypeEnum.Unified) {
                chats = McChat.LastestChats ();
                unreadCountsByChat = McChat.UnreadCountsByChat ();
            } else {
                chats = McChat.LastestChatsForAccount (account.Id);
                unreadCountsByChat = McChat.UnreadCountsByChat (account.Id);
            }
            NotifyDataSetChanged ();
            if (null != parent) {
                parent.MaybeDisplayNoChatsView (parent.View);
            }
        }

        public override long GetItemId (int position)
        {
            return chats [position].Id;
        }

        public override int Count {
            get {
                return chats.Count;
            }
        }

        public override McChat this [int position] {  
            get {
                return chats [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ChatListCell, parent, false);

            }
            var chat = chats [position];
            Bind.BindChatListCell (chat, view);
            return view;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ChatSetChanged:
                RefreshChatsIfVisible ();
                break;
            }
        }

    }
}

