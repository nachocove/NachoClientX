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
    [Activity (Label = "SupportActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class SupportActivity : NcTabBarActivity
    {
        private const string SUPPORT_FRAGMENT_TAG = "SupportFragment";

        public static string HIDE_TOOLBAR = "HideToolbar";

        public static Intent IntentWithoutToolbar(Context context)
        {
            var intent = new Intent ();
            intent.SetClass (context, typeof(SupportActivity));
            intent.PutExtra (HIDE_TOOLBAR, true);
            return intent;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.SupportActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<SupportFragment> (SUPPORT_FRAGMENT_TAG)) {
                var supportFragment = SupportFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, supportFragment, SUPPORT_FRAGMENT_TAG).Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            if (0 < FragmentManager.BackStackEntryCount) {
                FragmentManager.PopBackStack ();
            } else {
                base.OnBackPressed ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public void EmailSupportClick ()
        {
            var supportMessageFragment = SupportMessageFragment.newInstance ();
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, supportMessageFragment).AddToBackStack ("Message").Commit ();
        }

        public void MessageSentCallback ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SupportMessageFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

    }
}
