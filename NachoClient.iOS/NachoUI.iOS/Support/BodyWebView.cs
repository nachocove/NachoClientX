//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore.Utils;
using NachoCore;
using System.IO;
using System.Collections.Concurrent;

namespace NachoClient.iOS
{
    /// <summary>
    /// Abstract class for managing a UIWebView within a BodyView.  This class handles everything about
    /// the UIWebView except for the actual data to be displayed.  The derived classes should be customized
    /// for specific formats for the data, such as HTML or RTF.
    /// </summary>
    public class BodyWebView : UIWebView, IBodyRender
    {
        public delegate void LinkSelectedCallback (NSUrl url);

        protected nfloat preferredWidth;
        private Action sizeChangedCallback;
        private bool loadingComplete;
        public LinkSelectedCallback OnLinkSelected;
        NcEmailMessageBundle Bundle;

        private static ConcurrentStack<BodyWebView> ReusableViews = new ConcurrentStack<BodyWebView> ();

        public static BodyWebView ResuableWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight)
        {
            BodyWebView webView;
            var frame = new CGRect (0, Y, preferredWidth, initialHeight);
            if (!ReusableViews.TryPop (out webView)) {
                webView = new BodyWebView (frame);
            } else {
                webView.Frame = frame;
            }
            webView.preferredWidth = preferredWidth;
            webView.InitListeners ();
            return webView;
        }

        public void EnqueueAsReusable ()
        {
            if (ReusableViews.Count < 3) {
                if (base.IsLoading) {
                    StopLoading ();
                }
                LoadFinished -= OnLoadFinished;
                ShouldStartLoad -= OnShouldStartLoad;
                if (!loadingComplete) {
                    NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                }
                EvaluateJavascript ("document.body.innerHTML = ''");
                OnLinkSelected = null;
                sizeChangedCallback = null;
                loadingComplete = false;
                ReusableViews.Push (this);
            } else {
                Dispose ();
            }
        }

        private BodyWebView (CGRect frame)
            : base (frame)
        {
            this.DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber | UIDataDetectorType.Address;
            ScrollView.ScrollEnabled = false;
            loadingComplete = false;
            InitListeners ();
        }

        private void InitListeners ()
        {
            LoadFinished += OnLoadFinished;
            ShouldStartLoad += OnShouldStartLoad;
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public void LoadBundle (NcEmailMessageBundle bundle, Action onLoad)
        {
            Bundle = bundle;
            sizeChangedCallback = onLoad;
            LoadContent ();
        }

        private void LoadContent ()
        {
            if (Bundle != null) {
                if (Bundle.FullHtmlUrl == null) {
                    if (Bundle.FullHtml != null) {
                        var baseUrl = new NSUrl (Bundle.BaseUrl.ToString ());
                        LoadHtmlString (Bundle.FullHtml, baseUrl);
                    }
                } else {
                    var url = new NSUrl (Bundle.FullHtmlUrl.ToString ());
                    var request = new NSUrlRequest (url);
                    LoadRequest (request);
                }
            }
        }

        protected override void Dispose (bool disposing)
        {
            StopLoading ();
            OnLinkSelected = null;
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
            if (request.Url.Scheme.Equals ("x-apple-data-detectors")) {
                return true;
            }else if (UIWebViewNavigationType.LinkClicked == navigationType) {
                if (null != OnLinkSelected) {
                    OnLinkSelected (request.Url);
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


}
