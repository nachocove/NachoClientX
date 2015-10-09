
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
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class StartupFragment : Fragment
    {
        // Just shows "Let's get started"
        public static StartupFragment newInstance ()
        {
            var fragment = new StartupFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.StartupFragment, container, false);
            return view;
        }

    }
}

