//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyWebView : UIWebView, IBodyRender
    {
        public delegate void RenderStart ();
        public delegate void RenderComplete (float minZoomScale);

        const string magic = @"
            var style = document.createElement(""style""); 
            document.head.appendChild(style); 
            style.innerHTML = ""html{-webkit-text-size-adjust: auto; word-wrap: break-word;}"";
            var viewPortTag=document.createElement('meta');
            viewPortTag.id=""viewport"";
            viewPortTag.name = ""viewport"";
            viewPortTag.content = ""width=device-width; initial-scale=1.0;"";
            document.getElementsByTagName('head')[0].appendChild(viewPortTag);
        ";

        protected BodyRenderZoomRecognizer zoomRecognizer;
        protected RecursionCounter htmlBusy;
        public RenderStart OnRenderStart;
        public RenderComplete OnRenderComplete;

        public SizeF ContentSize {
            get {
                return ViewHelper.ScaleSizeF (ScrollView.ZoomScale, ScrollView.ContentSize);
            }
            protected set {
                ScrollView.ContentSize = value;
            }
        }

        public BodyWebView (IntPtr ptr) : base (ptr)
        {
        }

        public BodyWebView (UIView parentView, float leftMargin) : base ()
        {
            ViewHelper.SetDebugBorder (this, UIColor.Red);

            zoomRecognizer = new BodyRenderZoomRecognizer (ScrollView);
            zoomRecognizer.OnTap = () => {
                if ((null != OnRenderStart) && (null != OnRenderComplete)) {
                    OnRenderStart ();
                    OnRenderComplete (1.0f);
                }
            };
            AddGestureRecognizer (zoomRecognizer);

            ViewFramer.Create (this)
                .X (0)
                .Y (0);

            htmlBusy = new RecursionCounter (() => {
                EvaluateJavascript (magic);

                zoomRecognizer.Configure ();

                ViewFramer.Create (this)
                    .Size (parentView.Frame.Size);

                // If the content is wider than the frame, try to scale it down
                OnRenderComplete (1.0f);
            });

            ScrollView.ScrollEnabled = false;
            ScrollView.MinimumZoomScale = 0.5f;
            ScrollView.MaximumZoomScale = 4.0f;
            ScrollView.Bounces = false;
            ScrollView.MultipleTouchEnabled = false;
            ContentMode = UIViewContentMode.TopLeft;
            ScalesPageToFit = true;
            BackgroundColor = UIColor.White;
            Tag = (int)BodyView.TagType.MESSAGE_PART_TAG;

            LoadStarted += OnLoadStarted;
            LoadFinished += OnLoadFinished;
            LoadError += OnLoadError;
            ShouldStartLoad += OnShouldStartLoad;
            ScrollView.PinchGestureRecognizer.AddTarget (OnZoomChanged);

            // Disable all UIWebView double tap recognizers
            foreach (var gr in ScrollView.Subviews[0].GestureRecognizers) {
                var tapGr = gr as UITapGestureRecognizer;
                if (null == tapGr) {
                    continue;
                }
                if ((2 != tapGr.NumberOfTapsRequired) || (1 != tapGr.NumberOfTouchesRequired)) {
                    continue;
                }
                tapGr.Enabled = false;
            }
        }

        private void OnLoadStarted (object sender, EventArgs e)
        {
            htmlBusy.Increment ();
        }

        private void OnLoadFinished (object sender, EventArgs e)
        {
            htmlBusy.Decrement ();
        }

        private void OnLoadError (object sender, UIWebErrorArgs e)
        {
            OnRenderComplete (0.0f);
            Log.Error(Log.LOG_UI, "BodyWebView LoadError: {0}", e.Error);
        }

        private void OnZoomChanged ()
        {
            if ((null != OnRenderStart) && (null != OnRenderComplete)) {
                ScrollView.SetZoomScale (ScrollView.PinchGestureRecognizer.Scale, false);
                OnRenderStart ();
                OnRenderComplete (1.0f);
            }
        }

        private bool OnShouldStartLoad (UIWebView webView, NSUrlRequest request,
            UIWebViewNavigationType navigationType)
        {
            if (UIWebViewNavigationType.LinkClicked == navigationType) {
                UIApplication.SharedApplication.OpenUrl (request.Url);
                return false;
            }
            return true;
        }

        public override void SizeToFit ()
        {
            // Intentionally disable SizeToFit(). By allowing the base class SizeToFit() to
            // take effect, it will create a UIWebView with its frame equal to the content size
            // for large HTML email, it will consume a lot of memory. And ViewHelper.LayoutCursor
            // always call SizeToFit(). So, we need to disable it.
        }

        public void LoadHtmlString (string html)
        {
            NcAssert.True ((null != OnRenderStart) && (null != OnRenderComplete));

            htmlBusy.Reset ();
            OnRenderStart ();

            // Some CSS / CSP have to be added before render in order to be effective.
            string disable_js = "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">";
            string wrap_pre = "<style>pre { white-space: pre-wrap;}</style>";
            html = disable_js + wrap_pre + html;
            LoadHtmlString (html, null);
        }

        public void ScrollTo (PointF upperLeft)
        {
            ScrollView.SetContentOffset (upperLeft, false);
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyWebView: offset={0}  frame={1}  content={2}",
                Pretty.PointF (Frame.Location), Pretty.SizeF (Frame.Size), Pretty.SizeF (ContentSize));
        }
    }
}
