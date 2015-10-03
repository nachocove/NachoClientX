
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

using MimeKit;

namespace NachoClient.AndroidClient
{
    public class MessageViewFragment : Fragment
    {
        McEmailMessage message;

        BodyDownloader bodyDownloader;

        public static MessageViewFragment newInstance (McEmailMessage message)
        {
            var fragment = new MessageViewFragment ();
            fragment.message = message;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);

            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.MessageViewFragment, container, false);

            var saveButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            saveButton.SetImageResource (Resource.Drawable.folder_move);
            saveButton.Visibility = Android.Views.ViewStates.Visible;
            saveButton.Click += SaveButton_Click;

            view.Click += View_Click;

            var replyButton = view.FindViewById (Resource.Id.reply);
            replyButton.Click += ReplyButton_Click;

            var replyAllButton = view.FindViewById (Resource.Id.reply_all);
            replyAllButton.Click += ReplyAllButton_Click;

            var forwardButton = view.FindViewById (Resource.Id.forward);
            forwardButton.Click += ForwardButton_Click;

            var archiveButton = view.FindViewById (Resource.Id.archive);
            archiveButton.Click += ArchiveButton_Click;

            var deleteButton = view.FindViewById (Resource.Id.delete);
            deleteButton.Click += DeleteButton_Click;

            var chiliButton = view.FindViewById (Resource.Id.chili);
            chiliButton.Click += ChiliButton_Click;

            var webview = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            var webclient = new NachoWebViewClient ();
            webview.SetWebViewClient (webclient);

            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            BindValues (View);
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (null, message, view);

            var body = McBody.QueryById<McBody> (message.BodyId);

            if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                var webview = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
                var bodyRenderer = new BodyRenderer ();
                bodyRenderer.Start (webview, body, message.NativeBodyType);
            } else {
                bodyDownloader = new BodyDownloader ();
                bodyDownloader.Finished += BodyDownloader_Finished;
                bodyDownloader.Start (message);
            }
        }

        void BodyDownloader_Finished (object sender, string e)
        {
            bodyDownloader = null;

            message = (McEmailMessage)McAbstrItem.RefreshItem (message);

            if (null == View) {
                Console.WriteLine ("MessageViewFragment: BodyDownloader_Finished: View is null");
                return;
            }

            var webview = View.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);

            if (null == e) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                var bodyRenderer = new BodyRenderer ();
                bodyRenderer.Start (webview, body, message.NativeBodyType);
            } else {
                webview.LoadData (e, "text/plain", null);
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if ((null != bodyDownloader) && bodyDownloader.HandleStatusEvent (s)) {
                return;
            }
        }

        void DoneWithMessage ()
        {
            var parent = (InboxActivity)this.Activity;
            parent.DoneWithMessage ();
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("SaveButton_Click");
            ShowFolderChooser ();
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ChiliButton_Click");
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ArchiveButton_Click");
            NcEmailArchiver.Archive (message);
            DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("DeleteButton_Click");
            NcEmailArchiver.Delete (message);
            DoneWithMessage ();
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ForwardButton_Click");
            StartComposeActivity (EmailHelper.Action.Forward);
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ReplyButton_Click");
            StartComposeActivity (EmailHelper.Action.Reply);
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("ReplyAllButton_Click");
            StartComposeActivity (EmailHelper.Action.ReplyAll);
        }

        void StartComposeActivity (EmailHelper.Action action)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
        }

        void View_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("View_Click");
        }

        public void ShowFolderChooser ()
        {
            Console.WriteLine ("ShowFolderChooser: {0}", message);
            var folderFragment = ChooseFolderFragment.newInstance (null);
            folderFragment.setOnFolderSelected (OnFolderSelected);
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            folderFragment.Show (ft, "dialog");
        }

        public void OnFolderSelected (McFolder folder, McEmailMessageThread thread)
        {
            Console.WriteLine ("OnFolderSelected: {0}", message);
            NcEmailArchiver.Move (message, folder);
            DoneWithMessage ();
        }

    }


    public class NachoWebViewClient : Android.Webkit.WebViewClient
    {
//        public override void OnReceivedError (Android.Webkit.WebView view, Android.Webkit.ClientError errorCode, string description, string failingUrl)
//        {
//            base.OnReceivedError (view, errorCode, description, failingUrl);
//            Console.WriteLine ("OnReceivedError: {0}: {1} {2}", failingUrl, errorCode, description);
//        }
//
//        public override Android.Webkit.WebResourceResponse ShouldInterceptRequest (Android.Webkit.WebView view, Android.Webkit.IWebResourceRequest request)
//        {
//            Console.WriteLine ("ShouldInterceptRequest: {1} {0}", request.Url, request.Method);
//            return base.ShouldInterceptRequest (view, request);
//        }
    }
}
