using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "FoldersActivity")]            
    public class FoldersActivity : NcTabBarActivity
    {
        FolderListFragment folderListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.FoldersActivity);

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            folderListFragment = FolderListFragment.newInstance ();
            folderListFragment.onFolderSelected += onFolderSelected;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, folderListFragment).Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            // Highlight the tab bar icon of this activity
            var moreImage = FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);
        }

        void onFolderSelected (McFolder folder)
        {
            Log.Info (Log.LOG_UI, "FoldersActivity onFolderClick: {0}", folder);

            var intent = MessageFolderActivity.ShowFolderIntent (this, folder);
            StartActivity (intent);
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public override void SwitchAccount (McAccount account)
        {
            base.SwitchAccount (account);

            FragmentManager.PopBackStackImmediate ("Folders", PopBackStackFlags.None);

            if (null != folderListFragment) {
                folderListFragment.SwitchAccount ();
            }
        }
    }
}
