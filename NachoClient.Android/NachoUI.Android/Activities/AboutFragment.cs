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
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class AboutFragment : Fragment, AboutAdapter.Listener
    {
        #region Subviews

        RecyclerView RecyclerView;
        AboutAdapter ItemsAdapter;

        void FindSubviews (View view)
        {
            RecyclerView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
        }

        void ClearSubviews ()
        {
            RecyclerView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AboutFragment, container, false);
            FindSubviews (view);

            var context = RecyclerView.Context;
            RecyclerView.SetLayoutManager (new LinearLayoutManager (Context));
            ItemsAdapter = new AboutAdapter (this);
            RecyclerView.SetAdapter (ItemsAdapter);

            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region User Actions

        public void OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument document)
        {
            ShowLegalDocument (document);
        }

        #endregion

        #region Private Helpers

        void ShowLegalDocument (LegalDocumentActivity.LegalDocument document)
        {
            var intent = LegalDocumentActivity.BuildIntent (Activity, document);
            StartActivity (intent);
        }

        #endregion
    }

    #region Item Adapter

    class AboutAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument document);
        }

        int GeneralGroupPosition = 0;
        int GeneralItemCount = 1;
        int AboutPosition = 0;

        int LegalGroupPosition = 1;
        int LegalItemCount = 4;
        int ReleaseNotesPosition = 0;
        int PrivacyPolicyPosition = 1;
        int LicenseAgreementPosition = 2;
        int ContributionsPosition = 3;

        WeakReference<Listener> WeakListener;

        public AboutAdapter (Listener listener)
        {
            WeakListener = new WeakReference<Listener> (listener);
            Refresh ();
        }

        public void Refresh ()
        {
            NotifyDataSetChanged ();
        }

        public override int GroupCount {
            get {
                return 2;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == GeneralGroupPosition) {
                return GeneralItemCount;
            } else if (groupPosition == LegalGroupPosition) {
                return LegalItemCount;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AboutFragment.GroupItemCount: Unexpecetd group position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (int groupPosition)
        {
            return null;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == GeneralGroupPosition) {
                if (position == AboutPosition) {
                    return AboutItemViewHolder.VIEW_TYPE;
                }
            } else if (groupPosition == LegalGroupPosition) {
                if (position < LegalItemCount) {
                    return BasicItemViewHolder.VIEW_TYPE;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AboutFragment.GetItemViewType: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch (viewType) {
            case BasicItemViewHolder.VIEW_TYPE:
                return BasicItemViewHolder.Create (parent);
            case AboutItemViewHolder.VIEW_TYPE:
                return AboutItemViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AboutFragment.OnCreateGroupedViewHolder: Unexpecetd viewType: {0}", viewType));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var context = holder.ItemView.Context;
            if (groupPosition == GeneralGroupPosition) {
                if (position == AboutPosition) {
                    return;
                }
            } else if (groupPosition == LegalGroupPosition) {
                var basicHolder = (holder as BasicItemViewHolder);
                if (position == ReleaseNotesPosition) {
                    basicHolder.SetLabels (context.GetString (Resource.String.about_release_notes));
                    return;
                }else if (position == PrivacyPolicyPosition){
                    basicHolder.SetLabels (context.GetString (Resource.String.about_privacy_policy));
                    return;
                }else if (position == LicenseAgreementPosition){
                    basicHolder.SetLabels (context.GetString (Resource.String.about_license_agreement));
                    return;
                } else if (position == ContributionsPosition){
                    basicHolder.SetLabels (context.GetString (Resource.String.about_contributions));
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.OnBindViewHolder: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == LegalGroupPosition) {
                    if (position == ReleaseNotesPosition) {
                        listener.OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument.ReleaseNotes);
                    }else if (position == PrivacyPolicyPosition){
                        listener.OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument.PrivacyPolicy);
                    }else if (position == LicenseAgreementPosition){
                        listener.OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument.LicenseAgreement);
                    } else if (position == ContributionsPosition){
                        listener.OnLegalDocumentSelected (LegalDocumentActivity.LegalDocument.Contributions);
                    }
                }
            }
        }

        class BasicItemViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            public const int VIEW_TYPE = 1;

            TextView NameTextView;
            TextView DetailTextView;

            public static BasicItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.SettingsListBasicItem, parent, false);
                return new BasicItemViewHolder (view);
            }

            public BasicItemViewHolder (View view) : base (view)
            {
                NameTextView = view.FindViewById (Resource.Id.setting_name) as TextView;
                DetailTextView = view.FindViewById (Resource.Id.setting_detail) as TextView;
            }

            public void SetLabels (string name, string detail = null)
            {
                NameTextView.Text = name;
                if (String.IsNullOrEmpty (detail)) {
                    DetailTextView.Visibility = ViewStates.Gone;
                } else {
                    DetailTextView.Visibility = ViewStates.Visible;
                    DetailTextView.Text = detail;
                }
            }

        }

        class AboutItemViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            public const int VIEW_TYPE = 2;

            public static AboutItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.AboutListInfoItem, parent, false);
                return new AboutItemViewHolder (view);
            }

            public AboutItemViewHolder (View view) : base (view)
            {
                var versionTextView = view.FindViewById (Resource.Id.version) as TextView;
                versionTextView.Text = String.Format (view.Context.GetString (Resource.String.about_version_format), NcApplication.GetVersionString ());
            }
        }
    }

    #endregion

        /*
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
        */
}

