
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

        public MessageViewFragment (McEmailMessage message)
        {
            this.message = message;
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
                bodyRenderer.Start(webview, body, message.NativeBodyType);
            } else {
                bodyDownloader = new BodyDownloader (message);
                bodyDownloader.Finished += BodyDownloader_Finished;
                bodyDownloader.Start ();
            }
        }

        void BodyDownloader_Finished (object sender, string e)
        {
            bodyDownloader = null;

            message = (McEmailMessage) RefreshItem (message);

            var webview = View.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);

            if (null == e) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                var bodyRenderer = new BodyRenderer ();
                bodyRenderer.Start(webview, body, message.NativeBodyType);
            } else {
                webview.LoadData (e, "text/plain", null);
            }
        }

        private McAbstrItem RefreshItem (McAbstrItem item)
        {
            McAbstrItem refreshedItem;
            if (item is McEmailMessage) {
                refreshedItem = McEmailMessage.QueryById<McEmailMessage> (item.Id);
            } else if (item is McCalendar) {
                refreshedItem = McCalendar.QueryById<McCalendar> (item.Id);
            } else if (item is McException) {
                refreshedItem = McException.QueryById<McException> (item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (
                    string.Format ("Unhandled abstract item type {0}", item.GetType ().Name));
            }
            return refreshedItem;
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if((null != bodyDownloader) && bodyDownloader.HandleStatusEvent(s)) {
                return;
            }
        }

        void DoneWithMessage()
        {
            var parent = (InboxActivity)this.Activity;
            parent.DoneWithMessage ();
        }

        void SaveButton_Click (object sender, EventArgs e)
        {
            Console.WriteLine ("SaveButton_Click");
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
    }


    public class BodyRenderer
    {
        Android.Webkit.WebView webView;

        public BodyRenderer ()
        {
        }

        public void Start(Android.Webkit.WebView webView, McBody body, int nativeBodyType)
        {
            this.webView = webView;
 
            switch (body.BodyType) {
            case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                RenderTextString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                RenderHtmlString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                // FIXME
                break;
            case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                RenderMime (body, nativeBodyType);
                break;
            default:
                Log.Error (Log.LOG_UI, "Body {0} has an unknown body type {1}.", body.Id, (int)body.BodyType);
                RenderTextString (body.GetContentsString ());
                break;
            }
            this.webView = null;
        }

        void RenderTextString (string text)
        {
            webView.LoadData (text, "text/plain", null);
        }

        void RenderHtmlString (string html)
        {
            webView.LoadData (html, "text/html", null);
        }

        void RenderMime (McBody body, int nativeBodyType)
        {
            var mimeMessage = MimeHelpers.LoadMessage (body);
            var list = new List<MimeEntity> ();
            MimeHelpers.MimeDisplayList (mimeMessage, list, MimeHelpers.MimeTypeFromNativeBodyType (nativeBodyType));

            foreach (var entity in list) {
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtmlPart (part);
                    return;
                } else if (part.ContentType.Matches ("text", "rtf")) {
                    // FIXME
                } else if (part.ContentType.Matches ("text", "*")) {
                    RenderTextPart (part);
                } else if (part.ContentType.Matches ("image", "*")) {
                    // FIXME
                }
            }
        }

        void RenderTextPart (MimePart part)
        {
            RenderTextString ((part as TextPart).Text);
        }

        private void RenderHtmlPart (MimePart part)
        {
            RenderHtmlString ((part as TextPart).Text);
        }

    }

    public class BodyDownloader
    {
        McAbstrItem item;

        string downloadToken;

        public event EventHandler<string> Finished;

        public BodyDownloader (McAbstrItem item)
        {
            this.item = item;
        }

        public void Start ()
        {
            StartDownload ();
        }

        void StartDownload ()
        {
            // Download the body.
            NcResult nr;
            if (item is McEmailMessage) {
                nr = BackEnd.Instance.DnldEmailBodyCmd (item.AccountId, item.Id, true);
            } else if (item is McAbstrCalendarRoot) {
                nr = BackEnd.Instance.DnldCalBodyCmd (item.AccountId, item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (string.Format ("Unhandled abstract item type {0}", item.GetType ().Name));
            }
            downloadToken = nr.GetValue<string> ();

            if (null == downloadToken) {
                // FIXME: Race condition (see iOS)
                Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}", item.Id, item.AccountId);
                ReturnErrorMessage (nr);
                return;
            } else {
                McPending.Prioritize (item.AccountId, downloadToken);
            }
        }

        void ReturnSuccess ()
        {
            if (null != Finished) {
                Finished (this, null);
            }
        }

        void ReturnErrorMessage (NcResult nr)
        {
            string message;
            if (!ErrorHelper.ExtractErrorString (nr, out message)) {
                message = "Download failed.";
            }
            if (null != Finished) {
                Finished (this, message);
            }
        }

        public bool HandleStatusEvent (StatusIndEventArgs statusEvent)
        {
            if (null == statusEvent.Tokens) {
                return false;
            }
            if (statusEvent.Tokens.FirstOrDefault () != downloadToken) {
                return false;
            }

            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded:
            case NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded:
                ReturnSuccess ();
                break;
            case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
            case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:
                    // The McPending isn't needed any more.
                var localAccountId = item.AccountId;
                var localDownloadToken = downloadToken;
                NcTask.Run (delegate {
                    foreach (var request in McPending.QueryByToken (localAccountId, localDownloadToken)) {
                        if (McPending.StateEnum.Failed == request.State) {
                            request.Delete ();
                        }
                    }
                }, "DelFailedMcPendingBodyDnld");
                ReturnErrorMessage (NcResult.Error (statusEvent.Status.SubKind));
                break;
            }
            return true;
        }
    }

    public class NachoWebViewClient : Android.Webkit.WebViewClient
    {
        public override void OnReceivedError (Android.Webkit.WebView view, Android.Webkit.ClientError errorCode, string description, string failingUrl)
        {
            base.OnReceivedError (view, errorCode, description, failingUrl);
            Console.WriteLine ("OnReceivedError: {0}: {1} {2}", failingUrl, errorCode, description);
        }

        public override Android.Webkit.WebResourceResponse ShouldInterceptRequest (Android.Webkit.WebView view, Android.Webkit.IWebResourceRequest request)
        {
            Console.WriteLine ("ShouldInterceptRequest: {1} {0}", request.Url, request.Method);
            return base.ShouldInterceptRequest (view, request);
        }
    }
}
