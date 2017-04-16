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
    [Activity (Label = "@string/settings_label", ParentActivity=typeof (MainTabsActivity), LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
    public class SettingsActivity : NcActivity
    {
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
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
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

        #region Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            return base.OnOptionsItemSelected (item);
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
