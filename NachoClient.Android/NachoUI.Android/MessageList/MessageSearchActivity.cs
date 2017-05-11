using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    /*
    [Activity ()]
    class MessageSearchActivity : NcActivity
    {

        #region Intents

        public static Intent BuildIntent (Context context)
        {
            var intent = new Intent (context, typeof (MessageSearchActivity));
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
        }

        void ClearSubviews ()
        {
            Toolbar = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle savedInstanceState)
        {
            PopulateFromIntent ();
            base.OnCreate (savedInstanceState);
            SetContentView (Resource.Layout.MessageListActivity);
            FindSubviews ();
            if (ThreadId == null) {
                Toolbar.Title = Folder.DisplayName;
            } else {
                Toolbar.Title = "";
            }
            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);
        }

        public override void OnAttachFragment (Android.Support.V4.App.Fragment fragment)
        {
            base.OnAttachFragment (fragment);
            if (fragment is MessageListFragment) {
                NachoEmailMessages messages;
                if (IsDrafts) {
                    messages = new NachoDraftMessages (Folder);
                } else if (ThreadId != null) {
                    messages = new NachoThreadedEmailMessages (Folder, ThreadId);
                } else {
                    messages = new NachoFolderMessages (Folder);
                }
                (fragment as MessageListFragment).SetEmailMessages (messages);
            }
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        void PopulateFromIntent ()
        {
            var bundle = Intent.Extras;
            Folder = null;
            IsDrafts = false;
            ThreadId = null;
            if (bundle.ContainsKey (EXTRA_FOLDER_ID)) {
                var folderId = bundle.GetInt (EXTRA_FOLDER_ID);
                Folder = McFolder.QueryById<McFolder> (folderId);
                if (bundle.ContainsKey (EXTRA_IS_DRAFTS) && bundle.GetBoolean (EXTRA_IS_DRAFTS)) {
                    IsDrafts = true;
                } else if (bundle.ContainsKey (EXTRA_THREAD_ID)) {
                    ThreadId = bundle.GetString (EXTRA_THREAD_ID);
                }
            }
        }

        #endregion

        #region Menu

        public override bool OnOptionsItemSelected (IMenuItem item)
        {
            switch (item.ItemId) {
            case Android.Resource.Id.Home:
                Finish ();
                return true;
            }
            return base.OnOptionsItemSelected (item);
        }

        #endregion

        #region User Actions

        void ActionButtonClicked (object sender, EventArgs e)
        {
            ComposeMessage ();
        }

        #endregion

        #region Private Helpers

        void ComposeMessage ()
        {
            var intent = MessageComposeActivity.NewMessageIntent (this, Folder.AccountId);
            StartActivity (intent);
        }

        #endregion

    }
    */
}
