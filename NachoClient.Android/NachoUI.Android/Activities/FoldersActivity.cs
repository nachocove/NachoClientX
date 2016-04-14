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

            folderListFragment = FolderListFragment.newInstance (NcApplication.Instance.Account.Id);
            folderListFragment.OnFolderSelected += FolderListFragment_OnFolderSelected;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, folderListFragment).Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            // Highlight the tab bar icon of this activity
            var moreImage = FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);
        }

        void FolderListFragment_OnFolderSelected (object sender, McFolder folder)
        {
            Log.Info (Log.LOG_UI, "FoldersActivity OnFolderSelected: {0}", folder);

            Intent intent = null;

            switch (folder.Id) {
            case McFolder.INBOX_FAKE_FOLDER_ID:
                intent = InboxFolderActivity.ShowInboxFolderIntent (this, folder);
                break;
            case McFolder.HOT_FAKE_FOLDER_ID:
                intent = HotFolderActivity.ShowHotFolderIntent (this, folder);
                break;
            case McFolder.LTR_FAKE_FOLDER_ID:
                intent = LtrFolderActivity.ShowLtrFolderIntent (this, folder);
                break;
            default:
                intent = MessageFolderActivity.ShowFolderIntent (this, folder);
                folder.UpdateSet_LastAccessed (DateTime.UtcNow);
                break;
            }
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

        public override void MaybeSwitchAccount ()
        {
            base.MaybeSwitchAccount ();

            if (null != folderListFragment) {
                folderListFragment.SwitchAccount (NcApplication.Instance.Account);
            }
        }
    }
}
