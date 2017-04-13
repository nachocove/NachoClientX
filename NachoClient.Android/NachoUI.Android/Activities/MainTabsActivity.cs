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

using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{

    [Android.App.Activity (MainLauncher = true, Label = "@string/app_name", Icon = "@drawable/icon")]
    public class MainTabsActivity : Android.Support.V7.App.AppCompatActivity
    {

        #region Navigation

        public static void Show (Context context)
        {
            var intent = new Intent (context, typeof(MainTabsActivity));
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
                GoToSetup ();
                return;
            }

            SetContentView (Resource.Layout.MainTabsActivity);
            FindSubviews ();

            SetSupportActionBar (Toolbar);

            ViewPager.Adapter = new MainTabsPagerAdapter (SupportFragmentManager);
            TabLayout.SetupWithViewPager (ViewPager);
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

        #region Tabs

        class MainTabsPagerAdapter : FragmentPagerAdapter
        {

            TabFragment[] Tabs = { null };

            public MainTabsPagerAdapter (FragmentManager fragmentManager) : base (fragmentManager)
            {
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

            public void OnSelected ()
            {
            }

            public void OnUnselected ()
            {
            }

        }

        #endregion

        #region Private Helpers

        void GoToSetup ()
        {
        	var intent = new Intent (this, typeof (SetupActivity));
        	StartActivity (intent);
        	Finish ();
        }

        #endregion

    }
}
