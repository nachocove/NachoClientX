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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class EventEditActivity : NcActivity
    {
        
        public const string ACTION_DELETE = "NachoClient.AndroidClient.EventEditActivity.ACTION_DELETE";
        public const string EXTRA_CALENDAR_ID = "NachoClient.AndroidClient.EventEditActivity.EXTRA_CALENDAR_ID";
        public const string EXTRA_START_TIME = "NachoClient.AndroidClient.EventEditActivity.EXTRA_START_TIME";

        McCalendar CalendarItem;

        #region Intents

        public static Intent BuildNewEventIntent (Context context, DateTime? start = null)
        {
            var intent = new Intent (context, typeof (EventEditActivity));
            if (start.HasValue) {
                intent.PutExtra (EXTRA_START_TIME, start.Value.ToAsUtcString ());
            }
            return intent;
        }

        public static Intent BuildIntent (Context context, int calendarId)
        {
            var intent = new Intent (context, typeof (EventEditActivity));
            intent.PutExtra (EXTRA_CALENDAR_ID, calendarId);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        EventEditFragment EventEditFragment;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
            EventEditFragment = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.EventEditActivity);
            FindSubviews ();
            Toolbar.Title = "";
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        void PopulateFromIntent ()
        {
            if (Intent.HasExtra (EXTRA_CALENDAR_ID)) {
                var calendarId = Intent.Extras.GetInt (EXTRA_CALENDAR_ID);
                CalendarItem = McCalendar.QueryById<McCalendar> (calendarId);
            } else if (Intent.HasExtra (EXTRA_START_TIME)) {
                var dateTimeString = Intent.Extras.GetString (EXTRA_START_TIME);
                var startTime = dateTimeString.ToDateTime ();
                CalendarItem = new McCalendar ();
                CalendarItem.StartTime = startTime;
            } else {
                var now = DateTime.Now;
                var startTime = DateTime.Today.AddHours (now.Hour);
                CalendarItem = new McCalendar ();
                CalendarItem.StartTime = startTime;
            }
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is EventEditFragment) {
                EventEditFragment = fragment as EventEditFragment;
                EventEditFragment.CalendarItem = CalendarItem;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.event_edit, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                FinishWithSaveConfirmation ();
                return true;
            case Resource.Id.delete:
                ShowDeleteConfirmation ();
                return true;
            case Resource.Id.save:
                SaveAndFinish ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        public override void OnBackPressed ()
        {
        	FinishWithSaveConfirmation ();
        }

        #endregion

        #region Draft Management

        private void FinishWithSaveConfirmation ()
        {
            EventEditFragment.EndEditing ();
            var alert = new Android.App.AlertDialog.Builder (this);
            alert.SetItems (new string []{
                GetString (Resource.String.event_edit_close_save),
                GetString (Resource.String.event_edit_close_discard),
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

        public void Discard ()
        {
            SetResult (Result.Canceled);
            Finish ();
        }

        public void Save ()
        {
            // TODO: save changes to DB
        }

        public void SaveAndFinish ()
        {
            Save ();
            SetResult (Result.Ok);
            Finish ();
        }

        #endregion

        #region Private Helpers

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

        void DeleteEvent ()
        {
            // TODO: send cancelation notices
            BackEnd.Instance.DeleteCalCmd (CalendarItem.AccountId, CalendarItem.Id);
        	var intent = new Intent (ACTION_DELETE);
        	SetResult (Result.Ok, intent);
        	Finish ();
        }

        #endregion

    }
}
