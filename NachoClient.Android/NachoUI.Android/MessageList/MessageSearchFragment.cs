
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Views;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.Widget;
using Android.Support.V7.Widget;
using Android.Text;
using Android.Text.Style;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class MessageSearchFragment : Fragment, MessageSearchAdapter.Listener
    {

        public MessageSearchFragment () : base ()
        {
            RetainInstance = true;
        }

        #region Subviews

        RecyclerView ListView;
        MessageSearchAdapter Adapter;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Android.OS.Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MessageSearchFragment, container, false);
            FindSubviews (view);
            Adapter = new MessageSearchAdapter (this);
            Adapter.Account = NcApplication.Instance.Account;
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
        }

        public override void OnPause ()
        {
            // StopListeningForStatusInd ();
            base.OnPause ();
        }

        public override void OnDestroyView ()
        {
            Adapter.Cleanup ();
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Public API

        public void SearchForText (string searchText)
        {
            Adapter.Search (searchText);
        }

        #endregion

        #region Adapter Listener

        public void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread)
        {
            ShowMessage (message);
        }

        public void OnContactSelected (McContact contact)
        {
            ShowInteractions (contact);
        }

        #endregion

        #region Private Helpers

        void ShowMessage (McEmailMessage message)
        {
            var intent = MessageViewActivity.BuildIntent (Activity, message.Id);
            StartActivity (intent);
        }

        void ShowInteractions (McContact contact)
        {
            var intent = MessageListActivity.BuildContactIntent (Activity, contact);
            StartActivity (intent);
        }

        #endregion
    }

    public class MessageSearchAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread);
            void OnContactSelected (McContact contact);
        }

        WeakReference<Listener> WeakListener;

        EmailSearcher Searcher;
        EmailServerSearcher ServerSearcher;
        EmailSearchResults Results;
        NachoEmailMessages Messages;
        NachoEmailMessages ServerMessages;
        string Query;
        bool ServerSearchStarted;

        int _GroupCount;
        int ContactsGroupPosition;
        int MessagesGroupPosition;
        int ServerMessagesGroupPosition;
        int ServerPlaceholderGroupPosition;

        enum ViewType
        {
            Contact,
            Message,
            ServerPlaceholder
        }

        public McAccount Account {
            get {
                return Searcher.Account;
            }
            set {
                Searcher.Account = value;
                ServerSearcher.Account = value;
            }
        }

        public MessageSearchAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Searcher = new EmailSearcher ();
            Searcher.ResultsFound += UpdateResults;
            ServerSearcher = new EmailServerSearcher ();
            ServerSearcher.ResultsFound += UpdateServerResults;
        }

        public void Search (string query)
        {
            Query = query;
            Searcher.Search (query);
            ServerMessages = null;
            ServerSearchStarted = false;
            ReloadServerGroup ();
        }

        void UpdateResults (object sender, EmailSearchResults results)
        {
            Results = results;
            Messages = new NachoPrequeriedEmailMessages (results.MessageIds);
            ReloadData ();
        }

        void UpdateServerResults (object sender, int [] messageIds)
        {
            ServerMessages = new NachoPrequeriedEmailMessages (messageIds);
            ReloadServerGroup ();
        }

        public void Cleanup ()
        {
            Searcher.ResultsFound -= UpdateResults;
            ServerSearcher.ResultsFound -= UpdateServerResults;
            ServerSearcher.Cleanup ();
        }

        void ReloadData ()
        {
            _GroupCount = 0;
            ContactsGroupPosition = -1;
            MessagesGroupPosition = -1;
            ServerMessagesGroupPosition = -1;
            ServerPlaceholderGroupPosition = -1;
            if (Results != null && Results.ContactIds.Length > 0) {
                ContactsGroupPosition = _GroupCount++;
            }
            MessagesGroupPosition = _GroupCount++;
            if (!string.IsNullOrEmpty (Query)) {
                if (ServerMessages != null && ServerMessages.Count () > 0) {
                    ServerMessagesGroupPosition = _GroupCount++;
                } else {
                    ServerPlaceholderGroupPosition = _GroupCount++;
                }
            }
            NotifyDataSetChanged ();
        }

        void ReloadServerGroup ()
        {
            var serverGroupPosition = -1;
            if (ServerMessagesGroupPosition >= 0) {
                serverGroupPosition = ServerMessagesGroupPosition;
            } else if (ServerPlaceholderGroupPosition >= 0) {
                serverGroupPosition = ServerPlaceholderGroupPosition;
            }
            ServerMessagesGroupPosition = -1;
            ServerPlaceholderGroupPosition = -1;
            if (string.IsNullOrEmpty (Query)) {
                if (serverGroupPosition >= 0) {
                    _GroupCount--;
                    NotifyDataSetChanged ();
                }
            } else {
                if (serverGroupPosition < 0) {
                    serverGroupPosition = _GroupCount++;
                }
                if (ServerMessages != null && ServerMessages.Count () > 0) {
                    ServerMessagesGroupPosition = serverGroupPosition;
                } else {
                    ServerPlaceholderGroupPosition = serverGroupPosition;
                }
                NotifyDataSetChanged ();
            }
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == ContactsGroupPosition) {
                return context.GetString (Resource.String.messages_search_header_contacts);
            }
            if (groupPosition == MessagesGroupPosition) {
                return context.GetString (Resource.String.messages_search_header_messages);
            }
            return null;
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == ContactsGroupPosition) {
                return Results.ContactIds.Length;
            }
            if (groupPosition == MessagesGroupPosition) {
                return Results?.MessageIds.Length ?? 0;
            }
            if (groupPosition == ServerMessagesGroupPosition) {
                return ServerMessages.Count ();
            }
            if (groupPosition == ServerPlaceholderGroupPosition) {
                return 1;
            }
            return 0;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == ContactsGroupPosition) {
                return (int)ViewType.Contact;
            }
            if (groupPosition == MessagesGroupPosition || groupPosition == ServerMessagesGroupPosition) {
                return (int)ViewType.Message;
            }
            if (groupPosition == ServerPlaceholderGroupPosition) {
                return (int)ViewType.ServerPlaceholder;
            }
            return 0;
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.Contact:
                return ContactViewHolder.Create (parent);
            case ViewType.Message:
                return MessageViewHolder.Create (parent);
            case ViewType.ServerPlaceholder:
                return BasicItemViewHolder.Create (parent);
            }
            return null;
        }

        public override void OnBindHeaderViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
        {
            base.OnBindHeaderViewHolder (holder, groupPosition);
            var values = holder.ItemView.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.WindowBackground });
            holder.ItemView.SetBackgroundResource (values.GetResourceId (0, 0));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var values = holder.ItemView.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.WindowBackground });
            if (groupPosition == ContactsGroupPosition) {
                var contact = GetContact (position);
                var contactHolder = holder as ContactViewHolder;
                if (contact != null) {
                    contactHolder.SetContact (contact, contact.GetFirstAttributelMatchingTokens (Results.Tokens));
                    contactHolder.ContentView.Visibility = ViewStates.Visible;
                } else {
                    contactHolder.ContentView.Visibility = ViewStates.Invisible;
                }
                contactHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
            } else if (groupPosition == MessagesGroupPosition || groupPosition == ServerMessagesGroupPosition) {
                var messages = MessagesGroupPosition == ServerMessagesGroupPosition ? ServerMessages : Messages;
                var message = messages.GetCachedMessage (position);
                var thread = messages.GetEmailThread (position);
                var messageHolder = (holder as MessageViewHolder);
                if (message != null) {
                    messageHolder.SetMessage (message, thread.MessageCount);
                    if (Account.AccountType == McAccount.AccountTypeEnum.Unified) {
                        messageHolder.IndicatorColor = Util.ColorForAccount (message.AccountId);
                    } else {
                        messageHolder.IndicatorColor = 0;
                    }
                    messageHolder.ContentView.Visibility = ViewStates.Visible;
                } else {
                    Log.LOG_SEARCH.Warn ("Message search results returned a deleted message: {0}", thread.FirstMessageId);
                    NachoCore.Index.Indexer.Instance.RemoveMessageId (NcApplication.Instance.Account.Id, thread.FirstMessageId);
                    messageHolder.ContentView.Visibility = ViewStates.Invisible;
                }
                messageHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
            } else if (groupPosition == ServerPlaceholderGroupPosition) {
                var viewHolder = holder as BasicItemViewHolder;
                if (ServerMessages == null) {
                    var format = viewHolder.ItemView.Context.GetString (Resource.String.messages_search_server_format);
                    viewHolder.SetLabel (string.Format (format, Query));
                } else {
                    viewHolder.SetLabel (Resource.String.messages_search_server_no_results);
                }
                viewHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
            }
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == ContactsGroupPosition) {
                var contact = GetContact (position);
                if (contact != null && WeakListener.TryGetTarget (out var listener)) {
                    listener.OnContactSelected (contact);
                }
            } else if (groupPosition == MessagesGroupPosition || groupPosition == ServerMessagesGroupPosition) {
                var messages = MessagesGroupPosition == ServerMessagesGroupPosition ? ServerMessages : Messages;
                var message = messages.GetCachedMessage (position);
                var thread = messages.GetEmailThread (position);
                if (message != null && thread != null && WeakListener.TryGetTarget (out var listener)) {
                    listener.OnMessageSelected (message, thread);
                }
            } else if (groupPosition == ServerPlaceholderGroupPosition) {
                if (!ServerSearchStarted) {
                    ServerSearchStarted = true;
                    ServerSearcher.Search (Query);
                }
            }
        }

        McContact GetContact (int index)
        {
            // TODO: we could do some caching here
            var id = Results.ContactIds [index];
            var contact = McContact.QueryById<McContact> (id);
            if (contact == null) {
                Log.LOG_SEARCH.Warn ("Message search results returned a deleted contact: {0}", id);
                NachoCore.Index.Indexer.Instance.RemoveContactId (NcApplication.Instance.Account.Id, id);
            }
            return contact;
        }

        class BasicItemViewHolder : ViewHolder
        {

            public readonly View BackgroundView;
            public readonly View ContentView;
            public readonly TextView LabelView;

            public override View ClickTargetView {
                get {
                    return ContentView;
                }
            }

            public static BasicItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.MessageSearchBasicItem, parent, false);
                return new BasicItemViewHolder (view);
            }

            public BasicItemViewHolder (View view) : base (view)
            {
                BackgroundView = view.FindViewById (Resource.Id.background);
                ContentView = view.FindViewById (Resource.Id.content);
                LabelView = view.FindViewById (Resource.Id.label) as TextView;
            }

            public void SetLabel (string name)
            {
                LabelView.Text = name;
            }

            public void SetLabel (int nameResource)
            {
                var name = ItemView.Context.GetString (nameResource);
                SetLabel (name);
            }
        }

        class ContactViewHolder : ViewHolder
        {

            public readonly View BackgroundView;
            public readonly View ContentView;
            public readonly TextView NameView;
            public readonly TextView DetailView;
            public readonly PortraitView PortraitView;

            public override View ClickTargetView {
                get {
                    return ContentView;
                }
            }

            public static ContactViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.MessageSearchContactItem, parent, false);
                return new ContactViewHolder (view);
            }

            public ContactViewHolder (View view) : base (view)
            {
                BackgroundView = view.FindViewById (Resource.Id.background);
                ContentView = view.FindViewById (Resource.Id.content);
                NameView = view.FindViewById (Resource.Id.main_label) as TextView;
                DetailView = view.FindViewById (Resource.Id.detail_label) as TextView;
                PortraitView = view.FindViewById (Resource.Id.portrait_view) as PortraitView;
            }

            public void SetContact (McContact contact, string alternateEmail = null)
            {
                var name = contact.GetDisplayName ();
                var email = alternateEmail ?? contact.GetPrimaryCanonicalEmailAddress ();
                if (string.IsNullOrWhiteSpace (name) || string.Compare (name, email, StringComparison.OrdinalIgnoreCase) == 0) {
                    NameView.Text = email;
                    DetailView.Text = "";
                } else {
                    NameView.Text = name;
                    DetailView.Text = email;
                }
                PortraitView.SetPortrait (contact.PortraitId, contact.CircleColor, contact.Initials);
            }
        }
    }
}