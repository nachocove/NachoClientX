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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "CalendarActivity")]            
    public class CalendarActivity : NcActivity
    {
        EventViewFragment eventViewFragment;
        EventListFragment eventListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.CalendarActivity);

            eventListFragment = EventListFragment.newInstance ();
            eventListFragment.onEventClick += EventListFragment_onEventClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, eventListFragment).AddToBackStack ("Events").Commit ();
        }

        void EventListFragment_onEventClick (object sender, McEvent ev)
        {
            Log.Info (Log.LOG_UI, "EventListFragment_onEventClick: {0}", ev);
            eventViewFragment = EventViewFragment.newInstance (ev);
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, eventViewFragment).AddToBackStack ("View").Commit ();
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is EventViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
