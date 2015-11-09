//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Widget;
using NachoCore;
using NachoCore.Model;
using Android.Views;

namespace NachoClient.AndroidClient
{
    public static class CalendarChooserDialog
    {
        public delegate void CalendarSelectedDelegate (McFolder selectedFolder);

        public static void Show (Context context, List<Tuple<McAccount, NachoFolders>> calendars, McFolder initialSelection, CalendarSelectedDelegate selectedCallback)
        {
            var dialog = new AlertDialog.Builder (context).Create ();

            var view = new ListView (context);

            var adapter = new CalendarChooserAdapter (calendars, initialSelection);
            view.Adapter = adapter;

            view.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                dialog.Dismiss ();
                if (null != selectedCallback) {
                    selectedCallback (adapter [e.Position]);
                }
            };

            dialog.SetView (view);
            dialog.Show ();
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
                    if (item.Item2.Id == selected.Id) {
                        icon.SetImageResource (Resource.Drawable.gen_checkbox_checked);
                    } else {
                        icon.SetImageResource (Resource.Drawable.gen_checkbox);
                    }
                    var text = cellView.FindViewById<TextView> (Resource.Id.calendar_chooser_text);
                    text.Text = item.Item2.DisplayName;
                }
                return cellView;
            }
        }
    }
}

