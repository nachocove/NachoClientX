//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyWebView : UIWebView
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

        public BodyWebView (UIView parentView, float leftMargin) : base (parentView.Frame)
        {
            ViewFramer.Create (this).X (leftMargin).Height (1);
            htmlBusy = new RecursionCounter (() => {
                EvaluateJavascript (magic);
                // Adjust scroll view minimum zoom scale based on the content. Make sure 
                // that we cannot pinch (zoom out) to the point that the content is narrower
                // than 95% of the device screen width.
                float minZoomScale;
                if (ScrollView.ContentSize.Width < (0.95f * parentView.Bounds.Width)) {
                    minZoomScale = 1.0f;
                } else {
                    minZoomScale = (0.95f * parentView.Bounds.Width) / ScrollView.ContentSize.Width;
                }
                if (!ScrollingEnabled) {
                    ViewFramer.Create (this)
                        .Width (ScrollView.ContentSize.Width)
                        .Height (ScrollView.ContentSize.Height);
                } else {
                    // Make frame bigger so that after body view scroll view scale the 
                    // content down, the frame will still be the original size.
                    ViewFramer.Create (this)
                        .Width (Frame.Width / minZoomScale)
                        .Height (Frame.Height / minZoomScale);
                }
                OnRenderComplete (minZoomScale);
            });

            ScrollView.Bounces = false;
            ScrollView.ScrollEnabled = true;
            ScrollView.PagingEnabled = false;
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
    }
}
