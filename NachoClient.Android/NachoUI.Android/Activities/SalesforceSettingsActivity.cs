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
using Android.Widget;
using NachoCore.Model;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "SalesforceSettingsActivity")]
    public class SalesforceSettingsActivity : NcActivityWithData<McAccount>, IAccountSettingsFragmentOwner
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
            SetContentView (Resource.Layout.SalesforceSettingsActivity);
        }

        public static Intent ShowSalesforceSettingsIntent (Context context, McAccount account)
        {
            var intent = new Intent (context, typeof(SalesforceSettingsActivity));
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
}
