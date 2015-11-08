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
    public class FoldersActivity : NcTabBarActivity, MessageListDelegate
    {
        INachoEmailMessages messages;

        FolderListFragment folderListFragment;
        MessageListFragment messageListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.FoldersActivity);

            folderListFragment = FolderListFragment.newInstance ();
            folderListFragment.onFolderSelected += onFolderSelected;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, folderListFragment).AddToBackStack ("Folders").Commit ();
        }

        void onFolderSelected (McFolder folder)
        {
            Log.Info (Log.LOG_UI, "FoldersActivity onFolderClick: {0}", folder);

            messages = new NachoEmailMessages (folder);

            List<int> adds;
            List<int> deletes;
            messages.Refresh (out adds, out deletes);

            messageListFragment = MessageListFragment.newInstance (messages);
            messageListFragment.onMessageClick += onMessageClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Messages").Commit ();

        }

        void onMessageClick (object sender, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "FoldersActivity onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
                StartActivity (intent);
            } else {
                var threadMessages = messages.GetAdapterForThread (thread.GetThreadId ());
                messageListFragment = MessageListFragment.newInstance (threadMessages);
                messageListFragment.onMessageClick += onMessageClick;
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Thread").Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageListFragment) {
                this.FragmentManager.PopBackStack ();
            }
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

        // MessageListDelegate
        public bool ShowHotEvent()
        {
            return false;
        }

        // MessageListDelegate
        public void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            // See inbox & nacho now activities
        }
    }
}
