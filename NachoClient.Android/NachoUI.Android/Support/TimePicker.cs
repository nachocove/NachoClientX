//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Content;
using Android.App;
using System.Globalization;

namespace NachoClient.AndroidClient
{
    public static class TimePicker
    {
        public delegate void TimeSelectedDelegate (TimeSpan time);

        public static void Show (Context context, TimeSpan initialTime, TimeSelectedDelegate selectedCallback)
        {
            EventHandler<TimePickerDialog.TimeSetEventArgs> timeSelected = delegate(object sender, TimePickerDialog.TimeSetEventArgs args) {
                if (null != selectedCallback) {
                    var pickedTime = new TimeSpan (args.HourOfDay, args.Minute, 0);
                    selectedCallback (pickedTime);
                }
            };

            bool is24Hour = !CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains ("tt");
            var dialog = new TimePickerDialog (context, timeSelected, initialTime.Hours, initialTime.Minutes, is24Hour);
            dialog.Show ();
        }
    }
}

