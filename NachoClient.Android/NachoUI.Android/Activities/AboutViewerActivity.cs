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
    [Activity (Label = "AboutViewerActivity")]
    public class AboutViewerActivity : NcActivity
    {
        private const string ABOUTVIEWER_FRAGMENT_TAG = "AboutViewerFragment";

        private const string EXTRA_ABOUT_URL = "com.nachocove.nachomail.EXTRA_ABOUT_URL";
        private const string EXTRA_ABOUT_FILE = "com.nachocove.nachomail.EXTRA_ABOUT_FILE";
        private const string EXTRA_ABOUT_TITLE = "com.nachocove.nachomail.EXTRA_ABOUT_TITLE";


        public static Intent ShowAboutUrlIntent(Context context, string title, string name)
        {
            var intent = new Intent (context, typeof(AboutViewerActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ABOUT_URL, name);
            intent.PutExtra (EXTRA_ABOUT_TITLE, title);
            return intent;
        }

        public static Intent ShowAboutFileIntent(Context context, string title, string name)
        {
            var intent = new Intent (context, typeof(AboutViewerActivity));
            intent.SetAction (Intent.ActionView);
            intent.PutExtra (EXTRA_ABOUT_FILE, name);
            intent.PutExtra (EXTRA_ABOUT_TITLE, title);
            return intent;
        }

        public static string UrlFromIntent(Intent intent)
        {
            if(intent.HasExtra(EXTRA_ABOUT_URL)) {
                return intent.GetStringExtra(EXTRA_ABOUT_URL);
            } else {
                return null;
            }
        }

        public static string FileFromIntent(Intent intent)
        {
            if(intent.HasExtra(EXTRA_ABOUT_FILE)) {
                return intent.GetStringExtra (EXTRA_ABOUT_FILE);
            } else {
                return null;
            }
        }

        public static string TitleFromIntent(Intent intent)
        {
            if(intent.HasExtra(EXTRA_ABOUT_TITLE)) {
                return intent.GetStringExtra (EXTRA_ABOUT_TITLE);
            } else {
                return null;
            }
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView(Resource.Layout.AboutViewerActivity);

            if (null == bundle || null == FragmentManager.FindFragmentByTag<AboutViewerFragment> (ABOUTVIEWER_FRAGMENT_TAG)) {
                var aboutViewerFragment = AboutViewerFragment.newInstance ();
                FragmentManager.BeginTransaction ().Replace (Resource.Id.content, aboutViewerFragment, ABOUTVIEWER_FRAGMENT_TAG).Commit ();
            }
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

    }
}
