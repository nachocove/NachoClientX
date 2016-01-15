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
        private int accountId;
        private bool doGalSearch;
        private string searchToken;

        public void SetInitialValues (int accountId, string initialSearch)
        {
            this.accountId = accountId;
            var account = McAccount.QueryById<McAccount> (accountId);
            doGalSearch = null != account && account.HasCapability (McAccount.AccountCapabilityEnum.ContactReader);

            searchField.Text = initialSearch;
            searchField.SetSelection (initialSearch.Length);
            adapter.SetSearchString (initialSearch);
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            adapter = new ContactChooserListAdapter ();

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
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
            if (null != searchToken) {
                McPending.Cancel (accountId, searchToken);
                searchToken = null;
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        private void EnterPressed ()
        {
            if (string.IsNullOrEmpty (searchField.Text)) {
                this.Activity.SetResult (Result.Canceled);
            } else {
                this.Activity.SetResult (Result.Ok, ContactEmailChooserActivity.ResultIntent (searchField.Text, null));
            }
            this.Activity.Finish ();
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (null != searchToken &&
                NcResult.SubKindEnum.Info_ContactSearchCommandSucceeded == s.Status.SubKind &&
                null != s.Tokens &&
                s.Tokens.Contains (searchToken))
            {
                adapter.SetSearchString (searchField.Text);
            }
        }

        private void ListView_ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            var chosen = adapter [e.Position];
            this.Activity.SetResult (Result.Ok, ContactEmailChooserActivity.ResultIntent (chosen.Value, chosen.GetContact ()));
            this.Activity.Finish ();
        }

        private void SearchField_TextChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            string searchString = searchField.Text;
            adapter.SetSearchString (searchString);
            if (doGalSearch && 0 != accountId && 0 < searchString.Length) {
                if (null == searchToken) {
                    searchToken = BackEnd.Instance.StartSearchContactsReq (accountId, searchString, null).GetValue<string> ();
                } else {
                    BackEnd.Instance.SearchContactsReq (accountId, searchString, null, searchToken);
                }
            }
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
        private SearchHelper searcher;
        private List<McContactEmailAddressAttribute> searchResults = new List<McContactEmailAddressAttribute> ();

        public ContactChooserListAdapter ()
        {
            searcher = new SearchHelper ("ContactEmailChooser", (string searchString) => {
                if (string.IsNullOrEmpty (searchString)) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResults.Clear ();
                        NotifyDataSetChanged ();
                    });
                } else {
                    var results = McContact.SearchAllContactsForEmail (searchString);
                    InvokeOnUIThread.Instance.Invoke (() => {
                        searchResults = results;
                        NotifyDataSetChanged ();
                    });
                }
            });
        }

        public void SetSearchString (string searchString)
        {
            searcher.Search (searchString);
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return searchResults.Count;
            }
        }

        public override McContactEmailAddressAttribute this [int index] {
            get {
                return searchResults [index];
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
