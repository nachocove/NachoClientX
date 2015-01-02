//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    public class BodyWebView : UIWebView, IBodyRender
    {
        private string html;
        private NSUrl baseUrl;
        private float preferredWidth;
        private Action sizeChangedCallback;
        private bool loadingComplete;
        private BodyView.LinkSelectedCallback onLinkSelected;

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
        private const string disableJavaScript = "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">";
        private const string wrapPre = "<style>pre { white-space: pre-wrap;}</style>";

        public BodyWebView (float Y, float preferredWidth, float initialHeight, Action sizeChangedCallback, string html, NSUrl baseUrl, BodyView.LinkSelectedCallback onLinkSelected)
            : base (new RectangleF(0, Y, preferredWidth, initialHeight))
        {
            this.html = html;
            this.baseUrl = baseUrl;
            this.preferredWidth = preferredWidth;
            this.sizeChangedCallback = sizeChangedCallback;
            this.DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber;
            this.onLinkSelected = onLinkSelected;

            ScrollView.ScrollEnabled = false;
            LoadFinished += OnLoadFinished;
            ShouldStartLoad += OnShouldStartLoad;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            loadingComplete = false;

            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                LoadHtmlString (disableJavaScript + wrapPre + html, baseUrl);
            }
        }

        protected override void Dispose (bool disposing)
        {
            StopLoading ();
            onLinkSelected = null;
            LoadFinished -= OnLoadFinished;
            ShouldStartLoad -= OnShouldStartLoad;
            if (!loadingComplete) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            }
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
            if (!this.Frame.Equals (frame)) {
                this.Frame = frame;
            }
            if (!this.ScrollView.ContentOffset.Equals (contentOffset)) {
                this.ScrollView.ContentOffset = contentOffset;
            }
        }

        private void OnLoadFinished (object sender, EventArgs e)
        {
            loadingComplete = true;
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            EvaluateJavascript (string.Format(magic, preferredWidth));
            // Force a re-layout of this web view now that the JavaScript magic has been applied.
            ViewFramer.Create (this).Height (Frame.Height - 1);
            // And force a re-layout of the entire BodyView now that the size of this web view is known.
            if (null != sizeChangedCallback) {
                sizeChangedCallback ();
            }
        }

        private bool OnShouldStartLoad (UIWebView webView, NSUrlRequest request,
            UIWebViewNavigationType navigationType)
        {
            if (UIWebViewNavigationType.LinkClicked == navigationType) {
                if (null != onLinkSelected) {
                    onLinkSelected (request.Url);
                }
                return false;
            }
            return true;
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == statusEvent.Status.SubKind) {
                if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                    // If the web view loading was interrupted by the app going into
                    // the background, then restart it.
                    if (!loadingComplete && !base.IsLoading) {
                        LoadHtmlString (disableJavaScript + wrapPre + html, baseUrl);
                    }
                } else {
                    // The app is going into the background.  Stop any loading that
                    // is in progress.  But leave loadingComplete == false, so we know
                    // to restart loading when the app comes back into the foreground.
                    StopLoading ();
                }
            }
        }
    }
}
