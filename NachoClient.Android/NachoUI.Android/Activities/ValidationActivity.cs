﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
using Android.Widget;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "ValidationActivity")]            
    public class ValidationActivity : NcActivity, CredentialsFragmentDelegate
    {
        private const string EXTRA_ACCOUNT_ID = "com.nachocove.nachomail.EXTRA_ACCOUNT_ID";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.ValidationActivity);

            var accountId = Intent.GetIntExtra (EXTRA_ACCOUNT_ID, -1);
            var account = McAccount.QueryById<McAccount> (accountId);

            var validationFragment = ValidationFragment.newInstance (account.AccountService, account);
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, validationFragment).AddToBackStack ("Validation").Commit ();
        }

        public static Intent ValidationIntent (Context context, McAccount account)
        {
            var intent = new Intent (context, typeof(ValidationActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ACCOUNT_ID, account.Id);
            return intent;
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            SetResult (Result.Ok);
            Finish ();
        }

        // Credentials have been verified
        public void CredentialsValidated (McAccount account)
        {
            SetResult (Result.Ok);
            Finish ();
        }

    }
}
