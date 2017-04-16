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

    [Android.App.Activity (MainLauncher = true, Label = "@string/app_name", Icon = "@drawable/icon", LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
    public class MainTabsActivity : NcActivity
    {

        private MainTabsPagerAdapter TabsAdapter;
        private EventHandler ActionButtonClickHandler;

        #region Navigation

        public static void Show (Context context)
        {
            var intent = new Intent (context, typeof (MainTabsActivity));
            intent.SetFlags (ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            context.StartActivity (intent);
        }

        #endregion

        #region Subviews

        private FloatingActionButton ActionButton;
        private Toolbar Toolbar;
        private ViewPager ViewPager;
        private TabLayout TabLayout;

        private void FindSubviews ()
        {
            ActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            ViewPager = FindViewById (Resource.Id.container) as ViewPager;
            TabLayout = FindViewById (Resource.Id.tabs) as TabLayout;
        }

        private void ClearSubviews ()
        {
            ActionButton = null;
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
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            SupportActionBar.SetHomeAsUpIndicator (Resource.Drawable.action_hamburger);

            TabsAdapter = new MainTabsPagerAdapter (this, SupportFragmentManager);
            ViewPager.Adapter = TabsAdapter;
            TabLayout.SetupWithViewPager (ViewPager);

            UpdateToolbarAccountInfo ();
        }

        protected override void OnRestoreInstanceState (Bundle savedInstanceState)
        {
            base.OnRestoreInstanceState (savedInstanceState);
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
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
            if (tab.TabMenuResource < 0) {
                return false;
            }
            MenuInflater.Inflate (tab.TabMenuResource, menu);
            return true;
        }

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (item.ItemId == Resource.Id.action_settings) {
                ShowSettings ();
            } else if (item.ItemId == Android.Resource.Id.Home) {
                ShowAccountSwitcher ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region Floating Action Button

        public void HideActionButton ()
        {
            ActionButton.Hide ();
        }

        public void ShowActionButton (int imageResource, EventHandler clickHandler, bool isEnabled = true)
        {
            ActionButton.SetImageResource (imageResource);
            ActionButton.Show ();
            if (ActionButtonClickHandler != null) {
                ActionButton.Click -= ActionButtonClickHandler;
            }
            ActionButtonClickHandler = clickHandler;
            ActionButton.Click += clickHandler;
            if (isEnabled) {
                EnableActionButton ();
            } else {
                DisableActionButton ();
            }
        }

        public void DisableActionButton ()
        {
            ActionButton.Enabled = false;
        }

        public void EnableActionButton ()
        {
            ActionButton.Enabled = true;
        }

        #endregion

        #region Tabs

        class MainTabsPagerAdapter : FragmentPagerAdapter
        {

            class TabInfo
            {
                public int NameResource;
                public Type FragmentType;
                public WeakReference<Fragment> CachedFragmentInstance = new WeakReference<Fragment>(null);

                public Tab Tab {
                    get {
                        Fragment fragment;
                        if (CachedFragmentInstance.TryGetTarget (out fragment)) {
                            return fragment as Tab;
                        }
                        return null;
                    }
                }
            }

            TabInfo [] Tabs = {
                //new TabInfo () { NameResource = Resource.String.tab_home,       FragmentType=typeof (HomeFragment) },
                new TabInfo () { NameResource = Resource.String.tab_inbox,      FragmentType=typeof (InboxFragment) },
                new TabInfo () { NameResource = Resource.String.tab_all_mail,   FragmentType=typeof (AllMailFragment) },
                new TabInfo () { NameResource = Resource.String.tab_calendar,   FragmentType=typeof (CalendarFragment) },
                new TabInfo () { NameResource = Resource.String.tab_contacts,   FragmentType=typeof (ContactsFragment) }
            };

            int SelectedPosition = -1;
            MainTabsActivity MainTabsActivity;

            public Tab SelectedTab {
                get {
                    if (SelectedPosition >= 0) {
                        return Tabs [SelectedPosition].Tab;
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
                    return Tabs.Length;
                }
            }

            public override Java.Lang.Object InstantiateItem (ViewGroup container, int position)
            {
                var obj = base.InstantiateItem (container, position);
                var fragment = obj as Fragment;
                if (fragment != null) {
                    var info = Tabs [position];
                    info.CachedFragmentInstance.SetTarget (fragment);
                }
                return obj;
            }

            public override Fragment GetItem (int position)
            {
                var info = Tabs [position];
                var fragment = System.Activator.CreateInstance (info.FragmentType) as Fragment;
                return fragment;
            }

            // The idea here was to remove our CachedFragmentInstance when it was no longer needed, and rely on
            // GetItem to re-populate the item once it's needed again.  However, while Android calls DestroyItem,
            // it doesn't ever re-call GetItem.  Maybe there's something I'm missing, but for now we'll just hold
            // on the the CachedFragmentInstance forever
            //public override void DestroyItem (ViewGroup container, int position, Java.Lang.Object @object)
            //{
            //    base.DestroyItem (container, position, @object);
            //    var info = Tabs [position];
            //    info.CachedFragmentInstance = null;
            //}

            public override Java.Lang.ICharSequence GetPageTitleFormatted (int position)
            {
                var info = Tabs [position];
                var name = MainTabsActivity.GetString (info.NameResource);
                return new Java.Lang.String (name);
            }

            public override float GetPageWidth (int position)
            {
                return 1;
            }

            public override void FinishUpdate (ViewGroup container)
            {
                base.FinishUpdate (container);
                var position = MainTabsActivity.ViewPager.CurrentItem;
                if (position != SelectedPosition) {
                    if (SelectedTab != null) {
                        SelectedTab.OnTabUnselected (MainTabsActivity);
                    }
                    SelectedPosition = position;
                    if (SelectedTab != null) {
                        SelectedTab.OnTabSelected (MainTabsActivity);
                    }
                    MainTabsActivity.InvalidateOptionsMenu ();
                }
            }

        }

        public interface Tab
        {

            void OnTabSelected (MainTabsActivity tabActivity);
            void OnTabUnselected (MainTabsActivity tabActivity);

            int TabMenuResource { get; }

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

        void ShowAccountSwitcher ()
        {
            ShowSettings ();
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
