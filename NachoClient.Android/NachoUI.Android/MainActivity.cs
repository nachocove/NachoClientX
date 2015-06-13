using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Content.Res;
using Android.OS;
using Android.Runtime;
using Android.Support.V4.App;
using Android.Support.V4.Content;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Views;
using Android.Widget;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoClient.Build;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "NachoClient.AndroidClient",
        Theme = "@style/Theme.AppCompat.Light",
        MainLauncher = true,
        UiOptions = Android.Content.PM.UiOptions.SplitActionBarWhenNarrow
    )]
    [MetaData ("android.support.UI_OPTIONS", Value = "splitActionBarWhenNarrow")]
    public class MainActivity : ActionBarActivity
    {
        private DrawerLayout drawer;
        private MyActionBarDrawerToggle drawerToggle;
        private ListView drawerList;
        private string drawerTitle;
        private string title;

        public class SidebarMenu
        {
            public int Indent;
            public string SegueName;
            public string DisplayName;
            public McFolder Folder;
            public bool isDeviceContactsKludge;
            public bool isDeviceCalendarKludge;

            public SidebarMenu (McFolder folder, string displayName, string segueName)
            {
                Indent = 0;
                SegueName = segueName;
                DisplayName = displayName;
                Folder = folder;
                isDeviceContactsKludge = false;
                isDeviceCalendarKludge = false;
            }
        };

        public List<SidebarMenu> menu;
        NachoFolders email;
        NachoFolders contacts;
        NachoFolders calendars;
        const string SidebarToFoldersSegueId = "SidebarToFolders";
        const string SidebarToContactsSegueId = "SidebarToContacts";
        const string SidebarToCalendarSegueId = "SidebarToCalendar";
        const string SidebarToMessagesSegueId = "SidebarToMessages";
        const string SidebarToDeferredMessagesSegueId = "SidebarToDeferredMessages";
        const string SidebarToNachoNowSegueId = "SidebarToNachoNow";
        const string SidebarToHomeSegueId = "SidebarToHome";

        protected override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);

            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartClass1Services complete");

            NcApplication.Instance.AppStartupTasks ();

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            Log.Info (Log.LOG_UI, "{0} (build {1}) built at {2} by {3}",
                BuildInfo.Version, BuildInfo.BuildNumber, BuildInfo.Time, BuildInfo.User);
            if (0 < BuildInfo.Source.Length) {
                Log.Info (Log.LOG_INIT, "Source Info: {0}", BuildInfo.Source);
            }

            PopulateSidebarMenu ();

            title = drawerTitle = Title;
            drawer = FindViewById<Android.Support.V4.Widget.DrawerLayout> (Resource.Id.drawer_layout);
            drawerList = FindViewById<ListView> (Resource.Id.left_drawer);

            drawer.SetDrawerShadow (Resource.Drawable.drawer_shadow_dark, (int)GravityCompat.Start);

            drawerList.Adapter = new SidebarMenuAdapter (this);
            drawerList.ItemClick += (sender, args) => ItemSelected (args.Position);

            //DrawerToggle is the animation that happens with the indicator next to the
            //ActionBar icon. You can choose not to use this.
            drawerToggle = new MyActionBarDrawerToggle (this, drawer,
                Resource.Drawable.ic_drawer_light,
                Resource.String.DrawerOpen,
                Resource.String.DrawerClose);

            //You can alternatively use _drawer.DrawerClosed here
            drawerToggle.DrawerClosed += delegate {
                SupportActionBar.Title = title;
                SupportInvalidateOptionsMenu ();
            };

            //You can alternatively use _drawer.DrawerOpened here
            drawerToggle.DrawerOpened += delegate {
                SupportActionBar.Title = drawerTitle;
                SupportInvalidateOptionsMenu ();
            };

            drawer.SetDrawerListener (drawerToggle);

            // Watch for changes from the back end
            NcApplication.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_FolderSetChanged == s.Status.SubKind) {
                    PopulateSidebarMenu ();
                }
            };

            if (1 == NcModel.Instance.Db.Table<McAccount> ().Count ()) {
                var fragment = new CredentialsFragment ();
                this.SupportFragmentManager.BeginTransaction ()
                    .Replace (Resource.Id.content_frame, fragment)
                    .Commit ();
            } else {
                if (null == savedInstanceState) {
                    ItemSelected (0);
                }
            }

            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
            SupportActionBar.SetHomeButtonEnabled (true);
        }

        public void PopulateSidebarMenu ()
        {
            menu = new List<SidebarMenu> ();

//            email = new NachoFolders (NachoFolders.FilterForEmail);
//            contacts = new NachoFolders (NachoFolders.FilterForContacts);
//            calendars = new NachoFolders (NachoFolders.FilterForCalendars);

            menu.Add (new SidebarMenu (null, "Now", SidebarToNachoNowSegueId));

            menu.Add (new SidebarMenu (null, "Folders", SidebarToFoldersSegueId));

//            for (int i = 0; i < email.Count (); i++) {
//                McFolder f = email.GetFolder (i);
//                var m = new SidebarMenu (f, f.DisplayName, SidebarToMessagesSegueId);
//                m.Indent = 1;
//                menu.Add (m);
//            }
//            menu.Add (new SidebarMenu (null, "Later", SidebarToDeferredMessagesSegueId));
//
//            menu.Add (new SidebarMenu (null, "Contacts", SidebarToContactsSegueId));
//            for (int i = 0; i < contacts.Count (); i++) {
//                McFolder f = contacts.GetFolder (i);
//                var m = new SidebarMenu (f, f.DisplayName, SidebarToContactsSegueId);
//                m.Indent = 1;
//                menu.Add (m);
//            }
//            var deviceContacts = new SidebarMenu (null, "Device Contacts", SidebarToContactsSegueId);
//            deviceContacts.isDeviceContactsKludge = true;
//            menu.Add (deviceContacts);
//
//            menu.Add (new SidebarMenu (null, "Calendars", SidebarToCalendarSegueId));
//            for (int i = 0; i < calendars.Count (); i++) {
//                McFolder f = calendars.GetFolder (i);
//                var m = new SidebarMenu (f, f.DisplayName, SidebarToCalendarSegueId);
//                m.Indent = 1;
//                menu.Add (m);
//            }
//            var deviceCalendar = new SidebarMenu (null, "Device Calendar", SidebarToCalendarSegueId);
//            deviceCalendar.isDeviceCalendarKludge = true;
//            menu.Add (deviceCalendar);

            menu.Add (new SidebarMenu (null, "Home", SidebarToHomeSegueId));
            menu.Add (new SidebarMenu (null, "Accounts", "SidebarToAccounts"));
            menu.Add (new SidebarMenu (null, "Settings", "SidebarToSettings"));

        }
        // Pass the event to ActionBarDrawerToggle, if it returns
        // true, then it has handled the app icon touch event
        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            if (drawerToggle.OnOptionsItemSelected (item)) {
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        private void ItemSelected (int position)
        {
            var menuItem = menu [position];
            var segueName = menuItem.SegueName;

            Android.Support.V4.App.Fragment fragment = null;

            if (segueName.Equals (SidebarToFoldersSegueId)) {
                fragment = new FolderListFragment ();
            }
            if (segueName.Equals (SidebarToContactsSegueId)) {
                fragment = new ContactListFragment ();
                var bundle = new Bundle ();
                bundle.PutString ("segue", segueName);
                fragment.Arguments = bundle;
            }
            if (segueName.Equals (SidebarToCalendarSegueId)) {
                fragment = new CalendarListFragment ();
                var bundle = new Bundle ();
                bundle.PutString ("segue", segueName);
                fragment.Arguments = bundle;
            }
            if (segueName.Equals (SidebarToMessagesSegueId)) {
                fragment = new MessageListFragment ();
                var bundle = new Bundle ();
                bundle.PutInt ("accountId", menuItem.Folder.AccountId);
                bundle.PutInt ("folderId", menuItem.Folder.Id);
                bundle.PutString ("segue", segueName);
                fragment.Arguments = bundle;
            }
            if (segueName.Equals (SidebarToDeferredMessagesSegueId)) {
                fragment = new MessageListFragment ();
                var bundle = new Bundle ();
                bundle.PutInt ("accountId", menuItem.Folder.AccountId);
                bundle.PutInt ("folderId", menuItem.Folder.Id);
                bundle.PutString ("segue", segueName);
                fragment.Arguments = bundle;
            }
            if (segueName.Equals (SidebarToNachoNowSegueId)) {
                fragment = new NachoNowFragment ();
            }
            if (segueName.Equals (SidebarToHomeSegueId)) {
                fragment = new HomeFragment ();
            }

            NcAssert.True (null != fragment);

            this.SupportFragmentManager.BeginTransaction ()
                .Replace (Resource.Id.content_frame, fragment)
                .Commit ();

            SupportActionBar.Title = title = menu [position].DisplayName;
            drawer.CloseDrawer (drawerList);
        }

        protected override void OnPostCreate (Bundle savedInstanceState)
        {
            base.OnPostCreate (savedInstanceState);
            drawerToggle.SyncState ();
        }

        public override bool OnPrepareOptionsMenu (IMenu menu)
        {
            var drawerOpen = this.drawer.IsDrawerOpen (this.drawerList);
            // When open don't show anything
            for (int i = 0; i < menu.Size (); i++) {
                menu.GetItem (i).SetVisible (!drawerOpen);
            }
            return base.OnPrepareOptionsMenu (menu);
        }

        public override void OnConfigurationChanged (Configuration newConfig)
        {
            base.OnConfigurationChanged (newConfig);
            drawerToggle.OnConfigurationChanged (newConfig);
        }

        public class SidebarMenuAdapter : BaseAdapter<SidebarMenu>
        {
            MainActivity context;

            public SidebarMenuAdapter (MainActivity context) : base ()
            {
                this.context = context;
            }

            public override long GetItemId (int position)
            {
                return position;
            }

            public override SidebarMenu this [int position] {  
                get { return context.menu [position]; }
            }

            public override int Count {
                get { return context.menu.Count; }
            }

            public override View GetView (int position, View convertView, ViewGroup parent)
            {
                View view = convertView; // re-use an existing view, if one is available
                if (view == null) {
                    // otherwise create a new one
                    view = context.LayoutInflater.Inflate (Resource.Layout.DrawerListItem, null);
                }
                view.FindViewById<TextView> (Android.Resource.Id.Text1).Text = context.menu [position].DisplayName;
                return view;
            }
        }
    }
}


