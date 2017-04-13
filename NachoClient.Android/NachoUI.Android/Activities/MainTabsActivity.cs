//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.Design.Widget;
using Android.Support.V4.View;
using Android.Support.V4.App;
using Android.Support.V7.Widget;
using Android.Graphics.Drawables;

using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    [Android.App.Activity (MainLauncher = true, Label = "@string/app_name", Icon = "@drawable/icon")]
    public class MainTabsActivity : NcActivity
    {

        private MainTabsPagerAdapter TabsAdapter;

        #region Navigation

        public static void Show (Context context)
        {
            var intent = new Intent (context, typeof (MainTabsActivity));
            intent.SetFlags (ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            context.StartActivity (intent);
        }

        #endregion

        #region Subviews

        private FloatingActionButton Fab;
        private Toolbar Toolbar;
        private ViewPager ViewPager;
        private TabLayout TabLayout;

        private void FindSubviews ()
        {
            Fab = FindViewById (Resource.Id.fab) as FloatingActionButton;
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            ViewPager = FindViewById (Resource.Id.container) as ViewPager;
            TabLayout = FindViewById (Resource.Id.tabs) as TabLayout;
        }

        private void ClearSubviews ()
        {
            Fab = null;
            Toolbar = null;
            ViewPager = null;
            TabLayout = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            MainApplication.RegisterHockeyAppUpdateManager (this);

            // Kind of a kludge, but bail out and show the setup activity if we aren't ready to start yet
            if (!NcApplication.ReadyToStartUI ()) {
                ShowSetup ();
                return;
            }

            SetContentView (Resource.Layout.MainTabsActivity);
            FindSubviews ();

            SetSupportActionBar (Toolbar);

            ViewPager.Adapter = TabsAdapter = new MainTabsPagerAdapter (this, SupportFragmentManager);
            TabLayout.SetupWithViewPager (ViewPager);

            UpdateToolbarAccountInfo ();
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        protected override void OnPause ()
        {
            base.OnPause ();
            MainApplication.UnregisterHockeyAppUpdateManager ();
            MainApplication.SetupHockeyAppCrashManager (this);
        }

        #endregion

        #region Options Menu

        public override bool OnCreateOptionsMenu (IMenu menu)
        {
            var tab = TabsAdapter.SelectedTab;
            if (tab == null) {
                return false;
            }
            MenuInflater.Inflate (tab.MenuResource, menu);
            return true;
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Resource.Id.action_settings) {
                ShowSettings ();
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region Tabs

        class MainTabsPagerAdapter : FragmentPagerAdapter
        {

            TabFragment [] Tabs = { null };
            int SelectedPosition = 0;
            MainTabsActivity MainTabsActivity;

            public TabFragment SelectedTab {
                get {
                    if (SelectedPosition >= 0) {
                        return Tabs [SelectedPosition];
                    }
                    return null;
                }
            }

            public MainTabsPagerAdapter (MainTabsActivity activity, FragmentManager fragmentManager) : base (fragmentManager)
            {
                MainTabsActivity = activity;
            }

            public override int Count {
                get {
                    return 1;
                }
            }

            public override Fragment GetItem (int position)
            {
                TabFragment fragment = null;
                switch (position) {
                case 0:
                    fragment = new HomeFragment ();
                    break;
                default:
                    NcAssert.CaseError (String.Format ("Unexpected MainTabsActivity tab position: {0}", position));
                    break;
                }
                Tabs [position] = fragment;
                return fragment;
            }

            public override Java.Lang.ICharSequence GetPageTitleFormatted (int position)
            {
                switch (position) {
                case 0:
                    return new Java.Lang.String ("Home");
                default:
                    NcAssert.CaseError (String.Format ("Unexpected MainTabsActivity tab position: {0}", position));
                    break;
                }
                return null;
            }

            public override void FinishUpdate (View container)
            {
                base.FinishUpdate (container);
                var position = MainTabsActivity.ViewPager.CurrentItem;
                if (position != SelectedPosition) {
                    if (SelectedTab != null) {
                        SelectedTab.OnUnselected ();
                    }
                    SelectedPosition = position;
                    if (SelectedTab != null) {
                        SelectedTab.OnSelected ();
                    }
                    MainTabsActivity.InvalidateOptionsMenu ();
                }
            }

        }

        public class TabFragment : Fragment
        {

            public MainTabsActivity TabsActivity {
                get {
                    return Activity as MainTabsActivity;
                }
            }

            public FloatingActionButton Fab {
                get {
                    return TabsActivity.Fab;
                }
            }

            public virtual void OnSelected ()
            {
            }

            public virtual void OnUnselected ()
            {
            }

            public virtual int MenuResource {
                get {
                    return -1;
                }
            }

        }

        #endregion

        #region Private Helpers

        void ShowSetup ()
        {
            var intent = SetupActivity.BuildIntent (this);
            StartActivity (intent);
            // If we're showing setup, it's because there aren't any accounts setup and we should
            // finish immediately before trying to do anything else ourself
            Finish ();
        }

        void ShowSettings ()
        {
            var intent = SettingsActivity.BuildIntent (this);
            StartActivity (intent);
        }

        #endregion

        void UpdateToolbarAccountInfo ()
        {
            var account = NcApplication.Instance.Account;
            if (String.IsNullOrEmpty (account.DisplayName)) {
                Toolbar.Title = account.EmailAddr;
            } else {
                Toolbar.Title = account.DisplayName;
            }
            // FIXME: figure out how to size properly
            // var image = Util.GetAccountImage (this, account);
            // Also, the white background on our account images looks dumb
            // Toolbar.Logo = ScaledToolbarIcon (image);
        }

    }
}
