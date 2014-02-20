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
            var listview = rootView.FindViewById<ListView> (Resource.Id.listview);

            calendarItems = new NachoCalendar ();
            adapter = new CalendarListAdapter (this.Activity, calendarItems);
            listview.Adapter = adapter;

            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_CalendarSetChanged == s.Status.SubKind) {
                    calendarItems.Refresh ();
                    adapter.NotifyDataSetChanged ();
                }
            };

            listview.ItemClick += (object sender, AdapterView.ItemClickEventArgs e) => {
                var fragment = new CalendarItemViewFragment ();
                var bundle = new Bundle ();
                var calendarItem = calendarItems.GetCalendarItem (e.Position);
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

    public class CalendarListAdapter : BaseAdapter<McCalendar>
    {
        Activity context;
        INachoCalendar calendarItems;

        public CalendarListAdapter (Activity context, INachoCalendar calendarItems) : base ()
        {
            this.context = context;
            this.calendarItems = calendarItems;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override McCalendar this [int position] {  
            get { return calendarItems.GetCalendarItem (position); }
        }

        public override int Count {
            get { return calendarItems.Count (); }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                // otherwise create a new one
                view = context.LayoutInflater.Inflate (Android.Resource.Layout.SimpleListItem2, null);
            }
            var subject = view.FindViewById<TextView> (Android.Resource.Id.Text1);
            var startDate = view.FindViewById<TextView> (Android.Resource.Id.Text2);

            var calendarItem = calendarItems.GetCalendarItem (position);
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
    }
}
