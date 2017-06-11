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
    [Activity ()]
    class MessageListActivity : NcActivity
    {

        public const string EXTRA_THREAD_ID = "NachoClient.AndroidClient.MessageListActivity.EXTRA_THREAD_ID";
        public const string EXTRA_FOLDER_ID = "NachoClient.AndroidClient.MessageListActivity.EXTRA_FOLDER_ID";
        public const string EXTRA_IS_DRAFTS = "NachoClient.AndroidClient.MessageListActivity.EXTRA_IS_DRAFTS";
        public const string EXTRA_CONTACT_ID = "NachoClient.AndroidClient.MessageListActivity.EXTRA_CONTACT_ID";

        McContact Contact;
        McFolder Folder;
        string ThreadId;
        bool IsDrafts;


        #region Intents

        public static Intent BuildThreadIntent (Context context, McFolder folder, McEmailMessageThread thread)
        {
            var intent = BuildFolderIntent (context, folder);
            intent.PutExtra (EXTRA_THREAD_ID, thread.GetThreadId ());
            return intent;
        }

        public static Intent BuildFolderIntent (Context context, McFolder folder)
        {
            var intent = new Intent (context, typeof (MessageListActivity));
            intent.PutExtra (EXTRA_FOLDER_ID, folder.Id);
            return intent;
        }

        public static Intent BuildDraftsIntent (Context context, McFolder folder)
        {
            var intent = BuildFolderIntent (context, folder);
            intent.PutExtra (EXTRA_IS_DRAFTS, true);
            return intent;
        }

        public static Intent BuildContactIntent (Context context, McContact contact)
        {
            var intent = new Intent (context, typeof (MessageListActivity));
            intent.PutExtra (EXTRA_CONTACT_ID, contact.Id);
            return intent;
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        FloatingActionButton FloatingActionButton;

        void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            FloatingActionButton = FindViewById (Resource.Id.fab) as FloatingActionButton;
            FloatingActionButton.Click += ActionButtonClicked;
        }

        void ClearSubviews ()
        {
            FloatingActionButton.Click -= ActionButtonClicked;
            Toolbar = null;
            FloatingActionButton = null;
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
                if (Contact != null) {
                    Toolbar.Title = Contact.GetDisplayName ();
                } else if (Folder != null) {
                    Toolbar.Title = Folder.DisplayName;
                } else {
                    Toolbar.Title = "";
                }
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
                } else if (Contact != null) {
                    messages = new UserInteractionEmailMessages (Contact);
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
            Contact = null;
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
            } else if (bundle.ContainsKey (EXTRA_CONTACT_ID)) {
                var contactId = bundle.GetInt (EXTRA_CONTACT_ID);
                Contact = McContact.QueryById<McContact> (contactId);
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
            int accountId;
            string email = null;
            if (Contact != null) {
                accountId = Contact.AccountId;
                email = Contact.GetPrimaryCanonicalEmailAddress ();
            } else {
                accountId = Folder.AccountId;
            }
            var intent = MessageComposeActivity.NewMessageIntent (this, accountId, email);
            StartActivity (intent);
        }

        #endregion

    }

}
