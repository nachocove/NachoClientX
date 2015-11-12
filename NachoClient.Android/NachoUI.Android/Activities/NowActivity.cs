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
    public class NowActivity : NcTabBarActivity, MessageListDelegate
    {

        NowFragment nowFragment;
        MessageListFragment messageListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            Log.Info (Log.LOG_UI, "NowActivity OnCreate");

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            base.OnCreate (bundle, Resource.Layout.NowActivity);

            nowFragment = new NowFragment ();
            nowFragment.onEventClick += onEventClick;
            nowFragment.onMessageClick += onMessageClick;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, nowFragment).AddToBackStack ("Now").Commit ();
        }

        public bool ShowHotEvent ()
        {
            return false;
        }

        public void SetActiveImage (Android.Views.View view)
        {
            // Highlight the tab bar icon of this activity
            var tabImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.hot_image);
            tabImage.SetImageResource (Resource.Drawable.nav_nachonow_active);
        }

        void onEventClick (object sender, McEvent ev)
        {
            StartActivity (EventViewActivity.ShowEventIntent (this, ev));
        }

        void onMessageClick (object sender, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "NowActivity onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
                StartActivity (intent);
            } else {
                var threadMessage = new NachoThreadedEmailMessages (McFolder.GetDefaultInboxFolder (NcApplication.Instance.Account.Id), thread.GetThreadId ());
                messageListFragment = MessageListFragment.newInstance (threadMessage);
                messageListFragment.onMessageClick += onMessageClick;
                FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack ("Inbox").Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
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

