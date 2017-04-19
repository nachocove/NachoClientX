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
using NachoCore.Model;

namespace NachoClient.AndroidClient
{

    [Android.App.Activity (MainLauncher = true, Label = "@string/app_name", Icon = "@drawable/icon", LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
    public class MainTabsActivity : NcActivity
    {
        private const string ACTION_SHOW_SETUP = "NachocClient.AndroidClient.MainTabsActivity.ACTION_SHOW_SETUP";
        private const int REQUEST_ADD_ACCOUNT = 1;

        private MainTabsPagerAdapter TabsAdapter;
        private EventHandler ActionButtonClickHandler;

        #region Navigation

        public static void Show (Context context)
        {
            var intent = new Intent (context, typeof (MainTabsActivity));
            intent.SetFlags (ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            context.StartActivity (intent);
        }

        public static void ShowSetup (Context context)
        {
            var intent = new Intent (context, typeof (MainTabsActivity));
            intent.SetAction (ACTION_SHOW_SETUP);
            intent.SetFlags (ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            context.StartActivity (intent);
        }

        #endregion

        #region Subviews

        private FloatingActionButton ActionButton;
        private Toolbar Toolbar;
        private ViewPager ViewPager;
        private TabLayout TabLayout;
        private Android.Support.V4.Widget.DrawerLayout DrawerLayout;
        SwitchAccountFragment SwitchAccountFragment;

        private void FindSubviews ()
        {
            ActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            ViewPager = FindViewById (Resource.Id.container) as ViewPager;
            TabLayout = FindViewById (Resource.Id.tabs) as TabLayout;
            DrawerLayout = FindViewById (Resource.Id.main_drawer_layout) as Android.Support.V4.Widget.DrawerLayout;
        }

        private void ClearSubviews ()
        {
            ActionButton = null;
            Toolbar = null;
            ViewPager = null;
            TabLayout = null;
            DrawerLayout = null;
            SwitchAccountFragment = null;
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
            SupportActionBar.SetHomeAsUpIndicator (Resource.Drawable.action_switch_account);

            TabsAdapter = new MainTabsPagerAdapter (this, SupportFragmentManager);
            ViewPager.Adapter = TabsAdapter;
            TabLayout.SetupWithViewPager (ViewPager);

            NcAccountMonitor.Instance.AccountSwitched += AccountSwitched;

            UpdateToolbarAccountInfo ();
        }

        protected override void OnPostCreate (Bundle savedInstanceState)
        {
            base.OnPostCreate (savedInstanceState);
        }

        protected override void OnNewIntent (Intent intent)
        {
            base.OnNewIntent (intent);
            if (intent.Action == ACTION_SHOW_SETUP) {
                ShowSetup ();
            }
        }

        public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
        {
	        base.OnConfigurationChanged (newConfig);
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

        protected override void OnResume ()
        {
            base.OnResume ();
            UpdateToolbarAccountInfo ();
        }

        public override void OnAttachFragment (Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is SwitchAccountFragment) {
                SwitchAccountFragment = fragment as SwitchAccountFragment;
            }
        }

        public override void OnBackPressed ()
        {
            if (DrawerLayout.IsDrawerOpen (GravityCompat.Start)) {
                DrawerLayout.CloseDrawers ();
            }else{
                base.OnBackPressed ();
            }
        }

        protected override void OnActivityResult (int requestCode, Android.App.Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_ADD_ACCOUNT:
                HandleAddAccountResult (resultCode);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
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
            //if (DrawerToggle.OnOptionsItemSelected (item)) {
            //    return true;
            //}
            if (item.ItemId == Resource.Id.action_settings) {
                ShowSettings ();
                return true;
            } else if (item.ItemId == Android.Resource.Id.Home) {
                ShowAccountSwitcher ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region Account Switching

        public void SwitchToAccount (McAccount account)
        {
            if (account.Id != NcApplication.Instance.Account.Id) {
                LoginHelpers.SetSwitchAwayTime (NcApplication.Instance.Account.Id);
                LoginHelpers.SetMostRecentAccount (account.Id);
                NcAccountMonitor.Instance.ChangeAccount (account);
            }
            DrawerLayout.CloseDrawers ();
        }

        public void AddAccount ()
        {
            var intent = AddAccountActivity.BuildIntent (this);
            DrawerLayout.CloseDrawers ();
            StartActivityForResult (intent, REQUEST_ADD_ACCOUNT);
        }

        void HandleAddAccountResult (Android.App.Result result)
        {
            if (result == Android.App.Result.Ok) {
                UpdateToolbarAccountInfo ();
            }
        }

        void AccountSwitched (object sender, EventArgs e)
        {
            UpdateToolbarAccountInfo ();
            var tab = TabsAdapter.SelectedTab;
            if (tab != null) {
                tab.OnAccountSwitched (this);
            }
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
            void OnAccountSwitched (MainTabsActivity tabActivity);

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
            SwitchAccountFragment.Refresh ();
            DrawerLayout.OpenDrawer (GravityCompat.Start, true);
        }

        #endregion

        void UpdateToolbarAccountInfo ()
        {
            var account = NcApplication.Instance.Account;
            if (account == null) {
                return;
            }
            if (String.IsNullOrEmpty (account.DisplayName)) {
                Toolbar.Title = account.EmailAddr;
            } else {
                Toolbar.Title = account.DisplayName;
            }
            int size = (int)Math.Round(40.0 * Resources.DisplayMetrics.Density);
            var image = Util.GetSizedAndRoundedAccountImage (this, account, size);
            SupportActionBar.SetHomeAsUpIndicator (image);
        }

    }
}
