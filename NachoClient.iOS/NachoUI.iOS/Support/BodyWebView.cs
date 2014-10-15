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

        public bool ScrollingEnabled;

        protected RecursionCounter htmlBusy;
        public RenderStart OnRenderStart;
        public RenderComplete OnRenderComplete;

        private SizeF _contentSize;
        public SizeF ContentSize {
            get {
                float scale = ScrollView.ZoomScale;
                return new SizeF (_contentSize.Width * scale, _contentSize.Height * scale);
            }
            protected set {
                _contentSize = value;
            }
        }

        public BodyWebView (IntPtr ptr) : base (ptr)
        {
        }

        public BodyWebView (UIView parentView, float leftMargin) : base ()
        {
            ViewHelper.SetDebugBorder (this, UIColor.Red);

            ViewFramer.Create (this)
                .X (0)
                .Y (0)
                .Width (parentView.Frame.Width)
                .Height (1);

            htmlBusy = new RecursionCounter (() => {
                EvaluateJavascript (magic);

                // Save the rendered content size
                _contentSize = ScrollView.ContentSize;

                // If the content size is less than the given frame, we set the frame to the content size
                ViewFramer.Create(this)
                    .Width (Math.Min (ContentSize.Width, parentView.Frame.Width))
                    .Height (Math.Min (ContentSize.Height, parentView.Frame.Height));

                // If the content is wider than the frame, try to scale it down
                OnRenderComplete (1.0f);
            });

            UserInteractionEnabled = false;
            ScrollView.Bounces = false;
            ScrollView.MultipleTouchEnabled = false;
            ContentMode = UIViewContentMode.ScaleAspectFit;
            BackgroundColor = UIColor.White;
            Tag = (int)BodyView.TagType.MESSAGE_PART_TAG;

            LoadStarted += (object sender, EventArgs e) => {
                htmlBusy.Increment ();
            };

            LoadFinished += (object sender, EventArgs e) => {
                htmlBusy.Decrement ();
            };

            LoadError += (object sender, UIWebErrorArgs e) => {
                OnRenderComplete (0.0f);
            };

            ShouldStartLoad += delegate(UIWebView webView, NSUrlRequest request, UIWebViewNavigationType navigationType) {
                if (UIWebViewNavigationType.LinkClicked == navigationType) {
                    UIApplication.SharedApplication.OpenUrl (request.Url);
                    return false;
                }
                return true;
            };
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
            // TODO - Add sanity check
            ScrollView.SetContentOffset (upperLeft, false);
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyWebView: offset=({0},{1})  frame=({2},{3})  content=({4},{5})",
                Frame.X, Frame.Y, Frame.Width, Frame.Height, ContentSize.Width, ContentSize.Height);
        }
    }
}
