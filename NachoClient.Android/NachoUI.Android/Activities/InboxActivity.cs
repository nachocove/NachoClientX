
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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "InboxActivity")]            
    public class InboxActivity : NcActivity
    {
        MessageListFragment messageListFragment;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.InboxActivity);

            messageListFragment = new MessageListFragment ();
            messageListFragment.onMessageClick += MessageListFragment_onMessageClick;
            FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageListFragment).AddToBackStack("Inbox").Commit ();
        }

        void MessageListFragment_onMessageClick (object sender, int e)
        {
            Console.WriteLine ("MessageListFragment_onMessageClick: {0}", e);

            var messageViewFragment = new MessageViewFragment ();
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageViewFragment).AddToBackStack("View").Commit ();
        }

        public override void OnBackPressed ()
        {
            var f = FragmentManager.FindFragmentById (Resource.Id.content);
            if (f is MessageViewFragment) {
                this.FragmentManager.PopBackStack ();
            }
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}
