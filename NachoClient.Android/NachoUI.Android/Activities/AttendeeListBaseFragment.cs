//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    public class AttendeeListBaseFragment : Fragment
    {
        protected AttendeeListAdapter adapter;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            adapter = new AttendeeListAdapter ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AttendeeListFragment, container, false);

            var listView = view.FindViewById<SwipeMenuListView> (Resource.Id.listView);
            listView.Adapter = adapter;

            var navTitle = view.FindViewById<TextView> (Resource.Id.title);
            navTitle.Visibility = ViewStates.Visible;
            navTitle.Text = "Attendees";

            return view;
        }

        public IList<McAttendee> Attendees {
            get {
                return adapter.Attendees;
            }
            set {
                adapter.Attendees = new List<McAttendee> (value);
            }
        }
    }

    public class AttendeeListAdapter : BaseAdapter<McAttendee>
    {
        private List<McAttendee> _attendees = new List<McAttendee> ();
        public List<McAttendee> Attendees {
            get {
                return _attendees;
            }
            set {
                _attendees = value;
                NotifyDataSetChanged ();
            }
        }

        public AttendeeListAdapter ()
        {
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return _attendees.Count;
            }
        }

        public override McAttendee this[int index] {
            get {
                return _attendees [index];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View cell = convertView;
            if (null == cell) {
                cell = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttendeeListCell, parent, false);
            }
            cell.FindViewById<TextView> (Resource.Id.attendee_cell_text).Text = this [position].Email;
            return cell;
        }
    }
}

