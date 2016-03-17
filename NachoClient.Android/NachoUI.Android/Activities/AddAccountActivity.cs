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
    [Activity (Label = "AddAccountActivity")]
    public class AddAccountActivity : NcActivity, ChooseProviderDelegate, CredentialsFragmentDelegate, WaitingFragmentDelegate
    {
        private const string CHOOSE_PROVIDER_FRAGMENT_TAG = "ChooseProvider";
        private const string CREDENTIALS_FRAGMENT_TAG = "Credentials";
        private const string WAITING_FRAGMENT_TAG = "Waiting";

        McAccount account;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.AddAccountActivity);

            if ((bundle != null) && (null != FragmentManager.FindFragmentByTag<ChooseProviderFragment> (CHOOSE_PROVIDER_FRAGMENT_TAG))) {
                return;
            }

            if ((bundle != null) && (null != FragmentManager.FindFragmentByTag<CredentialsFragment> (CREDENTIALS_FRAGMENT_TAG))) {
                return;
            }

            if ((bundle != null) && (null != FragmentManager.FindFragmentByTag<WaitingFragment> (WAITING_FRAGMENT_TAG))) {
                return;
            }
                
            account = McAccount.GetAccountBeingConfigured ();
            if (null == account) {
                var chooseProviderFragment = ChooseProviderFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, chooseProviderFragment, CHOOSE_PROVIDER_FRAGMENT_TAG).Commit ();
            } else {
                ChooseProviderFinished (account.AccountService);
            }
        }

        public void ChooseProviderFinished (McAccount.AccountServiceEnum service)
        {
            Fragment fragment;
            switch (service) {
            case McAccount.AccountServiceEnum.GoogleDefault:
                fragment = GoogleSignInFragment.newInstance (service, account);
                break;
            default:
                fragment = CredentialsFragment.newInstance (service, account);
                break;
            }
            var ft = FragmentManager.BeginTransaction ();
            ft.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
            ft.Add (Resource.Id.content, fragment, CREDENTIALS_FRAGMENT_TAG).AddToBackStack (CREDENTIALS_FRAGMENT_TAG).Commit ();
        }

        public void CredentialsValidated (McAccount account)
        {
            var waitingFragment = WaitingFragment.newInstance (account);
            var ft = FragmentManager.BeginTransaction ();
            ft.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
            ft.Add (Resource.Id.content, waitingFragment, WAITING_FRAGMENT_TAG).AddToBackStack (WAITING_FRAGMENT_TAG).Commit ();
        }

        public void WaitingFinished (McAccount account)
        {
            Log.Info (Log.LOG_UI, "LaunchActivity syncing complete");
            if (null != account) {
                LoginHelpers.SetSwitchAwayTime (NcApplication.Instance.Account.Id);
                NcApplication.Instance.Account = account;
            }
            var intent = NcTabBarActivity.HotListIntent (this);
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
            Finish ();
        }

        public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged (newConfig);
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is NcFragment) {
                ((NcFragment)f).OnBackPressed ();
            }
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

    }
}
