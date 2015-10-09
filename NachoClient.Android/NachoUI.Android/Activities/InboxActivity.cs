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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "InboxActivity")]            
    public class InboxActivity : NcActivity
    {
        McAccount account;
        MessageViewFragment messageViewFragment;
        MessageListFragment messageListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.InboxActivity);

            account = NcApplication.Instance.Account;
            var messages = NcEmailManager.Inbox (NcApplication.Instance.Account.Id);

            List<int> adds;
            List<int> deletes;
            messages.Refresh (out adds, out deletes);

            messageListFragment = MessageListFragment.newInstance (messages);
            messageListFragment.onMessageClick += onMessageClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Inbox").Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            MaybeSwitchAccount ();
        }

        void onMessageClick (object sender, McEmailMessageThread thread)
        {
            Console.WriteLine ("InboxActivity onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                messageViewFragment = MessageViewFragment.newInstance (message);
                this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageViewFragment).AddToBackStack ("Message").Commit ();
            } else {
                var threadMessage = new NachoThreadedEmailMessages (McFolder.GetDefaultInboxFolder (NcApplication.Instance.Account.Id), thread.GetThreadId ());
                messageListFragment = MessageListFragment.newInstance (threadMessage);
                messageListFragment.onMessageClick += onMessageClick;
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Thread").Commit ();
            }
        }

        public void DoneWithMessage ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
            if (f is MessageListFragment) {
                // Don't pop if we are the top, e.g. Inbox
                if (1 < this.FragmentManager.BackStackEntryCount) {
                    this.FragmentManager.PopBackStack ();
                }
            }
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

        void MaybeSwitchAccount()
        {
            if ((null != account) && (NcApplication.Instance.Account.Id == account.Id)) {
                return;
            }
            FragmentManager.PopBackStackImmediate ("Inbox", PopBackStackFlags.None);
            account = NcApplication.Instance.Account;
            var messages = NcEmailManager.Inbox (account.Id);
            List<int> adds;
            List<int> deletes;
            messages.Refresh (out adds, out deletes);
            messageListFragment.SwitchAccount (messages);
        }
    }
}
