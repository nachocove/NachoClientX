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
    public class CalendarActivity : NcTabBarActivity
    {
        private const string EVENT_LIST_FRAGMENT_TAG = "EventList";

        // All of the work happens in NcTabBarActivity and in EventListFragment.  The only thing that
        // happens in this class is hooking up the correct base fragment
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.CalendarActivity);

            EventListFragment fragment = null;
            if (null != bundle) {
                fragment = FragmentManager.FindFragmentByTag<EventListFragment> (EVENT_LIST_FRAGMENT_TAG);
            }
            if (null == fragment) {
                fragment = new EventListFragment ();
                fragment.StartAtToday ();
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, fragment, EVENT_LIST_FRAGMENT_TAG).Commit ();
            }
        }
    }
}
