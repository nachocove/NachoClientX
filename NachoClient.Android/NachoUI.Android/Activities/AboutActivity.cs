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
    [Activity (Label = "AboutActivity")]
    public class AboutActivity : NcTabBarActivity
    {
        private const string ABOUT_FRAGMENT_TAG = "AboutFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.AboutActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<AboutFragment> (ABOUT_FRAGMENT_TAG)) {
                var aboutFragment = AboutFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, aboutFragment, ABOUT_FRAGMENT_TAG).Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

    }
}
