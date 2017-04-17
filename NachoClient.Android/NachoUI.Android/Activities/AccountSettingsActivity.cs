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

using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    [Activity (Label = "@string/account_settings_label", ParentActivity = typeof (SettingsActivity))]
    public class AccountSettingsActivity : NcActivity
    {

        public const string EXTRA_ACCOUNT_ID = "NachoClient.AndroidClient.AccountSettingsActivity.EXTRA_ACCOUNT_ID";
        public const Result RESULT_DELETED = Result.FirstUser;

        private McAccount Account;

        #region Navigation

        public void Show ()
        {
        	var intent = new Intent (this, typeof (AccountSettingsActivity));
        	intent.SetFlags (ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            StartActivity (intent);
        }

        #endregion

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

        protected override void OnNewIntent (Intent intent)
        {
            base.OnNewIntent (intent);
            Account = McAccount.QueryById<McAccount> (Account.Id);
            var fragment = FragmentManager.FindFragmentById (Resource.Id.fragment) as AccountSettingsFragment;
            if (fragment != null) {
                fragment.Account = Account;
                fragment.Refresh ();
            }
        }

        #endregion

        #region Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            MenuInflater.Inflate (Resource.Menu.account_settings, menu);
            return base.OnCreateOptionsMenu (menu);
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Resource.Id.action_delete_account:
                DeleteAccount ();
                break;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void DeleteAccount ()
        {
            // TODO: lock UI and show activity indicator
            Action action = () => {
				NcAccountHandler.Instance.RemoveAccount (Account.Id);
                NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
        			SetResult (RESULT_DELETED);
                    Finish ();
				});
            };
            NcTask.Run (action, "RemoveAccount");
        }

        #endregion

    }

}
