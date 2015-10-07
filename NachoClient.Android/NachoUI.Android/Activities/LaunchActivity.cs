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
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "LaunchActivity")]            
    public class LaunchActivity : AppCompatActivity
    {
        bool ReadyToStart ()
        {
            if (null == NcApplication.Instance.Account) {
                return false;
            }
            if (McAccount.AccountTypeEnum.Device == NcApplication.Instance.Account.AccountType) {
                return false;
            }
            return true;
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.LaunchActivity);

            var gettingStartedFragment = GettingStartedFragment.newInstance ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, gettingStartedFragment).Commit ();
        }

        public void GettingStartedFinished ()
        {
            var chooseProviderFragment = ChooseProviderFragment.newInstance ();
            FragmentManager.BeginTransaction ().Add(Resource.Id.content, chooseProviderFragment).AddToBackStack("ChooseProvider").Commit ();
        }

        public void ChooseProviderFinished (McAccount.AccountServiceEnum service)
        {
            var credentialsFragment = CredentialsFragment.newInstance(service);
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, credentialsFragment).AddToBackStack("Credentials").Commit ();
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

            var waitingFragment = WaitingFragment.newInstance (account);
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, waitingFragment).Commit ();
        }

        public void WaitingFinished ()
        {
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
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is GettingStartedFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
            if (f is ChooseProviderFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
            if (f is CredentialsFragment) {
                this.FragmentManager.PopBackStack (); // Let me go!
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}

