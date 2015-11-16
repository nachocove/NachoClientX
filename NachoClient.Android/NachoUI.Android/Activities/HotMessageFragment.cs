using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class HotMessageFragment : Android.App.Fragment, MessageDownloadDelegate
    {
        public event EventHandler<McEmailMessageThread> onMessageClick;

        McEmailMessage message;
        McEmailMessageThread thread;
        NcEmailMessageBundle bundle;

        MessageDownloader messageDownloader;

        Android.Webkit.WebView webView;

        // Display first message of a thread in a cardview
        public static HotMessageFragment newInstance (McEmailMessageThread thread)
        {
            var fragment = new HotMessageFragment ();

            fragment.thread = thread;
            fragment.message = thread.FirstMessageSpecialCase ();
            fragment.bundle = new NcEmailMessageBundle (fragment.message);

            // Hot query returns single messages for threads so
            // fix up the number of messages in the thread here
            fragment.thread.UpdateThreadCount (fragment.message.ConversationId);

            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.HotMessageFragment, container, false);

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
            webView.SetOnTouchListener (new IgnoreTouchListener (view));

            BindValues (view);

            return view;
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (thread, message, view);

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
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            View view;

            public IgnoreTouchListener (View view)
            {
                this.view = view;
            }

            public bool OnTouch (View v, MotionEvent e)
            {
//                view.OnTouchEvent (e);
                return false;
            }
        }

        void DoneWithMessage ()
        {
        }

        void ChiliButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "ChiliButton_Click");
            NachoCore.Utils.ScoringHelpers.ToggleHotOrNot (message);
            Bind.BindMessageChili (thread, message, (Android.Widget.ImageView)sender);
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
            StartActivity (MessageComposeActivity.RespondIntent (this.Activity, action, thread.FirstMessageId));
        }

        void View_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "View_Click");
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }
    }
}

