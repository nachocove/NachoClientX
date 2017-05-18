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
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity ()]
    public class EventEditActivity : Activity
    {

        public const string EXTRA_CALENDAR_ID = "NachoClient.AndroidClient.EventEditActivity.EXTRA_CALENDAR_ID";
        public const string EXTRA_START_TIME = "NachoClient.AndroidClient.EventEditActivity.EXTRA_START_TIME";

        McCalendar CalendarItem;

        #region Intents

        public static Intent BuildNewEventIntent (Context context, DateTime? start = null)
        {
            var intent = new Intent (context, typeof (EventEditActivity));
            if (start.HasValue) {
                intent.PutExtra (EXTRA_START_TIME, start.Value.ToAsUtcString());
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
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

    }
}
