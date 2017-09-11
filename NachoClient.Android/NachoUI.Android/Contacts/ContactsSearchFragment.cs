
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

    public class ContactsSearchFragment : Fragment, ContactsSearchAdapter.Listener
    {

        public ContactsSearchFragment () : base ()
        {
            RetainInstance = true;
        }

        #region Subviews

        RecyclerView ListView;
        ContactsSearchAdapter Adapter;

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
            var view = inflater.Inflate (Resource.Layout.ContactsSearchFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactsSearchAdapter (this);
            ListView.SetAdapter (Adapter);
            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Public API

        public void SearchForText (string searchText)
        {
            Adapter.Searcher.Search (searchText);
        }

        public void StartServerSearch ()
        {
        }

        #endregion

        #region Adapter Listener

        public void OnContactSelected (McContact contact)
        {
            ShowContact (contact);
        }

        #endregion

        #region Private Helpers

        void ShowContact (McContact contact)
        {
            var intent = ContactViewActivity.BuildIntent (Activity, contact);
            StartActivity (intent);
        }

        #endregion
    }

    public class ContactsSearchAdapter : RecyclerView.Adapter
    {

        public interface Listener
        {
            void OnContactSelected (McContact contact);
        }

        public ContactSearcher Searcher { get; private set; }
        ContactSearchResults Results;
        WeakReference<Listener> WeakListener;

        public ContactsSearchAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Searcher = new ContactSearcher ();
            Searcher.ResultsFound += UpdateResults;
        }

        void UpdateResults (object sender, ContactSearchResults results)
        {
            Results = results;
            NotifyDataSetChanged ();
        }

        public override int ItemCount {
            get {
                return Results?.ContactIds.Length ?? 0;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            var holder = ContactViewHolder.Create (parent);
            holder.ContentView.Click += (sender, e) => {
                ItemClicked (holder.AdapterPosition);
            };
            return holder;
        }

        McContact GetContact (int position)
        {
            var id = Results.ContactIds [position];
            return McContact.QueryById<McContact> (id);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var contactHolder = (holder as ContactViewHolder);
            var contact = GetContact (position);
            if (contact != null) {
                contactHolder.SetContact (contact, alternateEmail: contact.GetFirstAttributelMatchingTokens (Results.Tokens));
                contactHolder.ContentView.Visibility = ViewStates.Visible;
            } else {
                contactHolder.ContentView.Visibility = ViewStates.Invisible;
            }
            var values = contactHolder.BackgroundView.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.WindowBackground });
            contactHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
        }

        void ItemClicked (int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                var contact = GetContact (position);
                if (contact != null) {
                    listener.OnContactSelected (contact);
                }
            }
        }
    }
}