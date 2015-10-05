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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "NcActivity")]            
    public class NcActivity : AppCompatActivity
    {
        MoreFragment moreFragment = new MoreFragment ();
        SwitchAccountFragment switchAccountFragment = new SwitchAccountFragment ();

        protected void OnCreate (Bundle bundle, int layoutId)
        {
            base.OnCreate (bundle);

            SetContentView (layoutId);
        }

        public void HookNavigationToolbar(Android.Views.View view)
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

        public void HookSwitchAccountView(Android.Views.View view)
        {
            var switchAccountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            switchAccountButton.Click += SwitchAccountButton_Click;
            switchAccountButton.SetImageResource (Util.GetAccountServiceImageId(NcApplication.Instance.Account.AccountService));
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

            if (this is NowActivity) {
                return;
            } 
            var intent = new Intent ();
            intent.SetClass (this, typeof(NowActivity));
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

    }
}

