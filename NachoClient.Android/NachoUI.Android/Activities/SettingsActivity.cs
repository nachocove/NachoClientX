using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "@string/settings", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize, ParentActivity=typeof(MainTabsActivity))]
    public class SettingsActivity : NcActivity
    {
        //private const string SETTINGS_FRAGMENT_TAG = "SettingsFragment";
        private const int SALESFORCE_REQUEST_CODE = 1;

        #region Intents

        public static Intent BuildIntent (Context context)
        {
            var intent = new Intent(context, typeof(SettingsActivity));
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        private void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        private void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.SettingsActivity);
            FindSubviews ();
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);

            //if (null == bundle || null == FragmentManager.FindFragmentByTag<SettingsFragment> (SETTINGS_FRAGMENT_TAG)) {
            //    var settingsFragment = SettingsFragment.newInstance ();
            //    FragmentManager.BeginTransaction ().Replace (Resource.Id.content, settingsFragment, SETTINGS_FRAGMENT_TAG).Commit ();
            //}
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);
            if (requestCode == SALESFORCE_REQUEST_CODE) {
                if (resultCode == Result.Ok) {
                    ShowAccountSettings (McAccount.GetSalesForceAccount ());
                }
            }
        }

        #endregion

        #region General Settings

        public void ShowUnreadCountChooser ()
        {
        }

        public void ShowAbout ()
        {
        }

        #endregion

        #region Account Settings

        public void ShowAccountSettings (McAccount account)
        {
            // FIXME: NEWUI
            //if (McAccount.AccountTypeEnum.SalesForce == account.AccountType) {
            //    StartActivity (SalesforceSettingsActivity.ShowSalesforceSettingsIntent (this, account));
            //} else {
            //    StartActivity (AccountSettingsActivity.ShowAccountSettingsIntent (this, account));
            //}
        }

        //public void ConnectToSalesforce ()
        //{
	       // StartActivityForResult (new Intent (this, typeof (SalesforceSignInActivity)), SALESFORCE_REQUEST_CODE);
        //}

        #endregion

    }
}
