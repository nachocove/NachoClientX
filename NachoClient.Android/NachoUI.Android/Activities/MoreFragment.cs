
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

namespace NachoClient.AndroidClient
{
    public class MoreFragment : Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            // Create your fragment here
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MoreFragment, container, false);

            var activity = (NcActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            return view;
        }
    }
}

