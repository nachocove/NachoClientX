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
    public class HotMessageFragment : Android.App.Fragment
    {
        public event EventHandler<McEmailMessageThread> onMessageClick;

        McEmailMessage message;
        McEmailMessageThread thread;

        BodyDownloader bodyDownloader;

        public static HotMessageFragment newInstance (McEmailMessageThread thread)
        {
            var fragment = new HotMessageFragment ();

            fragment.thread = thread;
            fragment.message = thread.FirstMessageSpecialCase ();

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

            var webview = view.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);
            webview.SetOnTouchListener (new IgnoreTouchListener (view));

            bodyDownloader = new BodyDownloader ();

            BindValues (view);

            return view;
        }

        void BindValues (View view)
        {
            Bind.BindMessageHeader (thread, message, view);

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

        void BodyDownloader_Finished (object sender, string e)
        {
            bodyDownloader = null;

            message = (McEmailMessage)McAbstrItem.RefreshItem (message);

            if (null == View) {
                Console.WriteLine ("HotMessageFragment: BodyDownloader_Finished: View is null");
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
            var parent = (NowActivity)this.Activity;
            parent.DoneWithMessage ();
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
            if (null != onMessageClick) {
                onMessageClick (this, thread);
            }
        }
    }
}

