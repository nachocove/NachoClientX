
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
            Adapter.Searcher.SearchFor (searchText);
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
            // TODO:
		}

		#endregion
	}

	public class ContactsSearchAdapter : RecyclerView.Adapter
	{

		public interface Listener
		{
            void OnContactSelected (McContact contact);
		}

        public ContactsGeneralSearch Searcher { get; private set; }
        List<McContactEmailAddressAttribute> Results;
		WeakReference<Listener> WeakListener;

        public ContactsSearchAdapter (Listener listener) : base ()
		{
			WeakListener = new WeakReference<Listener> (listener);
            Searcher = new ContactsGeneralSearch (UpdateResults);
            Results = new List<McContactEmailAddressAttribute> ();
		}

        void UpdateResults (string searchString, List<McContactEmailAddressAttribute> results)
        {
        	Results = results;
            NotifyDataSetChanged ();
        }

		public override int ItemCount {
			get {
                return Results.Count;
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

		public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
		{
            var contactHolder = (holder as ContactViewHolder);
            var emailAttribute = Results [position];
            var contact = emailAttribute.GetContact ();
            contactHolder.SetContact (contact);
            var values = contactHolder.BackgroundView.Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.WindowBackground });
            contactHolder.BackgroundView.SetBackgroundResource (values.GetResourceId (0, 0));
		}

		void ItemClicked (int position)
		{
			Listener listener;
			if (WeakListener.TryGetTarget (out listener)) {
                var emailAttribute = Results [position];
                var contact = emailAttribute.GetContact ();
                if (contact != null) {
                    listener.OnContactSelected (contact);
				}
			}
		}
	}
}