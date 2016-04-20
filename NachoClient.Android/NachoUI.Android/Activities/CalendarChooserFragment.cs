//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;
using Android.Widget;
using Android.Views;
using Android.OS;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class CalendarChooserFragment : DialogFragment
    {
        public delegate void CalendarSelectedDelegate (McFolder selectedFolder);

        private List<Tuple<McAccount, NachoFolders>> calendars;
        private McFolder initialSelection;
        private CalendarSelectedDelegate selectedCallback;
        private AlertDialog dialog;
        private CalendarChooserAdapter adapter;

        public void SetValues (List<Tuple<McAccount, NachoFolders>> calendars, McFolder initialSelection, CalendarSelectedDelegate selectedCallback)
        {
            this.calendars = calendars;
            this.initialSelection = initialSelection;
            this.selectedCallback = selectedCallback;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            adapter = new CalendarChooserAdapter (calendars, initialSelection);

            var view = new ListView (this.Activity);
            view.Id = Resource.Id.listView;
            view.Adapter = adapter;

            dialog = new AlertDialog.Builder (this.Activity).Create ();
            dialog.SetView (view);
            return dialog;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            dialog.FindViewById<ListView> (Resource.Id.listView).ItemClick += ItemClick;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            dialog.FindViewById<ListView> (Resource.Id.listView).ItemClick -= ItemClick;
        }

        private void ItemClick (object sender, AdapterView.ItemClickEventArgs e)
        {
            dialog.Dismiss ();
            if (null != selectedCallback) {
                selectedCallback (adapter [e.Position]);
            }
        }

        private class CalendarChooserAdapter : BaseAdapter<McFolder>
        {
            private const int ACCOUNT_CELL_TYPE = 0;
            private const int CALENDAR_CELL_TYPE = 1;
            private const int NUM_CELL_TYPES = 2;

            private List<Tuple<McAccount, McFolder>> listItems;
            private McFolder selected;

            public CalendarChooserAdapter (List<Tuple<McAccount, NachoFolders>> calendars, McFolder initialSelection)
            {
                this.selected = initialSelection;

                listItems = new List<Tuple<McAccount, McFolder>> ();
                foreach (var accountCalendars in calendars) {
                    listItems.Add (new Tuple<McAccount, McFolder> (accountCalendars.Item1, null));
                    for (int i = 0; i < accountCalendars.Item2.Count (); ++i) {
                        listItems.Add (new Tuple<McAccount, McFolder> (accountCalendars.Item1, accountCalendars.Item2.GetFolder (i)));
                    }
                }
            }

            public override int Count {
                get {
                    return listItems.Count;
                }
            }

            public override McFolder this [int index] {
                get {
                    return listItems [index].Item2;
                }
            }

            public override bool IsEnabled (int position)
            {
                return null != listItems [position].Item2;
            }

            public override int ViewTypeCount {
                get {
                    return NUM_CELL_TYPES;
                }
            }

            public override int GetItemViewType (int position)
            {
                return IsEnabled (position) ? CALENDAR_CELL_TYPE : ACCOUNT_CELL_TYPE;
            }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View cellView;
                var item = listItems [position];
                if (null == item.Item2) {
                    cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.CalendarChooserHeader, parent, false);
                    var accountName = cellView.FindViewById<TextView> (Resource.Id.calendar_chooser_header_text);
                    accountName.Text = item.Item1.DisplayName;
                } else {
                    cellView = convertView ?? LayoutInflater.From (parent.Context).Inflate (Resource.Layout.CalendarChooserCell, parent, false);
                    var icon = cellView.FindViewById<ImageView> (Resource.Id.calendar_chooser_icon);
                    if (SameFolder (item.Item2, selected)) {
                        icon.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                    } else {
                        icon.SetImageResource (Resource.Drawable.gen_checkbox);
                    }
                    var text = cellView.FindViewById<TextView> (Resource.Id.calendar_chooser_text);
                    text.Text = item.Item2.DisplayName;
                }
                return cellView;
            }

            private bool SameFolder (McFolder a, McFolder b)
            {
                var aDevice = a as AndroidDeviceCalendarFolder;
                var bDevice = b as AndroidDeviceCalendarFolder;
                if (null != aDevice && null != bDevice) {
                    return aDevice.DeviceCalendarId == bDevice.DeviceCalendarId;
                }
                return null == aDevice && null == bDevice && a.Id == b.Id;
            }
        }
    }
}

