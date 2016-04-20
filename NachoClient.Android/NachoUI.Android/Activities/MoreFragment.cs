
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
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class MoreFragment : Fragment
    {

        public static MoreFragment newInstance ()
        {
            var fragment = new MoreFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.MoreFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var folderView = view.FindViewById<View> (Resource.Id.mail);
            folderView.Click += FolderView_Click;

            var deferredView = view.FindViewById<View> (Resource.Id.deferred);
            deferredView.Click += DeferredView_Click;

            var deadlineView = view.FindViewById<View> (Resource.Id.deadline);
            deadlineView.Click += DeadlineView_Click;

            var contactsView = view.FindViewById<View> (Resource.Id.contacts);
            contactsView.Click += ContactsView_Click;

            var filesView = view.FindViewById<View> (Resource.Id.files);
            filesView.Click += FilesView_Click;

            var settingsView = view.FindViewById<View> (Resource.Id.settings);
            settingsView.Click += SettingsView_Click;

            var supportView = view.FindViewById<View> (Resource.Id.support);
            supportView.Click += SupportView_Click;

            var aboutView = view.FindViewById<View> (Resource.Id.about);
            aboutView.Click += AboutView_Click;

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();

            // Highlight the tab bar icon of this activity
            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            var settingsImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.account_alert);

            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
                settingsImage.Visibility = ViewStates.Visible;
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more_active);
                settingsImage.Visibility = ViewStates.Invisible;
            }
        }

        void ContactsView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(ContactsActivity));
            StartActivity (intent); 
        }

        void AboutView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(AboutActivity));
            StartActivity (intent);    
        }

        void FilesView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(FileListActivity));
            StartActivity (intent);
        }

        void DeadlineView_Click (object sender, EventArgs e)
        { 
        }

        void DeferredView_Click (object sender, EventArgs e)
        {
        }

        void FolderView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(FoldersActivity));
            StartActivity (intent);
        }

        void SettingsView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(SettingsActivity));
            StartActivity (intent);
        }

        void SupportView_Click (object sender, EventArgs e)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(SupportActivity));
            StartActivity (intent);
        }

        static public List<Type> moreTabActivities = new List<Type> () {
            typeof(AboutActivity),
            typeof(FoldersActivity),
            typeof(FileListActivity),
            typeof(SettingsActivity),
            typeof(SupportActivity),
        };
    }
}

