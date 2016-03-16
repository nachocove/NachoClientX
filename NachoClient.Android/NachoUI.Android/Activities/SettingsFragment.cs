
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class SettingsFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        AccountAdapter accountAdapter;

        public static SettingsFragment newInstance ()
        {
            var fragment = new SettingsFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SettingsFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.SettingsListview, false, true);
            accountAdapter.AddAccount += AccountAdapter_AddAccount;
            accountAdapter.ConnectToSalesforce += AccountAdapter_ConnectToSalesforce;
            accountAdapter.AccountSelected += AccountAdapter_AccountSelected;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            var hotSwitch = view.FindViewById<Switch> (Resource.Id.show_hot_cards);
            hotSwitch.Checked = LoginHelpers.ShowHotCards ();
            hotSwitch.CheckedChange += HotSwitch_CheckedChange;

            var unreadSwitch = view.FindViewById<Switch> (Resource.Id.show_new_unread);
            unreadSwitch.Checked = EmailHelper.ShouldDisplayAllUnreadCount ();
            unreadSwitch.CheckedChange += UnreadSwitch_CheckedChange;

//            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
//                var crashButton = view.FindViewById<Button> (Resource.Id.crash_button);
//                crashButton.Visibility = ViewStates.Visible;
//                crashButton.Click += CrashButton_Click;
//                var tutorialButton = view.FindViewById<Button> (Resource.Id.tutorial_button);
//                tutorialButton.Visibility = ViewStates.Visible;
//                tutorialButton.Click += TutorialButton_Click;
//            }

            return view;
        }

        void TutorialButton_Click (object sender, EventArgs e)
        {
            StartActivity (new Intent (this.Activity, typeof(TutorialActivity)));
        }

        void HotSwitch_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            LoginHelpers.SetShowHotCards (e.IsChecked); 
        }

        void UnreadSwitch_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            EmailHelper.SetShouldDisplayAllUnreadCount (e.IsChecked);
        }

        void CrashButton_Click (object sender, EventArgs e)
        {
            throw new Exception ("CRASH SIMULATION");  
        }

        public override void OnResume ()
        {
            base.OnResume ();

            accountAdapter.Refresh ();

            // Highlight the tab bar icon of this activity
            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);

            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more_active);
            }

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void AccountAdapter_AccountSelected (object sender, McAccount account)
        {
            var parent = (SettingsActivity)Activity;
            parent.AccountSettingsSelected (account);
        }

        void AccountAdapter_AddAccount (object sender, EventArgs e)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AddAccount ();
        }

        void AccountAdapter_ConnectToSalesforce (object sender, EventArgs e)
        {
            var parent = (SettingsActivity)Activity;
            parent.ConnectToSalesforce ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_AccountSetChanged:
                accountAdapter.Refresh ();
                break;
            case NcResult.SubKindEnum.Info_AccountChanged:
                var activity = (NcTabBarActivity)this.Activity;
                activity.SetSwitchAccountButtonImage (View);
                break;
            }
        }

    }
}

