
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class SupportFragment : Fragment
    {
        public static SupportFragment newInstance ()
        {
            var fragment = new SupportFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SupportFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var account = view.FindViewById (Resource.Id.account);
            account.Visibility = ViewStates.Invisible;

            var fmt = GetString (Resource.String.version_string);
            var versionView = view.FindViewById<TextView> (Resource.Id.version);
            versionView.Text = String.Format (fmt, NcApplication.GetVersionString ());

            var emailSupport = view.FindViewById<View> (Resource.Id.email_support);
            emailSupport.Click += EmailSupport_Click;

            var callSupport = view.FindViewById<View> (Resource.Id.call_support);
            callSupport.Click += CallSupport_Click;

            // Highlight the tab bar icon of this activity
            var moreImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);

            if (activity.Intent.HasExtra (SupportActivity.HIDE_TOOLBAR)) {
                var toolbar = view.FindViewById (Resource.Id.navigation_toolbar);
                toolbar.Visibility = ViewStates.Gone;
            }

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more);
            }
        }

        void CallSupport_Click (object sender, EventArgs e)
        {
            var number = Android.Net.Uri.Parse ("tel:19718036226");
            Intent callIntent = new Intent(Intent.ActionDial, number);
            StartActivity(callIntent);
        }

        void EmailSupport_Click (object sender, EventArgs e)
        {
            var parent = (SupportActivity)Activity;
            parent.EmailSupportClick ();
        }

    }
}

