//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Content;
using Android.App;

namespace NachoClient.AndroidClient
{
    public static class DatePicker
    {
        public delegate void DateSelectedDelegate (DateTime date);

        public static void Show (Context context, DateTime startDate, DateTime minDate, DateTime maxDate, DateSelectedDelegate selectedCallback)
        {
            startDate = startDate.ToLocalTime ();
            if (DateTime.MinValue != minDate) {
                minDate = minDate.ToLocalTime ();
            }
            if (DateTime.MaxValue != maxDate) {
                maxDate = maxDate.ToLocalTime ();
            }

            // A bug in DatePicker causes it to start the view at MaxDate rather than the currently
            // selected date when MinDate is too close to the selected date.  To avoid the bug,
            // force MinDate to be at least two months before the selected date.
            var twoMonthsAgo = startDate.AddMonths (-2);
            if (minDate > twoMonthsAgo) {
                minDate = twoMonthsAgo;
            }

            EventHandler<DatePickerDialog.DateSetEventArgs> dateSelected = delegate(object sender, DatePickerDialog.DateSetEventArgs args) {
                if (null != selectedCallback) {
                    var pickedDate = DateTime.SpecifyKind (args.Date.Date, DateTimeKind.Local);
                    selectedCallback (pickedDate);
                }
            };

            var dialog = new DatePickerDialog (context, dateSelected, startDate.Year, startDate.Month - 1, startDate.Day);
            var datePicker = dialog.DatePicker;
            if (DateTime.MinValue != minDate) {
                datePicker.MinDate = minDate.MillisecondsSinceEpoch ();
            }
            if (DateTime.MaxValue != maxDate) {
                datePicker.MaxDate = maxDate.MillisecondsSinceEpoch ();
            }

            dialog.Show ();
        }
    }
}

