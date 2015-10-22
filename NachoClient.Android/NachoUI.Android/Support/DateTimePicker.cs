//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using Android.Content;
using Android.Views;
using Android.Widget;
using NachoCore.Utils;
using System.Globalization;

namespace NachoClient.AndroidClient
{
    public static class DateTimePicker
    {
        public delegate bool DateValidationDelegate (DateTime date);
        public delegate void DateSelectedDelegate (DateTime date);

        public static void Show (
            Context context, DateTime startDate, bool showTimePicker, DateTime minDate, DateTime maxDate,
            DateValidationDelegate validationCallback, DateSelectedDelegate selectedCallback)
        {
            // The date and time pickers work in local time, not UTC.  ToLocalTime() is a no-op
            // if the DateTime is already local, so this allows the caller to pass in either kind.
            startDate = startDate.ToLocalTime ();
            minDate = minDate.ToLocalTime ();
            maxDate = maxDate.ToLocalTime ();

            var twoMonthsAgo = startDate.AddMonths (-2);
            if (minDate > twoMonthsAgo) {
                // A bug in DatePicker causes it to start the view at MaxDate rather than the currently
                // selected time when MinDate is too close to the selected date.  To avoid this bug,
                // force MinDate to be at least two months before the selected date.
                minDate = twoMonthsAgo;
            }

            // The date and time pickers will be part of an alert-style dialog.
            var dialog = new AlertDialog.Builder (context).Create ();

            var inflater = (LayoutInflater)context.GetSystemService (Context.LayoutInflaterService);
            var view = inflater.Inflate (Resource.Layout.DateTimePicker, null);

            var datePicker = view.FindViewById<DatePicker> (Resource.Id.date_picker);
            datePicker.MinDate = minDate.MillisecondsSinceEpoch ();
            datePicker.MaxDate = maxDate.MillisecondsSinceEpoch ();
            datePicker.UpdateDate (startDate.Year, startDate.Month - 1, startDate.Day);

            var timePicker = view.FindViewById<TimePicker> (Resource.Id.time_picker);
            timePicker.CurrentHour = (Java.Lang.Integer)startDate.Hour;
            timePicker.CurrentMinute = (Java.Lang.Integer)startDate.Minute;
            timePicker.SetIs24HourView (new Java.Lang.Boolean (!CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains ("tt")));
            timePicker.Visibility = showTimePicker ? ViewStates.Visible : ViewStates.Gone;

            var setButton = view.FindViewById<Button> (Resource.Id.date_time_set);
            setButton.Click += (object sender, EventArgs e) => {
                var newDateTime = (datePicker.DateTime.Date + new TimeSpan ((int)timePicker.CurrentHour, (int)timePicker.CurrentMinute, 0)).ToUniversalTime ();
                if (null == validationCallback || validationCallback (newDateTime)) {
                    dialog.Dismiss ();
                    if (null != selectedCallback) {
                        selectedCallback (newDateTime);
                    }
                }
            };

            dialog.SetView (view);
            dialog.Show ();
        }
    }
}

