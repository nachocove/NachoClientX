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
    [Activity (Label = "SalesforceSignInActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]
    public class SalesforceSignInActivity : NcActivity
    {
        private const string SIGNIN_FRAGMENT_TAG = "SignInFragment";

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.SalesforceSignInActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<SupportFragment> (SIGNIN_FRAGMENT_TAG)) {
                var account = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.SalesForce).FirstOrDefault ();
                var signInFragment = SalesforceSignInFragment.newInstance (account);
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, signInFragment, SIGNIN_FRAGMENT_TAG).Commit ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

    }
}
