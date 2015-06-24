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

        public HotMessageFragment (McEmailMessageThread thread) : base ()
        {
            this.thread = thread;
            this.message = thread.FirstMessageSpecialCase ();

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
//            webview.SetScrollContainer(false);
//            webview.OverScrollMode = OverScrollMode.Never;

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
                bodyDownloader = new BodyDownloader (message);
                bodyDownloader.Finished += BodyDownloader_Finished;
                bodyDownloader.Start ();
            }
        }

        public class IgnoreTouchListener : Java.Lang.Object, View.IOnTouchListener
        {
            View view;

            public IgnoreTouchListener(View view)
            {
                this.view = view;
            }

            public bool OnTouch (View v, MotionEvent e)
            {
                view.OnTouchEvent (e);
                return false;
            }
        }
            
        void BodyDownloader_Finished (object sender, string e)
        {
            bodyDownloader = null;

            message = (McEmailMessage)RefreshItem (message);

            var webview = View.FindViewById<Android.Webkit.WebView> (Resource.Id.webview);

            if (null == e) {
                var body = McBody.QueryById<McBody> (message.BodyId);
                var bodyRenderer = new BodyRenderer ();
                bodyRenderer.Start (webview, body, message.NativeBodyType);
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

            if ((null != bodyDownloader) && bodyDownloader.HandleStatusEvent (s)) {
                return;
            }
        }

        void DoneWithMessage()
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

    public class WebViewNoScroll : Android.Webkit.WebView
    {
        protected WebViewNoScroll (IntPtr javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr, int defStyleRes) : base(context, attrs, defStyleAttr, defStyleRes)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr, bool privateBrowsing) : base(context, attrs, defStyleAttr, privateBrowsing)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs, int defStyleAttr) : base(context, attrs, defStyleAttr)
        {
        }

        public WebViewNoScroll (Context context, Android.Util.IAttributeSet attrs) : base(context, attrs)
        {
        }

        public WebViewNoScroll (Context context) : base(context)
        {
        }

        protected override bool OverScrollBy (int deltaX, int deltaY, int scrollX, int scrollY, int scrollRangeX, int scrollRangeY, int maxOverScrollX, int maxOverScrollY, bool isTouchEvent)
        {
            return false;
        }
    }
}

