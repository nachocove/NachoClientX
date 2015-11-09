//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Content;
using Android.App;
using Android.Views;
using Android.Widget;
using System.Collections.Generic;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public static class ReminderChooser
    {
        public delegate void ReminderSelectedDelegate (bool hasReminder, int reminder);

        public static void Show (Context context, bool hasReminder, int reminder, ReminderSelectedDelegate selectedCallback)
        {
            var dialog = new AlertDialog.Builder (context).Create ();

            var view = new ListView (context);

            var adapter = new ReminderChooserAdapter (hasReminder ? reminder : -1);
            view.Adapter = adapter;

            view.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                dialog.Dismiss ();
                if (null != selectedCallback) {
                    int selectedValue = adapter [e.Position];
                    selectedCallback (0 <= selectedValue, selectedValue);
                }
            };

            dialog.SetView (view);
            dialog.Show ();
        }

        private class ReminderChooserAdapter : BaseAdapter<int>
        {
            private int reminder;

            private List<int> reminderTimes;

            public ReminderChooserAdapter (int reminder)
            {
                if (0 > reminder) {
                    reminder = -1;
                }
                this.reminder = reminder;

                reminderTimes = new List<int> (new int[] { -1, 0, 1, 5, 15, 30, 60, 1440, 2880, 10080 });
                if (!reminderTimes.Contains (reminder)) {
                    reminderTimes.Add (reminder);
                    reminderTimes.Sort ();
                }
            }

            public override int Count {
                get {
                    return reminderTimes.Count;
                }
            }

            public override int this [int index] {
                get {
                    return reminderTimes [index];
                }
            }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View view = convertView;
                if (null == view) {
                    view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.ReminderChooserCell, parent, false);
                }

                var icon = view.FindViewById<ImageView> (Resource.Id.alert_chooser_icon);
                if (reminder == reminderTimes [position]) {
                    icon.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                } else {
                    icon.SetImageResource (Resource.Drawable.gen_checkbox);
                }

                var text = view.FindViewById<TextView> (Resource.Id.alert_chooser_text);
                text.Text = Pretty.ReminderString (0 <= reminderTimes [position], (uint)reminderTimes [position]);

                return view;
            }
        }
    }
}

