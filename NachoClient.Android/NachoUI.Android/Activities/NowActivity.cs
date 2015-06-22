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

namespace NachoClient.AndroidClient
{
    [Activity (Label = "NowActivity")]            
    public class NowActivity : NcActivity
    {

        NowFragment nowFragment = new NowFragment();

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle, Resource.Layout.NowActivity);

            nowFragment.onMessageClick += NowFragment_onMessageClick;
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, nowFragment).AddToBackStack("Now").Commit ();
        }

        void NowFragment_onMessageClick (object sender, int e)
        {
            Console.WriteLine ("NowFragment_onMessageClick: {0}", e);

            var messageViewFragment = new MessageViewFragment ();
            this.FragmentManager.BeginTransaction ().Add (Resource.Id.content, messageViewFragment).AddToBackStack ("View").Commit ();
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

