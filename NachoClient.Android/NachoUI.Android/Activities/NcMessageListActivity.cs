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
    [Activity (Label = "NcMessageListActivity", ScreenOrientation = Android.Content.PM.ScreenOrientation.Portrait)]            
    public class NcMessageListActivity : NcTabBarActivity, MessageListDelegate
    {
        protected McAccount account;
        MessageViewFragment messageViewFragment;
        MessageListFragment messageListFragment;

        protected virtual INachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            throw new NotImplementedException ();
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

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            account = NcApplication.Instance.Account;

            List<int> adds;
            List<int> deletes;
            var messages = GetMessages (out adds, out deletes);

            messageListFragment = MessageListFragment.newInstance (messages);
            messageListFragment.onMessageClick += onMessageClick;
            messageListFragment.onEventClick += MessageListFragment_onEventClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Inbox").Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            MaybeSwitchAccount ();
        }

        void MessageListFragment_onEventClick (object sender, McEvent ev)
        {
            StartActivity (EventViewActivity.ShowEventIntent (this, ev));
        }

        void onMessageClick (object sender, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "NcMessageListActivity onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                messageViewFragment = MessageViewFragment.newInstance (thread, message);
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
                ((MessageListFragment)f).OnBackPressed ();
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

        void MaybeSwitchAccount ()
        {
            if ((null != account) && (NcApplication.Instance.Account.Id == account.Id)) {
                return;
            }
            FragmentManager.PopBackStackImmediate ("Inbox", PopBackStackFlags.None);
            account = NcApplication.Instance.Account;
            List<int> adds;
            List<int> deletes;
            var messages = GetMessages (out adds, out deletes);
            messageListFragment.SwitchAccount (messages);
        }
    }
}
