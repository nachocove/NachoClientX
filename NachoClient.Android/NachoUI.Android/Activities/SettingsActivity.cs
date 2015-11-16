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
    [Activity (Label = "SettingsActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]            
    public class SettingsActivity : NcTabBarActivity
    {
        private const string SETTINGS_FRAGMENT_TAG = "SettingsFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.SettingsActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<SettingsFragment> (SETTINGS_FRAGMENT_TAG)) {
                var settingsFragment = SettingsFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, settingsFragment, SETTINGS_FRAGMENT_TAG).Commit ();
            }
        }

        public void AccountSettingsSelected (McAccount account)
        {
            var accountSettingsFragment = AccountSettingsFragment.newInstance (account);
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, accountSettingsFragment).AddToBackStack ("AccountSettings").Commit ();
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

        public override void SwitchAccount (McAccount account)
        {
            base.SwitchAccount (account);
        }

    }
}
