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
using Android.Support.V7.Widget;
using Android.Views.InputMethods;
using Android.Support.V4.Content;
using Android.Content.PM;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class ContactsFragment : Fragment, MainTabsActivity.Tab, ContactsAdapter.Listener, Android.Support.V4.View.MenuItemCompat.IOnActionExpandListener
    {

        private const int REQUEST_CONTACTS_PERMISSIONS = 1;

        McAccount Account;
        ContactsAdapter Adapter;
        bool CanCreateContact;

        #region Tab Interface

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            tabActivity.MenuInflater.Inflate (Resource.Menu.contacts, menu);
            var searchItem = menu.FindItem (Resource.Id.search);
            Android.Support.V4.View.MenuItemCompat.SetOnActionExpandListener (searchItem, this);
            var searchView = (searchItem.ActionView as Android.Widget.SearchView);
            searchView.SetIconifiedByDefault (false);
            return true;
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            CanCreateContact = McAccount.GetCanAddContactAccounts ().Count > 0;
            UpdateActions (tabActivity);
			StartListeningForStatusInd ();
			SetNeedsReload ();
            CheckForAndroidPermissions ();
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
		{
			StopListeningForStatusInd ();
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
            Account = NcApplication.Instance.Account;
            CanCreateContact = McAccount.GetCanAddContactAccounts ().Count > 0;
            UpdateActions (tabActivity);
        }

        private void UpdateActions (MainTabsActivity tabActivity)
        {
            if (CanCreateContact) {
                tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_contact, ActionButtonClicked);
            } else {
                tabActivity.HideActionButton ();
            }
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

        #region Subviews

        RecyclerView ListView;

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

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            Account = NcApplication.Instance.Account;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactsFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactsAdapter (this);
            ListView.SetAdapter (Adapter);
            return view;
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowNewContact ();
        }

        public override bool OnContextItemSelected (IMenuItem item)
        {
            var groupPosition = -1;
            var position = -1;
            var contactId = -1;
            if (item.Intent != null && item.Intent.HasExtra (ContactsAdapter.EXTRA_GROUP_POSITION)) {
                groupPosition = item.Intent.Extras.GetInt (ContactsAdapter.EXTRA_GROUP_POSITION);
            }
            if (item.Intent != null && item.Intent.HasExtra (ContactsAdapter.EXTRA_POSITION)) {
                position = item.Intent.Extras.GetInt (ContactsAdapter.EXTRA_POSITION);
            }
            if (item.Intent != null && item.Intent.HasExtra (ContactsAdapter.EXTRA_CONTACT_ID)) {
                contactId = item.Intent.Extras.GetInt (ContactsAdapter.EXTRA_CONTACT_ID);
            }
            if (groupPosition >= 0 && position >= 0 && contactId >= 0) {
                var contact = Adapter.GetContact (groupPosition, position);
                if (contact.Id != contactId) {
                    contact = McContact.QueryById<McContact> (contactId);
                }
                switch (item.ItemId) {
                case Resource.Id.call:
                    CallContact (contact);
                    return true;
                case Resource.Id.email:
                    EmailContact (contact);
                    return true;
                }
            }
            return base.OnContextItemSelected (item);
        }

        #endregion

        #region Reloading

        bool NeedsReload;
        bool IsReloading;

        void SetNeedsReload ()
        {
            NeedsReload = true;
            if (!IsReloading) {
                Reload ();
            }
        }

        void Reload ()
        {
            if (!IsReloading) {
                IsReloading = true;
                NeedsReload = false;
                NcTask.Run (() => {
                    // TODO: recents
                    //var recents = McContact.RicContactsSortedByRank (Account.Id, 5);
                    var contacts = McContact.AllContactsSortedByName (true);
                    var contactGroups = ContactGroup.CreateGroups (contacts, Adapter.Cache);
                    InvokeOnUIThread.Instance.Invoke (() => {
                        IsReloading = false;
                        if (NeedsReload) {
                            Reload ();
                        } else {
                            HandleReloadResults (contactGroups);
                        }
                    });
                }, "ContactsFragment.Reload");
            }
        }

        void HandleReloadResults (List<ContactGroup> contactGroups)
        {
            Adapter.SetContactGroups (contactGroups);
        }

        #endregion

        #region Search

        ContactsSearchFragment SearchFragment;

        void ShowSearch (MainTabsActivity tabActivity, IMenuItem item)
        {
            tabActivity.EnterSearchMode ();
            SearchFragment = new ContactsSearchFragment ();
            var searchView = (item.ActionView as Android.Widget.SearchView);
            searchView.QueryTextChange += SearchViewQueryTextChanged;
            searchView.QueryTextSubmit += SearchViewQueryDidSubmit;
            var transaction = FragmentManager.BeginTransaction ();
            transaction.Add (Resource.Id.content, SearchFragment);
            transaction.Commit ();
        }

        void HideSearch (MainTabsActivity tabActivity, IMenuItem item)
        {
            tabActivity.ExitSearchMode ();
            var searchView = (item.ActionView as Android.Widget.SearchView);
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
            var searchView = (item.ActionView as Android.Widget.SearchView);
            searchView.RequestFocus ();
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Context.InputMethodService);
                imm.ShowSoftInput (searchView.FindFocus (), ShowFlags.Implicit);
            });
            return true;
        }

        void SearchViewQueryTextChanged (object sender, Android.Widget.SearchView.QueryTextChangeEventArgs e)
        {
            SearchFragment.SearchForText (e.NewText);
        }

        void SearchViewQueryDidSubmit (object sender, Android.Widget.SearchView.QueryTextSubmitEventArgs e)
        {
            SearchFragment.StartServerSearch ();
        }

        #endregion

        #region Listener

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

        void ShowNewContact ()
        {
            if (Account.CanAddContact ()) {
                ShowNewContact (Account);
            } else {
                ShowAccountPicker ();
            }
        }

        void ShowAccountPicker ()
        {
            var builder = new Android.App.AlertDialog.Builder (Activity);
            builder.SetTitle (Resource.String.contact_choose_account);
            var accounts = McAccount.GetCanAddContactAccounts ();
            var items = new string [accounts.Count];
            for (var i = 0; i < accounts.Count; ++i) {
                items [i] = accounts [i].DisplayName + ": " + accounts [i].EmailAddr;
            }
            builder.SetItems (items, (sender, e) => {
                var account = accounts [e.Which];
                ShowNewContact (account);
            });
            builder.Show ();
        }

        void ShowNewContact (McAccount account)
        {
            var intent = ContactEditActivity.BuildNewIntent (Activity, account);
            StartActivity (intent);
        }

        void CallContact (McContact contact)
        {
            Util.CallNumber (Activity, contact, null);
        }

        void EmailContact (McContact contact)
        {
            var account = McAccount.EmailAccountForContact (contact);
            var intent = MessageComposeActivity.NewMessageIntent (Activity, account.Id, contact.GetPrimaryCanonicalEmailAddress ());
            StartActivity (intent);
        }

        #endregion

        #region System Events

        bool IsListeningForStatusInd;

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd){
                NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;
                IsListeningForStatusInd = true;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd){
                NcApplication.Instance.StatusIndEvent -= StatusIndEventHandler;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndEventHandler (object sender, EventArgs e)
        {
			var s = (StatusIndEventArgs)e;
			if (NcResult.SubKindEnum.Info_ContactSetChanged == s.Status.SubKind) {
				SetNeedsReload ();
			}
        }

        #endregion

        #region Permissions

        void CheckForAndroidPermissions()
        {
            // Check is always called when the calendar is selected.  The goal here is to ask only if we've never asked before
            // On Android, "never asked before" means:
            // 1. We don't have permission
            // 2. ShouldShowRequestPermissionRationale returns false
            //    (Android only instructs us to show a rationale if we've prompted once and the user has denied the request)
            bool hasAndroidReadPermission = ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.ReadContacts) == Permission.Granted;
            bool hasAndroidWritePermission = ContextCompat.CheckSelfPermission (Activity, Android.Manifest.Permission.WriteContacts) == Permission.Granted;
            if (!hasAndroidReadPermission || !hasAndroidWritePermission) {
                bool hasAskedRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
                bool hasAskedWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
                if (!hasAskedRead && !hasAskedWrite){
                    RequestAndroidPermissions ();
                }
            }
        }

        void RequestAndroidPermissions()
        {
            bool shouldAskRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
            bool shouldAskWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
            if (shouldAskRead || shouldAskWrite) {
                var builder = new Android.App.AlertDialog.Builder (Context);
                builder.SetTitle (Resource.String.contacts_permission_request_title);
                builder.SetMessage (Resource.String.contacts_permission_request_message);
                builder.SetNegativeButton (Resource.String.contacts_permission_request_cancel,(sender, e) => {});
                builder.SetPositiveButton (Resource.String.contacts_permission_request_ack, (sender, e) => {
                    RequestPermissions (new string [] {
                        Android.Manifest.Permission.ReadContacts,
                        Android.Manifest.Permission.WriteContacts
                    }, REQUEST_CONTACTS_PERMISSIONS);
                });
                builder.Show ();
            } else {
                RequestPermissions (new string [] {
                    Android.Manifest.Permission.ReadContacts,
                    Android.Manifest.Permission.WriteContacts
                }, REQUEST_CONTACTS_PERMISSIONS);
            }
        }

        public override void OnRequestPermissionsResult (int requestCode, string [] permissions, Permission [] grantResults)
        {
            if (requestCode == REQUEST_CONTACTS_PERMISSIONS){
                if (grantResults.Length == 2 && grantResults[0] == Permission.Granted && grantResults[1] == Permission.Granted){
                    BackEnd.Instance.Start (McAccount.GetDeviceAccount ().Id);
                }else{
                    // If the user denies one or both of the permissions, re-request, this time shownig our rationale.
                    bool shouldAskRead = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.ReadContacts);
                    bool shouldAskWrite = ShouldShowRequestPermissionRationale (Android.Manifest.Permission.WriteContacts);
                    if (shouldAskRead || shouldAskWrite){
                        RequestAndroidPermissions ();
                    }
                }
            }
            base.OnRequestPermissionsResult (requestCode, permissions, grantResults);
        }

        #endregion
    }

    public class ContactsAdapter : GroupedListRecyclerViewAdapter
    {

        public const string EXTRA_GROUP_POSITION = "NachoClient.AndroidClient.ContactsAdapter.EXTRA_GROUP_POSITION";
        public const string EXTRA_POSITION = "NachoClient.AndroidClient.ContactsAdapter.EXTRA_POSITION";
        public const string EXTRA_CONTACT_ID = "NachoClient.AndroidClient.ContactsAdapter.EXTRA_CONTACT_ID";

        public interface Listener
        {

            void OnContactSelected (McContact contact);

        }

        WeakReference<Listener> WeakListener;
        List<ContactGroup> ContactGroups;
        public ContactCache Cache { get; private set; }

        enum ViewType
        {
            GroupHeader,
            Contact
        }

        public ContactsAdapter (Listener listener) : base ()
        {
            ContactGroups = new List<ContactGroup> ();
            Cache = new ContactCache ();
            WeakListener = new WeakReference<Listener> (listener);
        }

        public void SetContactGroups (List<ContactGroup> contactGroups)
        {
            ContactGroups = contactGroups;
            NotifyDataSetChanged ();
        }

        public McContact GetContact (int groupPosition, int position)
        {
            var contactGroup = ContactGroups [groupPosition];
            return contactGroup.GetCachedContact (position);
        }

        public override bool HasFooters {
            get {
                return false;
            }
        }

        public override int GroupCount {
            get {
                return ContactGroups.Count;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            var contactGroup = ContactGroups [groupPosition];
            return contactGroup.Contacts.Count;
        }

        public override int GetHeaderViewType (int groupPosition)
        {
            return (int)ViewType.GroupHeader;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            return (int)ViewType.Contact;
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.GroupHeader:
                return HeaderViewHolder.Create (parent);
            case ViewType.Contact:
                var holder = ContactViewHolder.Create (parent);
                // FIXME: API
                //holder.ContentView.ContextClickable = true;
                //holder.ContentView.ContextMenuCreated += (sender, e) => {
                //    int groupPosition;
                //    int itemPosition;
                //    GetGroupPosition (holder.AdapterPosition, out groupPosition, out itemPosition);
                //    ItemContextMenuCreated (groupPosition, itemPosition, e.Menu);
                //};
                return holder;
            }
            throw new NotImplementedException ();
        }

        public override void OnBindHeaderViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
        {
            var contactGroup = ContactGroups [groupPosition];
            (holder as HeaderViewHolder).SetName (contactGroup.Name);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var contact = GetContact (groupPosition, position);
            var contactHolder = (holder as ContactViewHolder);
            contactHolder.SetContact (contact);
        }
        
        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var contact = GetContact (groupPosition, position);
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                listener.OnContactSelected (contact);
            }
        }

        void ItemContextMenuCreated (int groupPosition, int position, IContextMenu menu)
        {
            var contact = GetContact (groupPosition, position);
            var intent = new Intent ();
            intent.PutExtra (EXTRA_GROUP_POSITION, groupPosition);
            intent.PutExtra (EXTRA_POSITION, position);
            intent.PutExtra (EXTRA_CONTACT_ID, contact.Id);

            var hasEmail = contact.EmailAddresses.Count > 0;
            var hasPhone = contact.PhoneNumbers.Count > 0;

            if (hasEmail || hasPhone) {
                int order = 0;
                List<IMenuItem> items = new List<IMenuItem> ();
                if (hasPhone) {
                    items.Add (menu.Add (0, Resource.Id.call, order++, Resource.String.contact_item_action_call));
                }
                if (hasEmail) {
                    items.Add (menu.Add (0, Resource.Id.email, order++, Resource.String.contact_item_action_email));
                }
                foreach (var item in items) {
                    item.SetIntent (intent);
                }
                var name = contact.GetDisplayName ();
                if (String.IsNullOrEmpty (name)) {
                    name = contact.GetPrimaryCanonicalEmailAddress ();
                    if (String.IsNullOrEmpty (name)) {
                        name = contact.GetPrimaryPhoneNumber ();
                    }
                }
                menu.SetHeaderTitle (name);
            }
        }

        class HeaderViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            TextView NameLabel;

            public static HeaderViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactListHeaderItem, parent, false);
                return new HeaderViewHolder (view);
            }

            public HeaderViewHolder (View view) : base (view)
            {
                NameLabel = view.FindViewById (Resource.Id.name) as TextView;
            }

            public void SetName (string name)
            {
                NameLabel.Text = name;
            }

        }
    }

    public class ContactViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
    {

        public View BackgroundView { get; private set; }
        public View ContentView { get; private set; }
        PortraitView PortraitView;
        TextView NameLabel;
        TextView DetailLabel;

        public override View ClickTargetView {
            get {
                return ContentView;
            }
        }

        public static ContactViewHolder Create (ViewGroup parent)
        {
            var inflater = LayoutInflater.From (parent.Context);
            var view = inflater.Inflate (Resource.Layout.ContactListItem, parent, false);
            return new ContactViewHolder (view);
        }

        public ContactViewHolder (View view) : base (view)
        {
            BackgroundView = view.FindViewById (Resource.Id.background);
            ContentView = view.FindViewById (Resource.Id.content);
            PortraitView = view.FindViewById (Resource.Id.portrait) as PortraitView;
            NameLabel = view.FindViewById (Resource.Id.name) as TextView;
            DetailLabel = view.FindViewById (Resource.Id.detail) as TextView;
        }

        public void SetContact (McContact contact, string alternateEmail = null)
        {
            var name = contact.GetDisplayName ();
            var email = alternateEmail ?? contact.GetPrimaryCanonicalEmailAddress ();
            var phone = contact.GetPrimaryPhoneNumber ();

            if (!String.IsNullOrEmpty (name)) {
                NameLabel.Text = name;

                if (!String.IsNullOrEmpty (email)) {
                    DetailLabel.Text = email;
                } else if (!String.IsNullOrEmpty (phone)) {
                    DetailLabel.Text = phone;
                } else {
                    DetailLabel.Text = "";
                }
            } else {
                if (!String.IsNullOrEmpty (email)) {
                    NameLabel.Text = email;
                    if (!String.IsNullOrEmpty (phone)) {
                        DetailLabel.Text = phone;
                    } else {
                        DetailLabel.Text = "";
                    }
                } else if (!String.IsNullOrEmpty (phone)){
                    NameLabel.Text = phone;
                    DetailLabel.Text = "";
                } else {
                    NameLabel.Text = "Unnamed";
                    DetailLabel.Text = "";
                }
            }

            PortraitView.SetPortrait(contact.PortraitId, contact.CircleColor, NachoCore.Utils.ContactsHelper.GetInitials (contact));
        }
    }
}
