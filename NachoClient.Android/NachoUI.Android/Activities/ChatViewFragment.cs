
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
        EmailAddressField ToField;
        Android.Widget.TextView titleView;

        McChat chat;

        ButtonBar buttonBar;

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
            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.chat_add_contact, AddButton_Click);

            searchEditText = view.FindViewById<Android.Widget.EditText> (Resource.Id.searchstring);
            searchEditText.TextChanged += SearchString_TextChanged;

            var cancelButton = view.FindViewById (Resource.Id.cancel);
            cancelButton.Click += CancelButton_Click;

            var sendButton = view.FindViewById<Button> (Resource.Id.chat_send);
            sendButton.Click += SendButton_Click;

            ToField = view.FindViewById<EmailAddressField> (Resource.Id.compose_to);
            ToField.AllowDuplicates (false);
            ToField.Adapter = new ContactAddressAdapter (this.Activity);
            ToField.TokensChanged += ToField_TokensChanged;

            titleView = view.FindViewById<Android.Widget.TextView> (Resource.Id.chat_title);
            titleView.Click += TitleView_Click;

            return view;
        }

        void ToField_TokensChanged (object sender, EventArgs e)
        {
            UpdateChatFromToField ();
        }

        void TitleView_Click (object sender, EventArgs e)
        {
            if (null != chat) {
                var participants = McChatParticipant.GetChatParticipants (chat.Id);
                var intent = ChatParticipantListActivity.ParticipantsIntent (this.Activity, typeof(ChatParticipantListActivity), Intent.ActionView, chat.AccountId, participants);
                StartActivity (intent);
            }
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            chat = ((IChatViewFragmentOwner)Activity).ChatToView;
            chatAdapter = new ChatAdapter (chat, this);

            ShowAddressEditor (null == chat);

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
            if (IsAddressEditorVisible ()) {
                chat = null;
                UpdateChatFromToField ();
            }
            ShowAddressEditor ((null == chat) || !IsAddressEditorVisible ());
        }

        void SendButton_Click (object sender, EventArgs e)
        {
            var editText = View.FindViewById<EditText> (Resource.Id.chat_message);
            var text = editText.Text;

            if (String.IsNullOrEmpty (text)) {
                return;
            }
                
            if (null == chat) {
                UpdateChatFromToField ();
            }
            ShowAddressEditor (null == chat);

            if (null != chat) {
                ChatMessageComposer.SendChatMessage (chat, text, chatAdapter.GetNewestChats (3), (McEmailMessage message) => {
                    chat.AddMessage (message);
                    editText.Text = "";
                });
            }
        }

        void UpdateChatFromToField ()
        {
            var account = NcApplication.Instance.Account;
            if (McAccount.GetUnifiedAccount ().Id == account.Id) {
                account = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
            }

            var addresses = NcEmailAddress.ParseToAddressListString (ToField.AddressString);
            if ((null == addresses) || (0 == addresses.Count)) {
                chat = null;
            } else {
                chat = McChat.ChatForAddresses (account.Id, addresses);
            }

            listView = View.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            chatAdapter = new ChatAdapter (chat, this);
            listView.Adapter = chatAdapter;
        }

        bool IsAddressEditorVisible ()
        {
            var toView = View.FindViewById (Resource.Id.to_view);
            return ViewStates.Visible == toView.Visibility;
        }

        void ShowAddressEditor (bool visible)
        {
            var toView = View.FindViewById (Resource.Id.to_view);

            if (visible) {
                titleView.Visibility = ViewStates.Gone;
                toView.Visibility = ViewStates.Visible;
                ToField.AddressList = (null == chat) ? null : McChatParticipant.ConvertToAddressList (McChatParticipant.GetChatParticipants (chat.Id));
            } else {
                toView.Visibility = ViewStates.Gone;
                titleView.Visibility = ViewStates.Visible;
                titleView.Text = (null == chat) ? "" : chat.CachedParticipantsLabel;
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

        }

        void CancelSearch ()
        {

        }

        void SearchString_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {

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
        List<McEmailMessage> messages;
        Dictionary<int, McChatParticipant> ParticipantsByEmailId;
        Fragment parent;

        public ChatAdapter (McChat chat, Fragment parent)
        {
            this.chat = chat;
            this.parent = parent;

            if (null != chat) {
                ParticipantsByEmailId = McChatParticipant.GetChatParticipantsByEmailId (chat.Id);
            }

            RefreshChatIfVisible ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void Cleanup ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public List<McEmailMessage> GetNewestChats (int count)
        {
            var previousMessages = new List<McEmailMessage> ();
            for (int i = messages.Count - 1; i >= messages.Count - count && i >= 0; --i) {
                previousMessages.Add (messages [i]);
            }
            return previousMessages;
        }

        protected void RefreshChatIfVisible ()
        {
            if (null == chat) {
                messages = new List<McEmailMessage> ();
            } else {
                messages = chat.GetAllMessages ();
            }
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

            McChatParticipant particpant = null;
            ParticipantsByEmailId.TryGetValue (message.FromEmailAddressId, out particpant);

            Bind.BindChatViewCell (message, previousMessage, nextMessage, particpant, view);
            Bind.BindChatAttachments (message, view, LayoutInflater.From (parent.Context), AttachmentSelectedCallback, AttachmentErrorCallback);
            return view;
        }


        public void AttachmentSelectedCallback (McAttachment attachment)
        {
            AttachmentHelper.OpenAttachment (parent.Activity, attachment);
        }

        public  void AttachmentErrorCallback (McAttachment attachment, NcResult nr)
        {
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

