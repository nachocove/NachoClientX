
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
            Adapter.SearchResults.EnterSearchMode (NcApplication.Instance.Account);
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
            Adapter.SearchResults.ExitSearchMode ();
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Public API

        public void SearchForText (string searchText)
        {
            Adapter.SearchResults.SearchFor (searchText);
        }

        public void StartServerSearch ()
        {
            Adapter.SearchResults.StartServerSearch ();
        }

        #endregion

        #region Adapter Listener

        public void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread)
        {
            ShowMessage (message);
        }

        #endregion

        #region Private Helpers

        void ShowMessage (McEmailMessage message)
        {
            var intent = MessageViewActivity.BuildIntent (Activity, message.Id);
            StartActivity (intent);
        }

        #endregion
    }

    public class MessageSearchAdapter : RecyclerView.Adapter
    {

        public interface Listener
        {
            void OnMessageSelected (McEmailMessage message, McEmailMessageThread thread);
        }
        
        public EmailSearch SearchResults { get; private set; }
        WeakReference<Listener> WeakListener;

        public MessageSearchAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            SearchResults = new EmailSearch (UpdateResults);
        }

        void UpdateResults (string searchString, List<McEmailMessageThread> results)
        {
            NotifyDataSetChanged ();
        }

        public override int ItemCount {
            get {
                return SearchResults.Count ();
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            var holder = MessageViewHolder.Create (parent);
            holder.ContentView.Click += (sender, e) => {
                ItemClicked (holder.AdapterPosition);
            };
            return holder;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var messageHolder = (holder as MessageViewHolder);
            var message = SearchResults.GetCachedMessage (position);
            var thread = SearchResults.GetEmailThread (position);
            messageHolder.SetMessage (message, thread.MessageCount);
            if (SearchResults.IncludesMultipleAccounts ()) {
                messageHolder.IndicatorColor = Util.ColorForAccount (message.AccountId);
            } else {
                messageHolder.IndicatorColor = 0;
            }
            var values = messageHolder.BackgroundView.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.WindowBackground });
            messageHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
        }

        void ItemClicked (int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                var message = SearchResults.GetCachedMessage (position);
                var thread = SearchResults.GetEmailThread (position);
                if (message != null && thread != null) {
                    listener.OnMessageSelected (message, thread);
                }
            }
        }
    }
}