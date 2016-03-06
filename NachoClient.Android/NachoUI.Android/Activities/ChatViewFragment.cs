
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
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public interface IChatViewFragmentOwner
    {
        void DoneWithMessage ();

        McChat ChatToView { get; }
    }

    public class ChatViewFragment : Fragment
    {
        private const string SAVED_SEARCHING_KEY = "ChatViewFragment.searching";

        bool searching;
        Android.Widget.EditText searchEditText;
        SwipeMenuListView listView;
        ChatAdapter chatAdapter;

        McChat chat;

        ButtonBar buttonBar;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;

        SwipeRefreshLayout mSwipeRefreshLayout;

        public event EventHandler<McChat> onChatClick;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ChatViewFragment, container, false);

            mSwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            mSwipeRefreshLayout.SetColorSchemeResources (Resource.Color.refresh_1, Resource.Color.refresh_2, Resource.Color.refresh_3);

            mSwipeRefreshLayout.Refresh += (object sender, EventArgs e) => {
                rearmRefreshTimer (3);
            };

            buttonBar = new ButtonBar (view);
            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.nav_add, AddButton_Click);
            buttonBar.SetIconButton (ButtonBar.Button.Left1, Resource.Drawable.nav_search, SearchButton_Click);

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            var sendButton = view.FindViewById<Button> (Resource.Id.chat_send);
            sendButton.Click += SendButton_Click;

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            chat = ((IChatViewFragmentOwner)Activity).ChatToView;
            chatAdapter = new ChatAdapter (this, chat);

            var titleView = View.FindViewById<Android.Widget.TextView> (Resource.Id.chat_title);
            titleView.Text = chat.CachedParticipantsLabel;

            ParticipantsByEmailId = McChatParticipant.GetChatParticipantsByEmailId (chat.Id);

            listView = View.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = chatAdapter;

            listView.ItemClick += ListView_ItemClick;

            listView.setMenuCreator ((menu) => {
            }
            );

            listView.setOnMenuItemClickListener (( position, menu, index) => {
                return false;
            });

            listView.setOnSwipeStartListener ((position) => {
                mSwipeRefreshLayout.Enabled = false;
            });

            listView.setOnSwipeEndListener ((position) => {
                mSwipeRefreshLayout.Enabled = true;
            });
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
            chatAdapter.Cleanup ();
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
                var message = chatAdapter [e.Position];
                // TODO: Now what?
            }
        }

        void AddButton_Click (object sender, EventArgs e)
        {
            //   Activity.StartActivity (ContactEditActivity.AddContactIntent (Activity));
        }


        void SendButton_Click (object sender, EventArgs e)
        {
//            if (chat == null) {
//                var addresses = HeaderView.ToView.AddressList;
//                chat = McChat.ChatForAddresses (Account.Id, addresses);
//                if (chat == null) {
//                    Log.Error (Log.LOG_CHAT, "Got null chat when sending new message");
//                    return;
//                }
//                chat.ClearDraft ();
//                foreach (var attachment in AttachmentsForUnsavedChat) {
//                    attachment.Link (Chat.Id, Chat.AccountId, McAbstrFolderEntry.ClassCodeEnum.Chat);
//                }
//                AttachmentsForUnsavedChat.Clear ();
//                UpdateForChat ();
//                ReloadMessages ();
//            }
            var editText = View.FindViewById<EditText> (Resource.Id.chat_message);
            var text = editText.Text;
            ChatMessageComposer.SendChatMessage (chat, text, chatAdapter.GetNewestChats(3), (McEmailMessage message) => {
                chat.AddMessage (message);
                editText.Text = "";
            });
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
            refreshTimer = new NcTimer ("ChatFragment refresh", EndRefreshingOnUIThread, null, seconds * 1000, 0); 
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
                    chatAdapter.GetView (i, cell, listView);
                }
            }
        }

        public bool MaybeDisplayNoChatsView (View view)
        {
            if (null != view) {
                if (null != chatAdapter) {
                    var showEmpty = !searching && (0 == chatAdapter.Count);
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

    public class ChatAdapter : Android.Widget.BaseAdapter<McEmailMessage>
    {
        McChat chat;
        ChatViewFragment parent;
        List<McEmailMessage> messages;

        public ChatAdapter (ChatViewFragment parent, McChat chat)
        {
            this.parent = parent;
            this.chat = chat;

            RefreshChatIfVisible ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void Cleanup ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public List<McEmailMessage> GetNewestChats(int count)
        {
            var previousMessages = new List<McEmailMessage> ();
            for (int i = messages.Count - 1; i >= messages.Count - count && i >= 0; --i){
                previousMessages.Add (messages [i]);
            }
            return previousMessages;
        }

        protected void RefreshChatIfVisible ()
        {
            messages = chat.GetAllMessages ();
            NotifyDataSetChanged ();
        }

        public override long GetItemId (int position)
        {
            return messages [position].Id;
        }

        public override int Count {
            get {
                return messages.Count;
            }
        }

        public override McEmailMessage this [int position] {  
            get {
                return messages [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ChatViewCell, parent, false);

            }
            var message = messages [position];
            var previousMessage = (0 == position) ? null : messages [position - 1];
            var nextMessage = ((messages.Count - 1) >= position) ? null : messages [position + 1];
            Bind.BindChatViewCell (message, previousMessage, nextMessage, view);
            return view;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_ChatMessageAdded:
                RefreshChatIfVisible ();
                break;
            }
        }

    }
}

