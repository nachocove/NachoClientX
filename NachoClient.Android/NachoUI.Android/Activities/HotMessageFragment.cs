﻿using System;
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

        // Display first message of a thread in a cardview
        public static HotMessageFragment newInstance (McEmailMessageThread thread)
        {
            var fragment = new HotMessageFragment ();

            fragment.thread = thread;
            fragment.message = thread.FirstMessageSpecialCase ();

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
                Log.Info (Log.LOG_UI, "HotMessageFragment: BodyDownloader_Finished: View is null");
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
            var parent = (NowListActivity)this.Activity;
            parent.DoneWithMessage ();
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
            var intent = new Intent ();
            intent.SetClass (this.Activity, typeof(MessageComposeActivity));
            StartActivity (intent);
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

