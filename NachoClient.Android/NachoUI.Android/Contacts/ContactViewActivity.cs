//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class ContactViewActivity : NcActivity
    {

        private const string EXTRA_CONTACT_ID = "NachoClient.AndroidClient.ContactViewActivity.EXTRA_CONTACT_ID";
        public const string ACTION_DELETE = "NachoClient.AndroidClient.ContactViewActivity.ACTION_DELETE";
        private const int REQUEST_EDIT_CONTACT = 1;

        McContact Contact;
        bool CanEditContact;

        #region Intents

        public static Intent BuildIntent (Context context, McContact contact)
        {
            var intent = new Intent (context, typeof (ContactViewActivity));
            intent.PutExtra (EXTRA_CONTACT_ID, contact.Id);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        FloatingActionButton EditActionButton;
        ContactViewFragment ContactViewFragment;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            EditActionButton = FindViewById (Resource.Id.edit) as FloatingActionButton;
            EditActionButton.Click += EditButtonClicked;
        }

        void ClearSubviews ()
        {
            EditActionButton.Click -= EditButtonClicked;
            Toolbar = null;
            EditActionButton = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.ContactViewActivity);
            FindSubviews ();
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            if (!CanEditContact) {
                EditActionButton.Hide ();
            }
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is ContactViewFragment) {
                ContactViewFragment = fragment as ContactViewFragment;
                ContactViewFragment.Contact = Contact;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_EDIT_CONTACT:
                HandleEditResult (resultCode, data);
                return;
            }
            base.OnActivityResult (requestCode, resultCode, data);
        }

        void PopulateFromIntent ()
        {
            var contactId = Intent.GetIntExtra (EXTRA_CONTACT_ID, 0);
            Contact = McContact.QueryById<McContact> (contactId);
            if (Contact == null) {
                throw new NcAssert.NachoAssertionFailure (String.Format ("Invalid contact id {0} passed to ContactViewActivity", contactId));
            }
            CanEditContact = Contact.CanUserEdit ();
        }

        #endregion

        #region Options menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.contact_view, menu);
            if (!CanEditContact) {
                var deleteItem = menu.FindItem (Resource.Id.delete);
                deleteItem.SetVisible (false);
            }
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            case Resource.Id.delete:
                ShowDeleteConfirmation ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void EditButtonClicked (object sender, EventArgs e)
        {
            ShowEdit ();
        }

        #endregion

        #region Private Helpers

        void ShowEdit ()
        {
            var intent = ContactEditActivity.BuildIntent (this, Contact);
            StartActivityForResult (intent, REQUEST_EDIT_CONTACT);
        }

        void HandleEditResult (Result result, Intent data)
        {
            if (result == Result.Ok) {
                if (data != null && data.Action == ContactEditActivity.ACTION_DELETE) {
                    var intent = new Intent (ACTION_DELETE);
                    SetResult (Result.Ok, intent);
                    Finish ();
                } else {
                    Contact = McContact.QueryById<McContact> (Contact.Id);
                    ContactViewFragment.Contact = Contact;
                    ContactViewFragment.Update ();
                }
            }
        }

        void ShowDeleteConfirmation ()
        {
            var builder = new AlertDialog.Builder (this);
            builder.SetMessage (Resource.String.contact_delete_confirmation_message);
            var items = new string [] {
                GetString (Resource.String.contact_delete_confirmation_accept)
            };
            builder.SetItems (items, (sender, e) => {
                switch (e.Which) {
                case 0:
                    DeleteContact ();
                    break;
                default:
                    break;
                }
            });
            builder.Show ();
        }

        void DeleteContact ()
        {
            // TODO: actuall delete
            var intent = new Intent (ACTION_DELETE);
            SetResult (Result.Ok, intent);
            Finish ();
        }

        #endregion
    }
}
