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
using Android.Views;

namespace NachoClient.AndroidClient
{
    public class NcTabBarActivity : NcActivity, AccountListDelegate
    {
        SwitchAccountFragment switchAccountFragment = new SwitchAccountFragment ();

        static bool tabBarCreated = false;

        public static bool TabBarWasCreated {
            get {
                return tabBarCreated;
            }
        }

        protected void OnCreate (Bundle bundle, int layoutId)
        {
            base.OnCreate (bundle);
            SetContentView (layoutId);
            tabBarCreated = true;
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            this.MaybeSwitchAccount ();
            this.SetSwitchAccountButtonImage (Window.FindViewById (Resource.Id.content));

            var moreImage = Window.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (null != moreImage) {
                if (LoginHelpers.ShouldAlertUser ()) {
                    moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
                } else {
                    moreImage.SetImageResource (Resource.Drawable.nav_more);
                }
            }
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
            return new Intent (context, typeof(NowListActivity));
        }

        void HotButton_Click (object sender, EventArgs e)
        {
            var intent = HotListIntent (this);
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            StartActivity (intent);
        }

        public static Intent InboxIntent (Context context)
        {
            var intent = new Intent ();
            intent.SetClass (context, typeof(InboxActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            return intent;
        }

        void InboxButton_Click (object sender, EventArgs e)
        {
            if (this is InboxActivity) {
                return;
            } 
            StartActivity (InboxIntent (this));
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

        public static Intent CalendarIntent (Context context)
        {
            var intent = new Intent ();
            intent.SetClass (context, typeof(CalendarActivity));
            intent.SetFlags (ActivityFlags.ClearTop | ActivityFlags.SingleTop | ActivityFlags.NoAnimation);
            return intent;
        }

        void CalendarButton_Click (object sender, EventArgs e)
        {
            if (this is CalendarActivity) {
                return;
            } 
            StartActivity (CalendarIntent (this));
        }

        public void AddAccount ()
        {
            StartActivity (new Intent (this, typeof(AddAccountActivity)));
        }

        public virtual void MaybeSwitchAccount ()
        {
        }

        // Callback from account switcher
        public void AccountSelected (McAccount account)
        {
            Log.Info (Log.LOG_UI, "NcActivity account selected {0}", account.DisplayName);
            MaybeSwitchAccount ();
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
            if (MoreFragment.moreTabActivities.Contains (this.GetType ())) {
                base.OnBackPressed ();
            }
        }

    }
}

