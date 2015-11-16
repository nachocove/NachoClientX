
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
using Android.Content.PM;
using NachoCore.Brain;

namespace NachoClient.AndroidClient
{
    public interface IMessageViewFragmentOwner
    {
        void DoneWithMessage ();

        McEmailMessage MessageToView { get; }

        McEmailMessageThread ThreadToView { get; }
    }

    public class MessageViewFragment : Fragment, MessageDownloadDelegate
    {
        McEmailMessage message;
        McEmailMessageThread thread;
        NcEmailMessageBundle bundle;

        ButtonBar buttonBar;

        MessageDownloader messageDownloader;

        Android.Webkit.WebView webView;
        NachoWebViewClient webViewClient;

        public static MessageViewFragment newInstance (McEmailMessageThread thread, McEmailMessage message)
        {
            var fragment = new MessageViewFragment ();
            fragment.message = message;
            fragment.thread = thread;
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

            buttonBar = new ButtonBar (view);

            buttonBar.SetIconButton (ButtonBar.Button.Right1, Resource.Drawable.folder_move, SaveButton_Click);

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

            webView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webViewClient = new NachoWebViewClient ();
            webView.SetWebViewClient (webViewClient);

            return view;
        }

        public override void OnActivityCreated (Bundle savedInstanceState)
        {
            base.OnActivityCreated (savedInstanceState);

            thread = ((IMessageViewFragmentOwner)Activity).ThreadToView;
            message = ((IMessageViewFragmentOwner)Activity).MessageToView;

            // Refresh message to make sure it's still around
            message = McEmailMessage.QueryById<McEmailMessage>(message.Id);
            if (null == message) {
                ((IMessageViewFragmentOwner)Activity).DoneWithMessage ();
                return;
            }
            bundle = new NcEmailMessageBundle (message);

            NcBrain.MessageReadStatusUpdated (message, DateTime.UtcNow, 0.1);

            var inflater = Activity.LayoutInflater;
            var attachments = McAttachment.QueryByItem (message);
            var attachmentsView = View.FindViewById<LinearLayout> (Resource.Id.attachment_list_view);
            Bind.BindAttachmentListView (attachments, attachmentsView, inflater, AttachmentToggle_Click, AttachmentSelectedCallback, AttachmentErrorCallback);

            // MarkAsRead() will change the message from unread to read only if the body has been
            // completely downloaded, so it is safe to call it unconditionally.  We put the call
            // here, rather than in ConfigureAndLayout(), to handle the case where the body is
            // downloaded long after the message view has been opened.
            EmailHelper.MarkAsRead (thread);
        }

        public override void OnStart ()
        {
            base.OnStart ();

            BindValues (View);
        }

        public override void OnPause ()
        {
            base.OnPause ();
        }

        public  void AttachmentSelectedCallback (McAttachment attachment)
        {
            if (attachment.IsImageFile ()) {
                var viewerIntent = ImageViewActivity.ImageViewIntent (this.Activity, attachment.GetFileDirectory (), attachment.GetFileName ());
                StartActivity (viewerIntent);
                return;
            }

            // Look for a handler on the system.

            try {
                var myIntent = new Intent (Intent.ActionView);
                var file = new Java.IO.File (attachment.GetFilePath ()); 
                var extension = Android.Webkit.MimeTypeMap.GetFileExtensionFromUrl (Android.Net.Uri.FromFile (file).ToString ());
                var mimetype = Android.Webkit.MimeTypeMap.Singleton.GetMimeTypeFromExtension (extension);
                myIntent.SetDataAndType (Android.Net.Uri.FromFile (file), mimetype);
                var packageManager = this.Activity.PackageManager;
                var activities = packageManager.QueryIntentActivities (myIntent, PackageInfoFlags.MatchDefaultOnly);
                var isIntentSafe = 0 < activities.Count;
                if (isIntentSafe) {
                    StartActivity (myIntent);
                } else {
                    NcAlertView.ShowMessage (Activity, "Attachment", "No application can open this attachment.");
                }
            } catch (Exception e) {
                // TODO: handle exception
                String data = e.Message;
            }
        }

        public  void AttachmentErrorCallback (McAttachment attachment, NcResult nr)
        {
        }

        void AttachmentToggle_Click (object sender, EventArgs e)
        {
            var attachmentsView = View.FindViewById<View> (Resource.Id.attachment_list_view);
            Bind.ToggleAttachmentList (attachmentsView);
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (null, message, view);
            // The header view is shared between the message list view and the message detail view.
            // In the list view, the subject should be truncated to a single line.  In the detail
            // view, the full subject needs to be shown.
            view.FindViewById<TextView> (Resource.Id.subject).SetMaxLines (100);

            if (bundle.NeedsUpdate) {
                messageDownloader = new MessageDownloader ();
                messageDownloader.Bundle = bundle;
                messageDownloader.Delegate = this;
                messageDownloader.Download (message);
            } else {
                RenderBody ();
            }
        }

        public void MessageDownloadDidFinish (MessageDownloader downloader)
        {
            RenderBody ();
        }

        public void MessageDownloadDidFail (MessageDownloader downloader, NcResult result)
        {
            // TODO: show this inline, possibly with message preview (if available)
            // and give the user an option to retry if appropriate
            NcAlertView.ShowMessage (Activity, "Could not download message", "Sorry, we were unable to download the message.");
        }

        void RenderBody ()
        {
            if (bundle != null) {
                if (bundle.FullHtmlUrl != null) {
                    webView.LoadUrl (bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = bundle.FullHtml;
                    webView.LoadDataWithBaseURL (bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
            EmailHelper.MarkAsRead (thread);
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
            ((IMessageViewFragmentOwner)Activity).DoneWithMessage ();
        }

        void DeleteButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "DeleteButton_Click");
            NcEmailArchiver.Delete (message);
            ((IMessageViewFragmentOwner)Activity).DoneWithMessage ();
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
            StartActivity (MessageComposeActivity.RespondIntent (this.Activity, action, message.Id));
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
            ((IMessageViewFragmentOwner)Activity).DoneWithMessage ();
        }

    }

    public class AttachmentListViewAdapter : Android.Widget.BaseAdapter<object>
    {
        List<McAttachment> attachmentList;

        public AttachmentListViewAdapter (List<McAttachment> attachmentList)
        {
            this.attachmentList = attachmentList;
        }

        public override long GetItemId (int position)
        {
            return position;
        }

        public override int Count {
            get {
                return attachmentList.Count;
            }
        }

        public override object this [int position] {  
            get {
                return attachmentList [position];
            }
        }

        public override View GetView (int position, View convertView, ViewGroup parent)
        {
            View view = convertView; // re-use an existing view, if one is available
            if (view == null) {
                view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.AttachmentListViewCell, parent, false);
            }
            var info = attachmentList [position];

            return view;
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

        public override bool ShouldOverrideUrlLoading (Android.Webkit.WebView view, string url)
        {
            if (null == url) {
                return false;
            }
            try {
                var uri = Android.Net.Uri.Parse (url);
                var norm = uri.NormalizeScheme ();
                var scheme = norm.Scheme;
                if ("http" == scheme || "https" == scheme) {
                    view.Context.StartActivity (new Intent (Intent.ActionView, Android.Net.Uri.Parse (url)));
                    return true;
                }
            } catch (Exception ex) {
                Log.Info (Log.LOG_UI, "ShouldOverrideUrl: {0}", ex);
            }
            return false;
        }
    }
}
