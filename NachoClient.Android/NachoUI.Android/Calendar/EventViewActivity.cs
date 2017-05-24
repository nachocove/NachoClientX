﻿//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
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
    public class EventViewActivity : NcActivity
    {

        // TODO: accept/reject/maybe

        public const string ACTION_DELETE = "NachoClient.AndroidClient.EventViewActivity.ACTION_DELETE";
        public const string EXTRA_EVENT_ID = "NachoClient.AndroidClient.EventViewActivity.EXTRA_EVENT_ID";
        public const string EXTRA_ANDROID_EVENT_ID = "NachoClient.AndroidClient.EventViewActivity.EXTRA_ANDROID_EVENT_ID";
        public const int REQUEST_EDIT_EVENT = 1;

        McEvent Event;
        bool CanEditEvent;

        #region Intents

        public static Intent BuildIntent (Context context, McEvent calendarEvent)
        {
            if (calendarEvent.DeviceEventId != 0) {
                return BuildAndroidEventIntent (context, calendarEvent.DeviceEventId);
            }
            return BuildIntent (context, calendarEvent.Id);
        }

        public static Intent BuildIntent (Context context, int eventId)
        {
            var intent = new Intent (context, typeof (EventViewActivity));
            intent.PutExtra (EXTRA_EVENT_ID, eventId);
            return intent;
        }

        public static Intent BuildAndroidEventIntent (Context context, long androidEventId)
        {
            var intent = new Intent (context, typeof (EventViewActivity));
            intent.PutExtra (EXTRA_ANDROID_EVENT_ID, androidEventId);
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
            if (!CanEditEvent) {
                FloatingActionButton.Hide ();
            }
            FloatingActionButton.Click += ActionButtonClicked;
        }

        void PopulateFromIntent ()
        {
        	var bundle = Intent.Extras;
            if (Intent.HasExtra (EXTRA_EVENT_ID)) {
                var eventId = bundle.GetInt (EXTRA_EVENT_ID);
                Event = McEvent.QueryById<McEvent> (eventId);
            } else {
                var androidEventId = bundle.GetLong (EXTRA_ANDROID_EVENT_ID);
                Event = NachoPlatform.AndroidCalendars.GetEvent (androidEventId);
            }
            CanEditEvent = CalendarHelper.CanEdit (Event);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
        	base.OnAttachFragment (fragment);
            if (fragment is EventViewFragment) {
                EventViewFragment = (fragment as EventViewFragment);
                EventViewFragment.Event = Event;
                EventViewFragment.CanEditEvent = CanEditEvent;
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
                HandleEditComplete (resultCode, data);
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
            if (!CanEditEvent) {
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

        void HandleEditComplete (Result resultCode, Intent data)
        {
            if (resultCode == Result.Ok) {
                if (data != null && data.Action == EventEditActivity.ACTION_DELETE) {
                    FinishWithDeleteAction ();
                } else {
                    Event = McEvent.QueryById<McEvent> (Event.Id);
                    Update ();
                }
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
            var calendarItem = Event.Calendar;
            if (calendarItem != null) {
                var account = McAccount.EmailAccountForCalendar (calendarItem);
                var subject = EmailHelper.CreateInitialSubjectLine (EmailHelper.Action.Forward, calendarItem.Subject);
                var message = McEmailMessage.MessageWithSubject (account, subject);
                var intent = MessageComposeActivity.ForwardCalendarIntent (this, calendarItem.Id, message);
                StartActivity (intent);
            }
        }

        void DeleteEvent ()
        {
            // TODO: send cancelation notices
            BackEnd.Instance.DeleteCalCmd (Event.AccountId, Event.CalendarId);
            FinishWithDeleteAction ();
        }

        void FinishWithDeleteAction ()
        {
            var intent = new Intent (ACTION_DELETE);
			SetResult (Result.Ok, intent);
            Finish ();
        }

        #endregion

    }
}
