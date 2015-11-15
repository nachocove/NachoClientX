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
    [Activity (Label = "NcMessageListActivity")]
    public class NcMessageListActivity : NcTabBarActivity, MessageListDelegate
    {
        protected McAccount account;
        MessageListFragment messageListFragment;

        private const string EXTRA_THREAD = "com.nachocove.nachomail.EXTRA_THREAD";
        private const string EXTRA_MESSAGE = "com.nachocove.nachomail.EXTRA_MESSAGE";

        private const string MESSAGE_LIST_FRAGMENT_TAG = "MessageList";

        protected virtual INachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
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

        public virtual void SetActiveImage (View view)
        {
            // Highlight the tab bar icon of this activity
            // See inbox & nacho now activities
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.NcMessageListActivity);

            account = NcApplication.Instance.Account;

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

        protected override void OnResume ()
        {
            base.OnResume ();
        }

        void MessageListFragment_onEventClick (object sender, McEvent ev)
        {
            StartActivity (EventViewActivity.ShowEventIntent (this, ev));
        }

        void MessageListFragment_onThreadClick (object sender, INachoEmailMessages threadMessages)
        {
            var intent = MessageThreadActivity.ShowThreadIntent (this, threadMessages);
            StartActivity (intent);
        }

        void MessageListFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            var message = thread.FirstMessageSpecialCase ();
            var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
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
            MaybeSwitchAccount ();
        }

        void MaybeSwitchAccount ()
        {
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
}
