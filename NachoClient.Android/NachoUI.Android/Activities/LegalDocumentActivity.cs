using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V7.Widget;
using Android.Webkit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using System.IO;

namespace NachoClient.AndroidClient
{
    [Activity (ParentActivity=typeof(AboutActivity))]
    public class LegalDocumentActivity : NcActivity
    {

        public enum LegalDocument
        {
        	ReleaseNotes,
        	PrivacyPolicy,
        	LicenseAgreement,
        	Contributions
        }

        public const string EXTRA_NAME = "NachoClient.AndroidClient.LegalDocumentActivity.EXTRA_NAME";
        public const string EXTRA_FILENAME = "NachoClient.AndroidClient.LegalDocumentActivity.EXTRA_FILENAME";
        public const string EXTRA_URL = "NachoClient.AndroidClient.LegalDocumentActivity.EXTRA_URL";

        #region Intents

        private static Intent BuildIntent (Context context, string name, string filename = null, string url = null)
        {
            var intent = new Intent(context, typeof(LegalDocumentActivity));
            intent.PutExtra (EXTRA_NAME, name);
            if (filename != null) {
                intent.PutExtra (EXTRA_FILENAME, filename);
            }
            if (url != null) {
                intent.PutExtra (EXTRA_URL, url);
            }
            return intent;
        }

        public static Intent BuildReleaseNotesIntent (Context context)
        {
            return BuildIntent (context, context.GetString (Resource.String.about_release_notes), filename: "ReleaseNotes.txt");
        }

        public static Intent BuildPrivacyPolicyIntent (Context context)
        {
            return BuildIntent (context, context.GetString (Resource.String.about_privacy_policy), url: "http://nachocove.com/privacy-policy-text/");
        }

        public static Intent BuildLicenseAgreementIntent (Context context)
        {
            return BuildIntent (context, context.GetString (Resource.String.about_license_agreement), url: "http://nachocove.com/legal-text/");
        }

        public static Intent BuildContributionsIntent (Context context)
        {
            return BuildIntent (context, context.GetString (Resource.String.about_contributions), filename: "LegalInfo.txt");
        }

        public static Intent BuildIntent (Context context, LegalDocument document)
        {
            switch (document) {
            case LegalDocument.ReleaseNotes:
                return BuildReleaseNotesIntent (context);
            case LegalDocument.PrivacyPolicy:
                return BuildPrivacyPolicyIntent (context);
            case LegalDocument.LicenseAgreement:
                return BuildLicenseAgreementIntent (context);
            case LegalDocument.Contributions:
                return BuildContributionsIntent (context);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("LegalDocumentActivity.BuildIntent: unknown document: {0}", document));
        }

        #endregion

        #region Subviews

        Toolbar Toolbar;
        WebView WebView;

        private void FindSubviews ()
        {
            Toolbar = FindViewById (Resource.Id.toolbar) as Toolbar;
            WebView = FindViewById (Resource.Id.webview) as WebView;
        }

        private void ClearSubviews ()
        {
            Toolbar = null;
            WebView = null;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);
            SetContentView (Resource.Layout.LegalDocumentActivity);
            FindSubviews ();

            Toolbar.Title = Intent.Extras.GetString (EXTRA_NAME);

            SetSupportActionBar (Toolbar);
            SupportActionBar.SetDisplayHomeAsUpEnabled (true);

            LoadDocument ();
        }

        protected override void OnDestroy ()
        {
            ClearSubviews ();
            base.OnDestroy ();
        }

        #endregion

        void LoadDocument ()
        {
            if (Intent.Extras.ContainsKey (EXTRA_FILENAME)) {
                var filename = Intent.Extras.GetString (EXTRA_FILENAME);
                var plainText = "";
                using (var reader = new StreamReader (Assets.Open (filename))) {
                    plainText = reader.ReadToEnd ();
                }
                var deserializer = new HtmlTextDeserializer ();
                var document = deserializer.Deserialize (plainText);
                var style = document.CreateElement ("style");
                style.SetAttributeValue ("type", "text/css");
                style.AppendChild (document.CreateTextNode ("body { padding: 10px; font-family: sans-serif; }"));
                document.DocumentNode.FirstChild.AppendChild (style);
                using (var writer = new StringWriter ()) {
                    document.Save (writer);
                    WebView.LoadData (writer.ToString (), "text/html", "utf8");
                }
            } else if (Intent.Extras.ContainsKey (EXTRA_URL)) {
                var url = Intent.Extras.GetString (EXTRA_URL);
                WebView.LoadUrl (url);
            }
        }

    }
}
