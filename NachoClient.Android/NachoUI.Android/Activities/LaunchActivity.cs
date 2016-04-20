using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "LaunchActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class LaunchActivity : NcActivity, GettingStartedDelegate
    {
        private const string GETTING_STARTED_FRAGMENT_TAG = "GettingStarted";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.LaunchActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<GettingStartedFragment> (GETTING_STARTED_FRAGMENT_TAG)) {
                var gettingStartedFragment = GettingStartedFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, gettingStartedFragment, GETTING_STARTED_FRAGMENT_TAG).Commit ();
            }
        }

        public void GettingStartedFinished ()
        {
            StartActivity (new Intent (this, typeof(AddAccountActivity)));
        }

    }
}

