//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class ContactEmailChooserFragment : Fragment
    {
        private EditText searchField;
        private ButtonBar buttonBar;
        private ContactChooserListAdapter adapter;
        private ContactsEmailSearch searcher;

        public void SetInitialValues (string initialSearch)
        {
            searchField.Text = initialSearch;
            searchField.SetSelection (initialSearch.Length);
            if (!string.IsNullOrEmpty (initialSearch)) {
                searcher.SearchFor (initialSearch);
            }
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            adapter = new ContactChooserListAdapter ();
            searcher = new ContactsEmailSearch ((string searchString, List<McContactEmailAddressAttribute> results) => {
                adapter.SearchResults = results;
            });
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactEmailChooserFragment, container, false);

            buttonBar = new ButtonBar (view);
            buttonBar.SetTitle ("Chooser");

            searchField = view.FindViewById<EditText> (Resource.Id.contact_search_text);
            searchField.TextChanged += SearchField_TextChanged;
            searchField.SetOnKeyListener (new KeyListener (EnterPressed));

            var listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = adapter;
            listView.ItemClick += ListView_ItemClick;

            return view;
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            searcher.Dispose ();
            searcher = null;
        }

        private void EnterPressed ()
        {
            if (string.IsNullOrEmpty (searchField.Text)) {
                this.Activity.SetResult (Result.Canceled);
            } else if (!searchField.Text.Contains("@")) {
                NcAlertView.ShowMessage (this.Activity, "Invalid email address", "The value must be an email address containing '@'");
                return;
            } else {
                this.Activity.SetResult (Result.Ok, ContactEmailChooserActivity.ResultIntent (searchField.Text, null));
            }
            this.Activity.Finish ();
        }

        private void ListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var chosen = adapter [e.Position];
            this.Activity.SetResult (Result.Ok, ContactEmailChooserActivity.ResultIntent (chosen.Value, chosen.GetContact ()));
            this.Activity.Finish ();
        }

        private void SearchField_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            searcher.SearchFor (searchField.Text);
        }

        private class KeyListener : Java.Lang.Object, View.IOnKeyListener
        {
            Action enterAction;

            public KeyListener (Action enterAction)
            {
                this.enterAction = enterAction;
            }

            public bool OnKey (View view, Keycode code, KeyEvent e)
            {
                if (KeyEventActions.Down == e.Action && Keycode.Enter == code) {
                    enterAction ();
                    return true;
                }
                return false;
            }
        }
    }

    public class ContactChooserListAdapter : BaseAdapter<McContactEmailAddressAttribute>
    {
        private List<McContactEmailAddressAttribute> searchResults = new List<McContactEmailAddressAttribute> ();

        public List<McContactEmailAddressAttribute> SearchResults {
            private get {
                return searchResults;
            }
            set {
                searchResults = value;
                NotifyDataSetChanged ();
            }
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return SearchResults.Count;
            }
        }

        public override McContactEmailAddressAttribute this [int index] {
            get {
                return SearchResults [index];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View cell = convertView;
            if (null == cell) {
                cell = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ContactCell, parent, false);
            }

            var searchResult = this [position];
            Bind.BindContactCell (searchResult.GetContact (), cell, null, searchResult.Value);

            return cell;
        }
    }
}
