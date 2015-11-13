
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

            var fmt = GetString (Resource.String.version_string);
            var versionView = view.FindViewById<TextView> (Resource.Id.version);
            versionView.Text = String.Format (fmt, NcApplication.GetVersionString ());

            // Highlight the tab bar icon of this activity
            var moreImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);

            return view;
        }
      

    }
}

