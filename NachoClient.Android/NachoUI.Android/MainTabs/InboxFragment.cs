//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Views.InputMethods;

using NachoCore;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class InboxFragment : MessageListFragment, MainTabsActivity.Tab, Android.Support.V4.View.MenuItemCompat.IOnActionExpandListener
    {

        private McAccount Account;

        #region Tab Interface

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            tabActivity.MenuInflater.Inflate (Resource.Menu.inbox, menu);
            var searchItem = menu.FindItem (Resource.Id.search);
            Android.Support.V4.View.MenuItemCompat.SetOnActionExpandListener (searchItem, this);
            var searchView = (searchItem.ActionView as SearchView);
            searchView.SetIconifiedByDefault (false);
            return true;
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            if (Account.Id != NcApplication.Instance.Account.Id) {
                OnAccountSwitched (tabActivity);
            }
            tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_mail_filled, ActionButtonClicked);
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
        {
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
            Account = NcApplication.Instance.Account;
            CancelSyncing ();
            // TODO: cancel editing (if active)
            // TODO: cancel row swiping (if active)

            SetEmailMessages (NcEmailManager.Inbox (Account.Id));

            UpdateFilterbar ();
            ReloadTable (); // to clear the table since the new Messages is empty
            HasLoadedOnce = false;

            SetNeedsReload ();
        }

        public bool OnOptionsItemSelected (MainTabsActivity tabActivity, IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.search:
                ShowSearch (tabActivity, item);
                return true;
            }
            return false;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            Account = NcApplication.Instance.Account;
            SetEmailMessages (NcEmailManager.Inbox (Account.Id));
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return base.OnCreateView (inflater, container, savedInstanceState);
        }

        public override void OnDestroyView ()
        {
            base.OnDestroyView ();
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowMessageCompose ();
        }

        #endregion

        #region Private Helpers 

        void ShowMessageCompose ()
        {
            var intent = MessageComposeActivity.NewMessageIntent (Activity, NcApplication.Instance.Account.Id);
            StartActivity (intent);
        }

        #endregion

        #region Search

        MessageSearchFragment SearchFragment;

        void ShowSearch (MainTabsActivity tabActivity, IMenuItem item)
        {
            tabActivity.EnterSearchMode ();
            SearchFragment = new MessageSearchFragment ();
            var searchView = (item.ActionView as SearchView);
            searchView.QueryTextChange += SearchViewQueryTextChanged;
            searchView.QueryTextSubmit += SearchViewQueryDidSubmit;
            var transaction = FragmentManager.BeginTransaction ();
            transaction.Add (Resource.Id.content, SearchFragment);
            transaction.Commit ();
        }

        void HideSearch (MainTabsActivity tabActivity, IMenuItem item)
        {
            tabActivity.ExitSearchMode ();
            var searchView = (item.ActionView as SearchView);
            searchView.QueryTextChange -= SearchViewQueryTextChanged;
            searchView.QueryTextSubmit -= SearchViewQueryDidSubmit;
            searchView.SetQuery ("", false);
            var transaction = FragmentManager.BeginTransaction ();
            transaction.Remove (SearchFragment);
            transaction.Commit ();
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Context.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);
            SearchFragment = null;
        }

        public bool OnMenuItemActionCollapse (IMenuItem item)
        {
            HideSearch ((Activity as MainTabsActivity), item);
            return true;
        }

        public bool OnMenuItemActionExpand (IMenuItem item)
        {
            var searchView = (item.ActionView as SearchView);
            searchView.RequestFocus ();
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Context.InputMethodService);
                imm.ShowSoftInput (searchView.FindFocus (), ShowFlags.Implicit);
            });
            return true;
        }

        void SearchViewQueryTextChanged (object sender, SearchView.QueryTextChangeEventArgs e)
        {
            SearchFragment.SearchForText (e.NewText);
        }

        void SearchViewQueryDidSubmit (object sender, SearchView.QueryTextSubmitEventArgs e)
        {
        }

        #endregion
    }
}
