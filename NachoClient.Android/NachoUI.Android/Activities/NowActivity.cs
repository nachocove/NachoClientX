using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;
using Android.Graphics;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "NowActivity")]            
    public class NowActivity : NcTabBarActivity
    {

        NowFragment nowFragment;
        MessageViewFragment messageViewFragment;
        MessageListFragment messageListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            Log.Info (Log.LOG_UI, "NowActivity OnCreate");

            base.OnCreate (bundle, Resource.Layout.NowActivity);

            nowFragment = new NowFragment ();
            nowFragment.onMessageClick += onMessageClick;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, nowFragment).AddToBackStack ("Now").Commit ();
        }

        void onMessageClick (object sender, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "NowActivity onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                messageViewFragment = MessageViewFragment.newInstance (message);
                this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageViewFragment).AddToBackStack ("View").Commit ();
            } else {
                var threadMessage = new NachoThreadedEmailMessages (McFolder.GetDefaultInboxFolder (NcApplication.Instance.Account.Id), thread.GetThreadId ());
                messageListFragment = MessageListFragment.newInstance (threadMessage);
                messageListFragment.onMessageClick += onMessageClick;
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Inbox").Commit ();
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
                if (1 < this.FragmentManager.BackStackEntryCount) {
                    this.FragmentManager.PopBackStack ();
                }
            }
        }

        public override void SwitchAccount (McAccount account)
        {
            base.SwitchAccount (account);

            FragmentManager.PopBackStackImmediate ("Now", PopBackStackFlags.None);
            if (null != nowFragment) {
                nowFragment.SwitchAccount ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
       
    }
}

