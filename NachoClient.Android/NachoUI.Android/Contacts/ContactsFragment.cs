﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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

using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class ContactsFragment : Fragment, MainTabsActivity.Tab, ContactsAdapter.Listener
    {

        McAccount Account;
        ContactsAdapter Adapter;

        #region Tab Interface

        public bool OnCreateOptionsMenu (MainTabsActivity tabActivity, IMenu menu)
        {
            return false;
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            tabActivity.HideActionButton ();
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
        {
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
        }

        public bool OnOptionsItemSelected (MainTabsActivity tabActivity, IMenuItem item)
        {
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

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.ContactsFragment, container, false);
            FindSubviews (view);
            Adapter = new ContactsAdapter (this);
            ListView.SetAdapter (Adapter);
            Reload ();
            return view;
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

        #region Listener

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

    public class ContactsAdapter : GroupedListRecyclerViewAdapter
    {

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
                return ContactViewHolder.Create (parent);
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
            var contactGroup = ContactGroups [groupPosition];
            var contact = contactGroup.GetCachedContact (position);
            var contactHolder = (holder as ContactViewHolder);
            contactHolder.SetContact (contact);
            contactHolder.SetClickHandler ((sender, e) => {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnContactSelected (contact);
                }
            });
            // TODO: context menu
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

        class ContactViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            PortraitView PortraitView;
            TextView NameLabel;
            TextView DetailLabel;

            EventHandler ClickHandler;

            public static ContactViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ContactListItem, parent, false);
                return new ContactViewHolder (view);
            }

            public ContactViewHolder (View view) : base (view)
            {
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

            public void SetClickHandler (EventHandler clickHandler)
            {
                if (ClickHandler != null) {
                    ItemView.Click -= ClickHandler;
                }
                ClickHandler = clickHandler;
                if (ClickHandler != null) {
                    ItemView.Click += ClickHandler;
                }
            }
        }
    }
}