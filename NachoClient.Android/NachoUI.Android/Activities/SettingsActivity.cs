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

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.SettingsActivity);

            var settingsFragment = SettingsFragment.newInstance ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, settingsFragment).Commit ();
        }

        public void AccountSettingsSelected(McAccount account)
        {
            var accountSettingsFragment = AccountSettingsFragment.newInstance (account);
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, accountSettingsFragment).AddToBackStack ("AccountSettings").Commit ();
        }
            
        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if(f is AccountSettingsFragment) {
                this.FragmentManager.PopBackStack ();
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
