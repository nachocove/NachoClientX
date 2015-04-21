//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore.Utils;
using NachoCore;

namespace NachoClient.iOS
{
    /// <summary>
    /// Abstract class for managing a UIWebView within a BodyView.  This class handles everything about
    /// the UIWebView except for the actual data to be displayed.  The derived classes should be customized
    /// for specific formats for the data, such as HTML or RTF.
    /// </summary>
    public abstract class BodyWebView : UIWebView, IBodyRender
    {
        protected NSUrl baseUrl;
        protected nfloat preferredWidth;
        private Action sizeChangedCallback;
        private bool loadingComplete;
        private BodyView.LinkSelectedCallback onLinkSelected;

        public BodyWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, NSUrl baseUrl, BodyView.LinkSelectedCallback onLinkSelected)
            : base (new CGRect(0, Y, preferredWidth, initialHeight))
        {
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
        }

        /// <summary>
        /// Have the UIWebView load the content to be displayed.
        /// </summary>
        protected abstract void LoadContent ();

        /// <summary>
        /// Make any necessary adjustments to the content or the layout after the initial loading is complete.
        /// </summary>
        protected abstract void PostLoadAdjustment ();

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

        public CGSize ContentSize {
            get {
                return ScrollView.ContentSize;
            }
        }

        public void ScrollingAdjustment (CGRect frame, CGPoint contentOffset)
        {
            if (frame.Width < preferredWidth) {
                // Changing the width of the UIWebView will change the layout.
                // Making the width more narrow can have disastrous effects.
                nfloat expandWidthBy = preferredWidth - frame.Width;
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
            PostLoadAdjustment ();
            // Force a re-layout of this web view now that the JavaScript magic has been applied.
            // The ScrollView.ContentSize is never smaller than the frame size, so in order to
            // figure out how big the content really is, we have to set the frame height to a
            // small number.
            ViewFramer.Create (this).Height (1);
            // Force a re-layout of the entire BodyView now that the size of this web view is known.
            // This web view's frame will be adjusted as part of that.
            if (null != sizeChangedCallback) {
                sizeChangedCallback ();
            } else {
                // There is no callback to force the BodyView to re-layout.
                ViewFramer.Create (this).Height (ScrollView.ContentSize.Height);
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
                        LoadContent ();
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

    /// <summary>
    /// Display HTML in a UIWebView.
    /// </summary>
    public class BodyHtmlWebView : BodyWebView
    {
        private string html;

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

        public BodyHtmlWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string html, NSUrl baseUrl, BodyView.LinkSelectedCallback onLinkSelected)
            : base (Y, preferredWidth, initialHeight, sizeChangedCallback, baseUrl, onLinkSelected)
        {
            this.html = html;

            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                LoadContent ();
            }
        }

        protected override void LoadContent ()
        {
            LoadHtmlString (disableJavaScript + wrapPre + html, baseUrl);
        }

        protected override void PostLoadAdjustment ()
        {
            EvaluateJavascript (string.Format (magic, preferredWidth));
        }
    }

    /// <summary>
    /// Display RTF in a UIWebView.
    /// </summary>
    public class BodyRtfWebView : BodyWebView
    {
        private string rtf;

        public BodyRtfWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string rtf, NSUrl baseUrl, BodyView.LinkSelectedCallback onLinkSelected)
            : base (Y, preferredWidth, initialHeight, sizeChangedCallback, baseUrl, onLinkSelected)
        {
            this.rtf = rtf;

            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                LoadContent ();
            }
        }

        protected override void LoadContent ()
        {
            LoadData (NSData.FromString (rtf, NSStringEncoding.UTF8), "text/rtf", "utf-8", baseUrl);
        }

        protected override void PostLoadAdjustment ()
        {
            // No adjustment is necessary.
        }
    }
}
