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


    /*
    [Activity (Label = "NcMessageListActivity")]
    public class NcMessageListActivity : NcTabBarActivity, MessageListDelegate
    {
        protected McAccount account;
        MessageListFragment messageListFragment;

        private const string EXTRA_THREAD = "com.nachocove.nachomail.EXTRA_THREAD";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        private const string MESSAGE_LIST_FRAGMENT_TAG = "MessageList";

        protected virtual NachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            throw new NotImplementedException ();
        }

        public virtual void ListIsEmpty ()
        {
        }

        public virtual bool ShowHotEvent ()
        {
            return false;
        }

        public virtual int ShowListStyle()
        {
            return MessageListAdapter.LISTVIEW_STYLE;
        }

        public virtual void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            // See inbox & nacho now activities
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.NcMessageListActivity);

            account = NcApplication.Instance.Account;

            // Maybe during short-circuit start up
            if (null == account) {
                return;
            }

            List<int> adds;
            List<int> deletes;
            var messages = GetMessages (out adds, out deletes);

            messageListFragment = null;
            if (null != bundle) {
                messageListFragment = FragmentManager.FindFragmentByTag<MessageListFragment> (MESSAGE_LIST_FRAGMENT_TAG);
            }
            if (null == messageListFragment) {
                messageListFragment = new MessageListFragment ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, messageListFragment, MESSAGE_LIST_FRAGMENT_TAG).Commit ();
            }
            messageListFragment.Initialize (messages, MessageListFragment_onEventClick, MessageListFragment_onThreadClick, MessageListFragment_onMessageClick);
        }

        void MessageListFragment_onEventClick (object sender, McEvent ev)
        {
            StartActivity (EventViewActivity.ShowEventIntent (this, ev));
        }

        void MessageListFragment_onThreadClick (object sender, NachoEmailMessages threadMessages)
        {
            var intent = MessageThreadActivity.ShowThreadIntent (this, threadMessages);
            StartActivity (intent);
        }

        void MessageListFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            if ((null != thread.Source) && thread.Source.HasDraftsSemantics ()) {
                var message = thread.SingleMessageSpecialCase ();
                ComposeDraft (message);
            } else if ((null != thread.Source) && thread.Source.HasOutboxSemantics ()) {
                DealWithThreadInOutbox (thread);
            } else {
                var message = thread.FirstMessageSpecialCase ();
                if (null != message) {
                    var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
                    StartActivity (intent);
                }
            }
        }

        public void DealWithThreadInOutbox (McEmailMessageThread messageThread)
        {
            var message = messageThread.SingleMessageSpecialCase ();
            if (null == message) {
                return;
            }

            var pending = McPending.QueryByEmailMessageId (message.AccountId, message.Id);
            if ((null == pending) || (NcResult.KindEnum.Error != pending.ResultKind)) {
                var copy = EmailHelper.MoveFromOutboxToDrafts (message);
                ComposeDraft (copy);
                return;
            }

            string errorString;
            if (!ErrorHelper.ErrorStringForSubkind (pending.ResultSubKind, out errorString)) {
                errorString = String.Format ("(ErrorCode={0}", pending.ResultSubKind);
            }
            var messageString = "There was a problem sending this message.  You can resend this message or open it in the drafts folder.";
            var alertString = String.Format ("{0}\n{1}", messageString, errorString);
            NcAlertView.Show (this, "Edit Message", alertString, () => {
                var copy = EmailHelper.MoveFromOutboxToDrafts (message);
                ComposeDraft (copy);
            });
        }

        void ComposeDraft (McEmailMessage message)
        {
            StartActivity (MessageComposeActivity.DraftIntent (this, message));
        }

        public override void OnBackPressed ()
        {
            if (null == messageListFragment || !messageListFragment.HandleBackButton ()) {
                base.OnBackPressed ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        public override void MaybeSwitchAccount ()
        {
            base.MaybeSwitchAccount ();

            if (null != messageListFragment) {
                messageListFragment.MaybeSwitchStyle (ShowListStyle ());
            }

            if ((null != account) && (NcApplication.Instance.Account.Id == account.Id)) {
                return;
            }
            account = NcApplication.Instance.Account;
            List<int> adds;
            List<int> deletes;
            var messages = GetMessages (out adds, out deletes);
            messageListFragment.SwitchAccount (messages);
        }
    }
    */
}
