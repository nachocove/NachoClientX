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
        private const string NOW_FRAGMENT_TAG = "NowFragment";

        NowFragment nowFragment;

        protected override void OnCreate (Bundle bundle)
        {
            Log.Info (Log.LOG_UI, "NowActivity OnCreate");

            this.RequestedOrientation = Android.Content.PM.ScreenOrientation.Nosensor;

            base.OnCreate (bundle, Resource.Layout.NowActivity);

            nowFragment = null;
            if (null != bundle) {
                nowFragment = FragmentManager.FindFragmentByTag<NowFragment> (NOW_FRAGMENT_TAG);
            }
            if (null == nowFragment) {
                nowFragment = new NowFragment ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, nowFragment, NOW_FRAGMENT_TAG).AddToBackStack ("Now").Commit ();
            }
            nowFragment.onEventClick += NowFragment_onEventClick;
            nowFragment.onThreadClick += NowFragment_onThreadClick;
            nowFragment.onMessageClick += NowFragment_onMessageClick;
        }

        public void ListIsEmpty ()
        {
        }

        public bool ShowHotEvent ()
        {
            return false;
        }

        public int ShowListStyle ()
        {
            if (LoginHelpers.ShowHotCards ()) {
                return MessageListAdapter.CARDVIEW_STYLE;
            } else {
                return MessageListAdapter.LISTVIEW_STYLE;
            }
        }

        public void SetActiveImage (Android.Views.View view)
        {
            // Highlight the tab bar icon of this activity
            var tabImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.hot_image);
            tabImage.SetImageResource (Resource.Drawable.nav_nachonow_active);
        }

        void NowFragment_onEventClick (object sender, McEvent ev)
        {
            StartActivity (EventViewActivity.ShowEventIntent (this, ev));
        }

        void NowFragment_onThreadClick (object sender, INachoEmailMessages threadMessages)
        {
            var intent = MessageThreadActivity.ShowThreadIntent (this, threadMessages);
            StartActivity (intent);
        }

        void NowFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            var message = thread.FirstMessageSpecialCase ();
            var intent = MessageViewActivity.ShowMessageIntent (this, thread, message);
            StartActivity (intent);
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

        public override void MaybeSwitchAccount ()
        {
            base.MaybeSwitchAccount ();

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

