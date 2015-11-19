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
    public class NcTabBarActivity : NcActivity, ChooseProviderDelegate, CredentialsFragmentDelegate, WaitingFragmentDelegate, AccountListDelegate
    {
        SwitchAccountFragment switchAccountFragment = new SwitchAccountFragment ();

        protected void OnCreate (Bundle bundle, int layoutId)
        {
            base.OnCreate (bundle);
            SetContentView (layoutId);
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

        void MoreButton_Click (object sender, EventArgs e)
        {
            if (this is MoreActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(MoreActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        public static Intent HotListIntent (Context context)
        {
            if (LoginHelpers.ShowHotCards ()) {
                return new Intent (context, typeof(NowActivity));
            } else {
                return new Intent (context, typeof(NowListActivity));
            }
        }

        void HotButton_Click (object sender, EventArgs e)
        {
            var intent = HotListIntent (this);
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        void InboxButton_Click (object sender, EventArgs e)
        {
            if (this is InboxActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(InboxActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        void ContactsButton_Click (object sender, EventArgs e)
        {
            if (this is ContactsActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(ContactsActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        void CalendarButton_Click (object sender, EventArgs e)
        {
            if (this is CalendarActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(CalendarActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
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
            LoginHelpers.SetSwitchToTime (account);

            // Pop the switcher if the activity hasn't already done it.
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                FragmentManager.PopBackStack ();
            } 
        }

        public void AccountShortcut (int shortcut)
        {
            if (Resource.Id.account_settings == shortcut) {
                StartActivity (AccountSettingsActivity.ShowAccountSettingsIntent (this, NcApplication.Instance.Account));
                return;
            }
            // Pop the switcher if the activity hasn't already done it.
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                FragmentManager.PopBackStack ();
            }
            switch (shortcut) {
            case Resource.Id.go_to_inbox:
                {
                    if (this is InboxActivity) {
                        return;
                    } 
                    var intent = new Intent ();
                    intent.SetClass (this, typeof(InboxActivity));
                    intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
                    StartActivity (intent);
                }
                break;
            case Resource.Id.go_to_deferred:
                {
                    var folder = McFolder.GetDeferredFakeFolder ();
                    var intent = DeferredActivity.ShowDeferredFolderIntent (this, folder);
                    StartActivity (intent); 
                }
                break;
            case Resource.Id.go_to_deadlines:
                {
                    var folder = McFolder.GetDeadlineFakeFolder ();
                    var intent = DeadlineActivity.ShowDeadlineFolderIntent (this, folder);
                    StartActivity (intent);
                }
                break;
            }
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is SwitchAccountFragment) {
                this.FragmentManager.PopBackStack ();
                return;
            }
            if (f is ChooseProviderFragment) {
                this.FragmentManager.PopBackStack ();
                return;
            }
            if (f is CredentialsFragment) {
                ((CredentialsFragment)f).OnBackPressed ();
                this.FragmentManager.PopBackStack ();
                return;
            }
            if (f is GoogleSignInFragment) {
                this.FragmentManager.PopBackStack ();
                return;
            }
            if (f is WaitingFragment) {
                this.FragmentManager.PopBackStack ();
                return;
            }
            if (MoreFragment.moreTabActivities.Contains (this.GetType ())) {
                base.OnBackPressed ();
            }
        }

    }
}

