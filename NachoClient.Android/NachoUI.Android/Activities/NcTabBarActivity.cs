using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;
using Android.Graphics;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class NcTabBarActivity : AppCompatActivity, ChooseProviderDelegate, CredentialsFragmentDelegate, WaitingFragmentDelegate, AccountListDelegate
    {
        private string ClassName;

        MoreFragment moreFragment = new MoreFragment ();
        SwitchAccountFragment switchAccountFragment = new SwitchAccountFragment ();

        protected void OnCreate (Bundle bundle, int layoutId)
        {
            ClassName = this.GetType ().Name;

            base.OnCreate (bundle);
            SetContentView (layoutId);
        }

        protected override void OnStart ()
        {
            base.OnStart ();
            NachoCore.Utils.NcAbate.HighPriority ("NcActivity OnStart");
        }

        protected override void OnResume ()
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_BEGIN");
            base.OnResume ();
            NachoCore.Utils.NcAbate.RegularPriority ("NcActivity OnResume");
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        protected override void OnPause ()
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.OnPause ();
            NachoCore.Utils.NcAbate.RegularPriority ("NcActivity OnPause");
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_END");
        }

        public void HookNavigationToolbar (Android.Views.View view)
        {
            var hotButton = view.FindViewById<Android.Views.View> (Resource.Id.hot);
            hotButton.Click += HotButton_Click;

            var inboxButton = view.FindViewById<Android.Views.View> (Resource.Id.inbox);
            inboxButton.Click += InboxButton_Click;

            var calendarButton = view.FindViewById<Android.Views.View> (Resource.Id.calendar);
            calendarButton.Click += CalendarButton_Click;

            var contactsButton = view.FindViewById<Android.Views.View> (Resource.Id.contacts);
            contactsButton.Click += ContactsButton_Click;

            var moreButton = view.FindViewById<Android.Views.View> (Resource.Id.more);
            moreButton.Click += MoreButton_Click;

            HookSwitchAccountView (view);
        }

        public void HookSwitchAccountView (Android.Views.View view)
        {
            var switchAccountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            switchAccountButton.Click += SwitchAccountButton_Click;
            SetSwitchAccountButtonImage (view);
        }

        public void SetSwitchAccountButtonImage (Android.Views.View view)
        {
            var switchAccountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            switchAccountButton.SetImageResource (Util.GetAccountServiceImageId (NcApplication.Instance.Account.AccountService));
        }

        void SwitchAccountButton_Click (object sender, EventArgs e)
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                FragmentManager.PopBackStack ();
            } else {
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, switchAccountFragment).AddToBackStack (null).Commit ();
            }
        }

        protected bool MaybePopMoreFragment ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MoreFragment) {
                FragmentManager.PopBackStack ();
                return true;
            } else {
                return false;
            }
        }

        void MoreButton_Click (object sender, EventArgs e)
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MoreFragment) {
                // Already displayed
                return;
            }
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, moreFragment).AddToBackStack (null).Commit ();
        }

        void HotButton_Click (object sender, EventArgs e)
        {
            MaybePopMoreFragment ();

            if (this is NowListActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(NowListActivity));
            StartActivity (intent);
        }

        void InboxButton_Click (object sender, EventArgs e)
        {
            MaybePopMoreFragment ();

            if (this is InboxActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(InboxActivity));
            StartActivity (intent);
        }

        void ContactsButton_Click (object sender, EventArgs e)
        {
            MaybePopMoreFragment ();

            if (this is ContactsActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(ContactsActivity));
            StartActivity (intent);
        }

        void CalendarButton_Click (object sender, EventArgs e)
        {
            MaybePopMoreFragment ();

            if (this is CalendarActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(CalendarActivity));
            StartActivity (intent);
        }

        public void AddAccount ()
        {
            var chooseProviderFragment = ChooseProviderFragment.newInstance ();
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, chooseProviderFragment).AddToBackStack ("ChooseProvider").Commit ();
        }

        public void ChooseProviderFinished (McAccount.AccountServiceEnum service)
        {
            switch (service) {
            case McAccount.AccountServiceEnum.GoogleDefault:
                var googleSignInFragment = GoogleSignInFragment.newInstance (service, null);
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, googleSignInFragment).AddToBackStack ("GoogleSignIn").Commit ();
                break;
            default:
                var credentialsFragment = CredentialsFragment.newInstance (service, null);
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, credentialsFragment).AddToBackStack ("Credentials").Commit ();
                break;
            }
        }

        // Credentials have been verified
        public void CredentialsValidated (McAccount account)
        {
            var waitingFragment = WaitingFragment.newInstance (account);
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, waitingFragment).AddToBackStack ("Waiting").Commit ();
        }

        public void WaitingFinished (McAccount account)
        {
            Log.Info (Log.LOG_UI, "NcActivity syncing complete");
            if (null != account) {
                NcApplication.Instance.Account = account;
                LoginHelpers.SetSwitchToTime (account);
            }
            FragmentManager.PopBackStack ("ChooseProvider", PopBackStackFlags.Inclusive);
        }

        public virtual void SwitchAccount (McAccount account)
        {
        }

        // Callback from account switcher
        public void AccountSelected (McAccount account)
        {
            Log.Info (Log.LOG_UI, "NcActivity account selected {0}", account.DisplayName);
            SwitchAccount (account);

            // Pop the switcher if the activity hasn't already done it.
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                FragmentManager.PopBackStack ();
            } 
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                this.FragmentManager.PopBackStack ();
            }
            if (f is ChooseProviderFragment) {
                this.FragmentManager.PopBackStack ();
            }
            if (f is CredentialsFragment) {
                ((CredentialsFragment)f).OnBackPressed ();
                this.FragmentManager.PopBackStack ();
            }
            if (f is GoogleSignInFragment) {
                this.FragmentManager.PopBackStack ();
            }
            if (f is WaitingFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

    }
}

