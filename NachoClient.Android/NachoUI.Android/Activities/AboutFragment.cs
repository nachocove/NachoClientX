
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
    public class AboutFragment : Fragment
    {
        public static AboutFragment newInstance ()
        {
            var fragment = new AboutFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AboutFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            // FIXME: Un-highlight any highlighted tab bar items.

            return view;
        }
      

    }
}

