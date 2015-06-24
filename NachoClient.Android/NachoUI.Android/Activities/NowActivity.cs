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
    public class NowActivity : NcActivity
    {

        NowFragment nowFragment = new NowFragment ();

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.NowActivity);

            nowFragment.onMessageClick += NowFragment_onMessageClick;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, nowFragment).AddToBackStack ("Now").Commit ();
        }

        void NowFragment_onMessageClick (object sender, McEmailMessageThread thread)
        {
            Console.WriteLine ("MessageListFragment_onMessageClick: {0}", thread);

            if (1 == thread.MessageCount) {
                var message = thread.FirstMessageSpecialCase ();
                MessageThreadFragment_onMessageClick (sender, message);
                return;
            }

            var messageThreadFragment = new MessageThreadFragment (thread);
            messageThreadFragment.onMessageClick += MessageThreadFragment_onMessageClick;

            FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageThreadFragment).AddToBackStack ("Inbox").Commit ();
        }

        void MessageThreadFragment_onMessageClick (object sender, McEmailMessage message)
        {
            Console.WriteLine ("MessageThreadFragment_onMessageClick: {0}", message);
            var messageViewFragment = new MessageViewFragment (message);
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageViewFragment).AddToBackStack ("View").Commit ();
        }

        public void DoneWithMessage()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
            if (f is MessageThreadFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
       
    }
}

