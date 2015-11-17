
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
using System.IO;
using Android.Webkit;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class AboutViewerFragment : Fragment
    {
        public static AboutViewerFragment newInstance ()
        {
            var fragment = new AboutViewerFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AboutViewerFragment, container, false);

            var buttonBar = new ButtonBar (view);

            var title = AboutViewerActivity.TitleFromIntent (Activity.Intent);
            if (null != title) {
                buttonBar.SetTitle (title);
            }

            var tv = view.FindViewById<TextView> (Resource.Id.text_information);
            var wv = view.FindViewById<WebView> (Resource.Id.webview);

            var filename = AboutViewerActivity.FileFromIntent (Activity.Intent);
            if (null != filename) {
                string content;
                using (var sr = new StreamReader (Activity.Assets.Open (filename))) {
                    content = sr.ReadToEnd ();
                }
                tv.Text = content;
                wv.Visibility = ViewStates.Gone;
            }

            var url = AboutViewerActivity.UrlFromIntent (Activity.Intent);
            if (null != url) {
                // Hide scrollview, show spinner, until the load is finished
                var sv = view.FindViewById<View> (Resource.Id.scrollView);
                sv.Visibility = ViewStates.Invisible;
                tv.Visibility = ViewStates.Gone;
                wv.SetWebViewClient (new AboutViewerWebViewClient (view));
                wv.LoadUrl (url);
            }
            return view;
        }
    }

    public class AboutViewerWebViewClient : WebViewClient
    {
        View aboutViewerView;

        public AboutViewerWebViewClient (View view)
        {
            this.aboutViewerView = view;
        }

        public override void OnPageStarted (WebView view, string url, Android.Graphics.Bitmap favicon)
        {
            base.OnPageStarted (view, url, favicon);
            aboutViewerView.FindViewById<View> (Resource.Id.spinner).Visibility = ViewStates.Visible;
        }

        void Done ()
        {
            aboutViewerView.FindViewById<View> (Resource.Id.spinner).Visibility = ViewStates.Gone;
            aboutViewerView.FindViewById<View> (Resource.Id.scrollView).Visibility = ViewStates.Visible;
        }

        void ShowError ()
        {
            aboutViewerView.FindViewById<View> (Resource.Id.webview).Visibility = ViewStates.Gone;
            var tv = aboutViewerView.FindViewById<TextView> (Resource.Id.text_information);
            tv.SetText (Resource.String.download_error);
            tv.Visibility = ViewStates.Visible;
            Done ();
        }

        public override void OnReceivedError (WebView view, ClientError errorCode, string description, string failingUrl)
        {
            base.OnReceivedError (view, errorCode, description, failingUrl);
            Log.Error (Log.LOG_UI, "AboutViewer: {0} {1}", failingUrl, description);
            ShowError ();
        }

        public override void OnReceivedSslError (WebView view, SslErrorHandler handler, Android.Net.Http.SslError error)
        {
            base.OnReceivedSslError (view, handler, error);
            Log.Error (Log.LOG_UI, "AboutViewer: {0} {1}", error.Url, error.PrimaryError.ToString ());
            ShowError ();
        }

        public override void OnPageFinished (WebView view, string url)
        {
            base.OnPageFinished (view, url);
            Done ();
        }
    }
}
