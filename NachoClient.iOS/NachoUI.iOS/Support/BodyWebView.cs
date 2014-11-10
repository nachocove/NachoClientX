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
        private float preferredWidth;
        private Action sizeChangedCallback;

        private const string magic = @"
            var style = document.createElement(""style""); 
            document.head.appendChild(style); 
            style.innerHTML = ""html{{-webkit-text-size-adjust: auto; word-wrap: break-word;}}"";
            var viewPortTag=document.createElement('meta');
            viewPortTag.id=""viewport"";
            viewPortTag.name = ""viewport"";
            viewPortTag.content = ""width={0}; initial-scale=1.0;"";
            document.getElementsByTagName('head')[0].appendChild(viewPortTag);
        ";
        private const string dispableJavaScript = "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">";
        private const string wrapPre = "<style>pre { white-space: pre-wrap;}</style>";

        public BodyWebView (float Y, float preferredWidth, float initialHeight, Action sizeChangedCallback, string html, NSUrl baseUrl)
            : base (new RectangleF(0, Y, preferredWidth, initialHeight))
        {
            this.preferredWidth = preferredWidth;
            this.sizeChangedCallback = sizeChangedCallback;

            ScrollView.ScrollEnabled = false;
            LoadFinished += OnLoadFinished;
            ShouldStartLoad += OnShouldStartLoad;

            LoadHtmlString (dispableJavaScript + wrapPre + html, baseUrl);
        }

        protected override void Dispose (bool disposing)
        {
            StopLoading ();
            LoadFinished -= OnLoadFinished;
            ShouldStartLoad -= OnShouldStartLoad;
            base.Dispose (disposing);
        }

        public UIView uiView ()
        {
            return this;
        }

        public SizeF ContentSize {
            get {
                return ScrollView.ContentSize;
            }
        }

        public void ScrollingAdjustment (RectangleF frame, PointF contentOffset)
        {
            if (frame.Width < preferredWidth) {
                // Changing the width of the UIWebView will change the layout.
                // Making the width more narrow can have disastrous effects.
                float expandWidthBy = preferredWidth - frame.Width;
                frame.X -= expandWidthBy;
                contentOffset.X -= expandWidthBy;
                frame.Width += expandWidthBy;
            }
            this.Frame = frame;
            this.ScrollView.ContentOffset = contentOffset;
        }

        private void OnLoadFinished (object sender, EventArgs e)
        {
            EvaluateJavascript (string.Format(magic, preferredWidth));
            // Force a re-layout of this web view now that the JavaScript magic has been applied.
            ViewFramer.Create (this).Height (2);
            // And force a re-layout of the entire BodyView now that the size of this web view is known.
            if (null != sizeChangedCallback) {
                sizeChangedCallback ();
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
    }
}
