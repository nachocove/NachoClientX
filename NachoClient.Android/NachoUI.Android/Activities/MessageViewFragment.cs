
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
                Log.Info (Log.LOG_UI, "MessageViewFragment: BodyDownloader_Finished: View is null");
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
            Log.Info (Log.LOG_UI, "SaveButton_Click");
            ShowFolderChooser ();
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ChiliButton_Click");
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (message, (Android.Widget.ImageView)sender);
        }

        void ArchiveButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ArchiveButton_Click");
            NcEmailArchiver.Archive (message);
            DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            NcEmailArchiver.Delete (message);
            DoneWithMessage ();
        }

        void ForwardButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ForwardButton_Click");
            StartComposeActivity (EmailHelper.Action.Forward);
        }

        void ReplyButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyButton_Click");
            StartComposeActivity (EmailHelper.Action.Reply);
        }

        void ReplyAllButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ReplyAllButton_Click");
            StartComposeActivity (EmailHelper.Action.ReplyAll);
        }

        void StartComposeActivity (EmailHelper.Action action)
        {
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            intent.PutExtra (MessageComposeActivity.EXTRA_ACTION, (int)action);
            intent.PutExtra (MessageComposeActivity.EXTRA_RELATED_MESSAGE_ID, message.Id);
            StartActivity (intent);
        }

        void View_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "View_Click");
        }

        public void ShowFolderChooser ()
        {
            Log.Info (Log.LOG_UI, "ShowFolderChooser: {0}", message);
            var folderFragment = ChooseFolderFragment.newInstance (null);
            folderFragment.setOnFolderSelected (OnFolderSelected);
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            folderFragment.Show (ft, "dialog");
        }

        public void OnFolderSelected (McFolder folder, McEmailMessageThread thread)
        {
            Log.Info (Log.LOG_UI, "OnFolderSelected: {0}", message);
            NcEmailArchiver.Move (message, folder);
            DoneWithMessage ();
        }

    }


    public class NachoWebViewClient : Android.Webkit.WebViewClient
    {
//        public override void OnReceivedError (Android.Webkit.WebView view, Android.Webkit.ClientError errorCode, string description, string failingUrl)
//        {
//            base.OnReceivedError (view, errorCode, description, failingUrl);
//            Log.Info (Log.LOG_UI, "OnReceivedError: {0}: {1} {2}", failingUrl, errorCode, description);
//        }
//
//        public override Android.Webkit.WebResourceResponse ShouldInterceptRequest (Android.Webkit.WebView view, Android.Webkit.IWebResourceRequest request)
//        {
//            Log.Info (Log.LOG_UI, "ShouldInterceptRequest: {1} {0}", request.Url, request.Method);
//            return base.ShouldInterceptRequest (view, request);
//        }
    }
}
