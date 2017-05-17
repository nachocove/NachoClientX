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

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class EventViewActivity : NcActivity
    {

        public const string ACTION_DELETE = "NachoClient.AndroidClient.EventViewActivity.ACTION_DELETE";
        public const string EXTRA_EVENT_ID = "NachoClient.AndroidClient.EventViewActivity.EXTRA_EVENT_ID";
        public const int REQUEST_EDIT_EVENT = 1;

        McEvent Event;

        #region Intents

        public static Intent BuildIntent (Context context, int eventId)
        {
            var intent = new Intent (context, typeof (EventViewActivity));
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        FloatingActionButton FloatingActionButton;
        EventViewFragment EventViewFragment;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            FloatingActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
            FloatingActionButton = null;
            EventViewFragment = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.EventViewActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            FloatingActionButton.Click += ActionButtonClicked;
        }

        void PopulateFromIntent ()
        {
        	var bundle = Intent.Extras;
            var eventId = bundle.GetInt (EXTRA_EVENT_ID);
            Event = McEvent.QueryById<McEvent> (eventId);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
        	base.OnAttachFragment (fragment);
            if (fragment is EventViewFragment) {
                EventViewFragment = (fragment as EventViewFragment);
                EventViewFragment.Event = Event;
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
            case REQUEST_EDIT_EVENT:
                HandleEditComplete (resultCode);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.event_view, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.delete:
                ShowDeleteConfirmation ();
                return true;
            case Resource.Id.forward:
                ShowForward ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs e)
        {
            ShowEdit ();
        }

        #endregion

        #region View Updates

        void HandleEditComplete (Result resultCode)
        {
            if (resultCode == Result.Ok) {
                Event = McEvent.QueryById<McEvent> (Event.Id);
                Update ();
            }
        }

        void Update ()
        {
            EventViewFragment.Event = Event;
            EventViewFragment.Update ();
        }

        #endregion

        #region Private Helpers

        void ShowEdit ()
        {
            var intent = EventEditActivity.BuildIntent (this, Event.Id);
            StartActivityForResult (intent, REQUEST_EDIT_EVENT);
        }

        void ShowDeleteConfirmation ()
        {
            var builder = new AlertDialog.Builder (this);
            builder.SetMessage (Resource.String.event_delete_confirmation_message);
            var items = new string [] {
                GetString (Resource.String.event_delete_confirmation_accept)
            };
            builder.SetItems (items, (sender, e) => {
                switch (e.Which) {
                case 0:
                    DeleteEvent ();
                    break;
                default:
                    break;
                }
            });
            builder.Show ();
        }

        void ShowForward ()
        {
        }

        void DeleteEvent ()
        {
            // TODO: send cancelation notices
            BackEnd.Instance.DeleteCalCmd (Event.AccountId, Event.CalendarId);
            var intent = new Intent (ACTION_DELETE);
            SetResult (Result.Ok, intent);
            Finish ();
        }

        #endregion

    }
}
