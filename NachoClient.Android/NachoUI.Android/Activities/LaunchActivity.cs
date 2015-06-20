using System;

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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "LaunchActivity")]            
    public class LaunchActivity : AppCompatActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.LaunchActivity);

            var welcomeFragment = new WelcomeFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, welcomeFragment).Commit ();

        }

        public void WelcomeFinished ()
        {
            var chooseProviderFragment = new ChooseProviderFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, chooseProviderFragment).Commit ();
        }

        public void ChooseProviderFinished (McAccount.AccountServiceEnum service)
        {
            var credentialsFragment = new CredentialsFragment ();
            credentialsFragment.service = service;

            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, credentialsFragment).Commit ();
        }

        public void GoogleSignInFinished ()
        {
            var intent = new Intent ();
            intent.SetClass (this, typeof(NowActivity));
            StartActivity (intent);
        }

        public void CredentialsFinished (McAccount.AccountServiceEnum service, string emailAddress, string password)
        {
            var account = NcAccountHandler.Instance.CreateAccount (service, emailAddress, password);
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);

            // FIXME
            NcApplication.Instance.Account = account;
            BackEnd.Instance.Start (account.Id);

            var intent = new Intent ();
            intent.SetClass (this, typeof(NowActivity));
            StartActivity (intent);
        }

        public void Skip ()
        {
            var intent = new Intent ();
            intent.SetClass (this, typeof(NowActivity));
            StartActivity (intent);
        }

        public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged (newConfig);
        }

        public override void OnBackPressed ()
        {
            //            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}

