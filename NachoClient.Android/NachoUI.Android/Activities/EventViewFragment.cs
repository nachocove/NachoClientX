
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
    public class EventViewFragment : Fragment
    {
        McEvent ev;

        public static EventViewFragment newInstance (McEvent ev)
        {
            var fragment = new EventViewFragment ();
            fragment.ev = ev;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.EventViewFragment, container, false);
            var tv = view.FindViewById<TextView> (Resource.Id.textview);
            tv.Text = "EventView fragment";
            return view;
        }
    }
}

