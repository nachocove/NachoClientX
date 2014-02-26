using Android.OS;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V7.App;
using Android.Support.V4.Widget;
using Android.Widget;
using NachoCore.Model;
using NachoCore;
using NachoClient;
using NachoCore.Utils;
using System;
using System.Linq;
using Android.App;

namespace NachoClient.AndroidClient
{
    public class CalendarListFragment : Android.Support.V4.App.Fragment
    {
        INachoCalendar calendarItems;
        CalendarListAdapter adapter;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.CalendarListFragment, container, false);
            var listview = rootView.FindViewById<ExpandableListView> (Resource.Id.listview);

            calendarItems = new NachoCalendar ();
            adapter = new CalendarListAdapter (this.Activity, calendarItems);
            listview.SetAdapter (adapter);

            // Ignore clicks
            listview.GroupClick += (object sender, ExpandableListView.GroupClickEventArgs e) => {
                ; // ignore group clicks
            };
            // Remove the open/close icon
            listview.SetGroupIndicator (null);

            // Need to dump expandable view to remove this kludge!
            for (var i = 0; i < calendarItems.NumberOfDays (); i++) {
                listview.ExpandGroup (i);
            }

            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_CalendarSetChanged == s.Status.SubKind) {
                    calendarItems.Refresh ();
                    adapter.NotifyDataSetChanged ();
                }
            };

            listview.ChildClick += (object sender, ExpandableListView.ChildClickEventArgs e) => {
                var fragment = new CalendarItemViewFragment ();
                var bundle = new Bundle ();
                var calendarItem = calendarItems.GetCalendarItem (e.GroupPosition, e.ChildPosition);
                bundle.PutInt ("accountId", calendarItem.AccountId);
                bundle.PutInt ("calendarItemId", calendarItem.Id);
                bundle.PutString ("segue", "CalendarListToCalendarItemView");
                fragment.Arguments = bundle;
                Activity.SupportFragmentManager.BeginTransaction ()
                    .Add (Resource.Id.content_frame, fragment)
                    .AddToBackStack (null)
                    .Commit ();
            };

            Activity.Title = "Calendars";
            return rootView;
        }
    }

    public class CalendarListAdapter : BaseExpandableListAdapter
    {
        Activity context;
        INachoCalendar calendarItems;

        public CalendarListAdapter (Activity context, INachoCalendar calendarItems) : base ()
        {
            this.context = context;
            this.calendarItems = calendarItems;
        }

        public override View GetGroupView (int groupPosition, bool isExpanded, View convertView, ViewGroup parent)
        {
            if (null == convertView) {
                convertView = context.LayoutInflater.Inflate (Resource.Layout.CalendarListGroup, null);
            }
            var header = convertView.FindViewById<TextView> (Resource.Id.header);
            var date = calendarItems.GetDayDate (groupPosition);
            header.Text = date.ToString ("D");
            return convertView;
        }

        public override View GetChildView (int groupPosition, int childPosition, bool isLastChild, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Android.Resource.Layout.SimpleListItem2, null);
            }
            var subject = view.FindViewById<TextView> (Android.Resource.Id.Text1);
            var startDate = view.FindViewById<TextView> (Android.Resource.Id.Text2);

            var calendarItem = calendarItems.GetCalendarItem (groupPosition, childPosition);
            subject.Text = ConvertToPrettySubjectString (calendarItem.Subject);
            startDate.Text = calendarItem.StartTime.ToString ();

            return view;
        }

        string ConvertToPrettySubjectString (String Subject)
        {
            if (null == Subject) {
                return "";
            } else {
                return Subject;
            }
        }

        public override int GetChildrenCount (int groupPosition)
        {
            return calendarItems.NumberOfItemsForDay (groupPosition);
        }

        public override int GroupCount {
            get { return calendarItems.NumberOfDays (); }
        }

        #region implemented abstract members of BaseExpandableListAdapter

        public override Java.Lang.Object GetChild (int groupPosition, int childPosition)
        {
            throw new NotImplementedException ();
        }

        public override long GetChildId (int groupPosition, int childPosition)
        {
            return childPosition;
        }

        public override Java.Lang.Object GetGroup (int groupPosition)
        {
            throw new NotImplementedException ();
        }

        public override long GetGroupId (int groupPosition)
        {
            return groupPosition;
        }

        public override bool IsChildSelectable (int groupPosition, int childPosition)
        {
            return true;
        }

        public override bool HasStableIds {
            get {
                return true;
            }
        }

        #endregion

    }
}
