
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
    public class AboutFragment : Fragment
    {
        public static AboutFragment newInstance ()
        {
            var fragment = new AboutFragment ();
            return fragment;
        }

        TextView releaseNotesView;
        TextView privacyPolicyView;
        TextView licenseAgreementView;
        TextView openSourceContributionsView;

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AboutFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var fmt = GetString (Resource.String.version_string);
            var versionView = view.FindViewById<TextView> (Resource.Id.version);
            versionView.Text = String.Format (fmt, NcApplication.GetVersionString ());

            // Highlight the tab bar icon of this activity
            var moreImage = view.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            moreImage.SetImageResource (Resource.Drawable.nav_more_active);

            releaseNotesView = view.FindViewById<TextView> (Resource.Id.release_notes);
            privacyPolicyView = view.FindViewById<TextView> (Resource.Id.privacy_policy);
            licenseAgreementView = view.FindViewById<TextView> (Resource.Id.license_agreement);
            openSourceContributionsView = view.FindViewById<TextView> (Resource.Id.open_source);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            releaseNotesView.Click += ReleaseNotesView_Click;
            privacyPolicyView.Click += PrivacyPolicyView_Click;
            licenseAgreementView.Click += LicenseAgreementView_Click;
            openSourceContributionsView.Click += OpenSourceContributionsView_Click;

            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more);
            }
        }

        public override void OnPause ()
        {
            base.OnResume ();
            releaseNotesView.Click -= ReleaseNotesView_Click;
            privacyPolicyView.Click -= PrivacyPolicyView_Click;
            licenseAgreementView.Click -= LicenseAgreementView_Click;
            openSourceContributionsView.Click -= OpenSourceContributionsView_Click;
        }

        void OpenSourceContributionsView_Click (object sender, EventArgs e)
        {
            StartActivity (AboutViewerActivity.ShowAboutFileIntent (Activity, "Open Source Contrbutions", "LegalInfo.txt"));
        }

        void LicenseAgreementView_Click (object sender, EventArgs e)
        {
            StartActivity(AboutViewerActivity.ShowAboutUrlIntent(Activity, "License Agreement", "https://nachocove.com/legal-text/"));
        }

        void PrivacyPolicyView_Click (object sender, EventArgs e)
        {
            StartActivity(AboutViewerActivity.ShowAboutUrlIntent(Activity, "Privacy Policy", "https://nachocove.com/privacy-policy-text/"));
        }

        void ReleaseNotesView_Click (object sender, EventArgs e)
        {
            StartActivity (AboutViewerActivity.ShowAboutFileIntent (Activity, "Release Notes", "ReleaseNotes.txt"));
        }
      

    }
}

