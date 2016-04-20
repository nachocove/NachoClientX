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
        private const int SALESFORCE_REQUEST_CODE = 1;

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
            if (McAccount.AccountTypeEnum.SalesForce == account.AccountType) {
                StartActivity (SalesforceSettingsActivity.ShowSalesforceSettingsIntent (this, account));
            } else {
                StartActivity (AccountSettingsActivity.ShowAccountSettingsIntent (this, account));
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public void ConnectToSalesforce()
        {
            StartActivityForResult (new Intent (this, typeof(SalesforceSignInActivity)), SALESFORCE_REQUEST_CODE);
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);
            if (requestCode == SALESFORCE_REQUEST_CODE) {
                if (resultCode == Result.Ok) {
                    AccountSettingsSelected (McAccount.GetSalesForceAccount ());
                }
            }
        }

    }
}
