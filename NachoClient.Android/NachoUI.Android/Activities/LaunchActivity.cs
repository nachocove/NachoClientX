﻿using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "LaunchActivity", WindowSoftInputMode = Android.Views.SoftInput.AdjustResize)]            
    public class LaunchActivity : NcActivity, GettingStartedDelegate, ChooseProviderDelegate, CredentialsFragmentDelegate, WaitingFragmentDelegate
    {
        McAccount account;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.LaunchActivity);

            account = McAccount.GetAccountBeingConfigured ();

            var gettingStartedFragment = GettingStartedFragment.newInstance (account);
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, gettingStartedFragment).Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
        }

        public void GettingStartedFinished ()
        {
            if ((account != null) && (McAccount.AccountServiceEnum.None != account.AccountService)) {
                ChooseProviderFinished (account.AccountService);
            } else {
                var chooseProviderFragment = ChooseProviderFragment.newInstance ();
                var ft = FragmentManager.BeginTransaction ();
                ft.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
                ft.Add (Resource.Id.content, chooseProviderFragment).AddToBackStack ("ChooseProvider").Commit ();
            }
        }

        public void ChooseProviderFinished (McAccount.AccountServiceEnum service)
        {
            switch (service) {
            case McAccount.AccountServiceEnum.GoogleDefault:
                var googleSignInFragment = GoogleSignInFragment.newInstance (service, account);
                var ft1 = FragmentManager.BeginTransaction ();
                ft1.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
                ft1.Add (Resource.Id.content, googleSignInFragment).AddToBackStack ("GoogleSignIn").Commit ();
                break;
            default:
                var credentialsFragment = CredentialsFragment.newInstance (service, account);
                var ft2 = FragmentManager.BeginTransaction ();
                ft2.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
                ft2.Add (Resource.Id.content, credentialsFragment).AddToBackStack ("Credentials").Commit ();
                break;
            }
        }

        // Credentials have been verified
        public void CredentialsValidated (McAccount account)
        {
            var waitingFragment = WaitingFragment.newInstance (account);
            var ft = FragmentManager.BeginTransaction ();
            ft.SetCustomAnimations (Resource.Animation.fade_in, Resource.Animation.fade_out, Resource.Animation.fade_in, Resource.Animation.fade_out);
            ft.Add (Resource.Id.content, waitingFragment).AddToBackStack ("Waiting").Commit ();
        }

        public void WaitingFinished (McAccount account)
        {
            Log.Info (Log.LOG_UI, "LaunchActivity syncing complete");
            if (null != account) {
                NcApplication.Instance.Account = account;
                LoginHelpers.SetSwitchToTime (account);
            }
            var intent = NcTabBarActivity.HotListIntent(this);
            StartActivity (intent);
        }

        public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged (newConfig);
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is GettingStartedFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
            if (f is ChooseProviderFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
            if (f is CredentialsFragment) {
                ((CredentialsFragment)f).OnBackPressed ();
                this.FragmentManager.PopBackStack (); // Let me go!
            }
            if (f is GoogleSignInFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}

