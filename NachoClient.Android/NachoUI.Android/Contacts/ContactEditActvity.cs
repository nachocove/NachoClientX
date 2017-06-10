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
	public class ContactEditActivity : NcActivity
	{

		private const string EXTRA_CONTACT_ID = "NachoClient.AndroidClient.ContactEditActivity.EXTRA_CONTACT_ID";
        private const string EXTRA_ACCOUNT_ID = "NachoClient.AndroidClient.ContactEditActivity.EXTRA_ACCOUNT_ID";
        public const string ACTION_DELETE = "NachoClient.AndroidClient.ContactEditActivity.ACTION_DELETE";

        McContact Contact;

		#region Intents

		public static Intent BuildIntent (Context context, McContact contact)
		{
			var intent = new Intent (context, typeof (ContactEditActivity));
			intent.PutExtra (EXTRA_CONTACT_ID, contact.Id);
			return intent;
		}

        public static Intent BuildNewIntent (Context context, McAccount account)
        {
            var intent = new Intent (context, typeof (ContactEditActivity));
            intent.PutExtra (EXTRA_ACCOUNT_ID, account.Id);
            return intent;
        }

		#endregion

		#region Subviews

		Toolbar Toolbar;
        ContactEditFragment ContactEditFragment;

		void FindSubviews ()
		{
			Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
		}

		void ClearSubviews ()
		{
			Toolbar = null;
		}

		#endregion

		#region Activity Lifecycle

		protected override void OnCreate (Bundle savedInstanceState)
		{
			base.OnCreate (savedInstanceState);
			SetContentView (Resource.Layout.ContactEditActivity);
			FindSubviews ();
            Toolbar.Title = "";
			SetSupportActionBar (Toolbar);
			SupportActionBar.SetDisplayHomeAsUpEnabled (true);
		}

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is ContactEditFragment) {
                ContactEditFragment = fragment as ContactEditFragment;
                if (ContactEditFragment.Contact == null) {
                    PopulateFromIntent ();
                } else {
                    Contact = ContactEditFragment.Contact;
                }
            }
        }

		protected override void OnDestroy ()
		{
			ClearSubviews ();
			base.OnDestroy ();
		}

        void PopulateFromIntent ()
        {
            if (Intent.HasExtra (EXTRA_CONTACT_ID)) {
                var contactId = Intent.GetIntExtra (EXTRA_CONTACT_ID, 0);
                Contact = McContact.QueryById<McContact> (contactId);
            } else {
                var accountId = Intent.GetIntExtra (EXTRA_ACCOUNT_ID, 0);
                Contact = new McContact ();
                Contact.AccountId = accountId;
                Contact.Source = McAbstrItem.ItemSource.ActiveSync;
                Contact.CircleColor = NachoPlatform.PlatformUserColorIndex.PickRandomColorForUser ();
            }
            ContactEditFragment.Contact = Contact;
        }

		#endregion

		#region Options menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.contact_edit, menu);
            return base.OnCreateOptionsMenu (menu);
        }

		public override bool OnOptionsItemSelected (IMenuItem item)
		{
			switch (item.ItemId) {
			case Android.Resource.Id.Home:
                FinishWithSaveConfirmation ();
				return true;
            case Resource.Id.save:
                SaveAndFinish ();
                return true;
            case Resource.Id.delete:
                ShowDeleteConfirmation ();
                return true;
            }
			return base.OnOptionsItemSelected (item);
		}

		#endregion

        #region Draft Management

        private void FinishWithSaveConfirmation ()
        {
            ContactEditFragment.EndEditing ();
        	var alert = new Android.App.AlertDialog.Builder (this);
        	alert.SetItems (new string []{
				GetString (Resource.String.contact_edit_close_save),
				GetString (Resource.String.contact_edit_close_discard),
			}, (sender, e) => {
				switch (e.Which) {
				case 0:
					SaveAndFinish ();
					break;
				case 1:
					Discard ();
					break;
				}
			});
        	alert.Show ();
        }

        #endregion

        void Save ()
        {
            ContactEditFragment.Save ();
        }

        void SaveAndFinish ()
        {
            Save ();
            SetResult (Result.Ok);
            Finish ();
        }

        void Discard ()
        {
            SetResult (Result.Canceled);
            Finish ();
        }

        #region Private Helpers

        void ShowDeleteConfirmation ()
        {
            var builder = new AlertDialog.Builder (this);
            builder.SetMessage (Resource.String.contact_edit_delete_confirmation_message);
            var items = new string [] {
                GetString (Resource.String.contact_edit_delete_confirmation_accept)
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
            BackEnd.Instance.DeleteContactCmd (Contact.AccountId, Contact.Id);
            var intent = new Intent (ACTION_DELETE);
            SetResult (Result.Ok, intent);
            Finish ();
        }

		#endregion
	}
}
