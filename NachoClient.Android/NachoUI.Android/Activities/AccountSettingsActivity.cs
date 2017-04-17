//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

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
using NachoCore.Model;

namespace NachoClient.AndroidClient
{

    [Activity (Label="@string/account_settings_label", ParentActivity=typeof(SettingsActivity))]
    public class AccountSettingsActivity : NcActivity
    {

        public const string EXTRA_ACCOUNT_ID = "NachoClient.AndroidClient.AccountSettingsActivity.EXTRA_ACCOUNT_ID";
        public const Result RESULT_DELETED = Result.FirstUser;

        private McAccount Account;

        #region Intents

        public static Intent BuildIntent (Context context, int accountId)
        {
            var intent = new Intent (context, typeof (AccountSettingsActivity));
            intent.PutExtra (EXTRA_ACCOUNT_ID, accountId);
            return intent;
        }

        private void PopulateFromIntent ()
        {
            var accountId = Intent.Extras.GetInt (EXTRA_ACCOUNT_ID);
            Account = McAccount.QueryById<McAccount> (accountId);
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

            PopulateFromIntent ();

            SetContentView (Resource.Layout.AccountSettingsActivity);
            FindSubviews ();

            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is AccountSettingsFragment) {
                (fragment as AccountSettingsFragment).Account = Account;
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
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

        #region User Actions

        void DeleteAccount ()
        {
            SetResult (RESULT_DELETED);
            Finish ();
        }

        #endregion

    }

    /*

    [Activity (Label = "AccountSettingsActivity")]
    public class AccountSettingsActivity : NcActivityWithData<McAccount>, IAccountSettingsFragmentOwner
    {
        private const string EXTRA_ACCOUNT = "com.nachocove.nachomail.EXTRA_ACCOUNT";

        private McAccount account;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            var retainedAccount = RetainedData;
            if (null == retainedAccount) {
                retainedAccount = IntentHelper.RetrieveValue<McAccount> (Intent.GetStringExtra (EXTRA_ACCOUNT));
                RetainedData = retainedAccount;
            }
            account = retainedAccount;
            SetContentView (Resource.Layout.AccountSettingsActivity);
        }

        public static Intent ShowAccountSettingsIntent (Context context, McAccount account)
        {
            var intent = new Intent (context, typeof(AccountSettingsActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ACCOUNT, IntentHelper.StoreValue (account));
            return intent;
        }

        McAccount IAccountSettingsFragmentOwner.AccountToView {
            get {
                return this.account;
            }
        }
    }

    */
}
