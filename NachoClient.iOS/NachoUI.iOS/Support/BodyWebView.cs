//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using CoreGraphics;
using Foundation;
using UIKit;
using NachoCore.Utils;
using NachoCore;
using System.IO;

namespace NachoClient.iOS
{
    /// <summary>
    /// Abstract class for managing a UIWebView within a BodyView.  This class handles everything about
    /// the UIWebView except for the actual data to be displayed.  The derived classes should be customized
    /// for specific formats for the data, such as HTML or RTF.
    /// </summary>
    public abstract class BodyWebView : UIWebView, IBodyRender
    {
        public delegate void LinkSelectedCallback (NSUrl url);

        // We re-use instances. if you add an ivar, add the reset code for it to Reset() in-order!
        protected NSUrl baseUrl;
        protected nfloat preferredWidth;
        private Action sizeChangedCallback;
        private bool loadingComplete;
        private LinkSelectedCallback onLinkSelected;

        public void InitializeFrame (nfloat Y, nfloat preferredWidth, nfloat initialHeight)
        {
            this.Frame = new CGRect (0, Y, preferredWidth, initialHeight);
        }

        public void InitializeRemaining (nfloat preferredWidth, Action sizeChangedCallback, NSUrl baseUrl, LinkSelectedCallback onLinkSelected)
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

        public virtual void Reset ()
        {
            StopLoading ();
            LoadFinished -= OnLoadFinished;
            ShouldStartLoad -= OnShouldStartLoad;
            if (!loadingComplete) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            }
            LoadHtmlString ("<html><body><p>&nbsp;</p></body></html>", null);
            baseUrl = null;
            preferredWidth = (nfloat)0.0;
            sizeChangedCallback = null;
            loadingComplete = false;
            onLinkSelected = null;
        }

        public BodyWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, NSUrl baseUrl, LinkSelectedCallback onLinkSelected)
            : base (new CGRect(0, Y, preferredWidth, initialHeight))
        {
            InitializeRemaining (preferredWidth, sizeChangedCallback, baseUrl, onLinkSelected);
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
            Reset ();
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
            NcTimeStamp.Add ("OnLoadFinished");
            NcTimeStamp.Dump ();
            return;
            //PostLoadAdjustment ();
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
            style.setAttribute(""type"", ""text/css"");
            style.innerHTML = ""@import '__nacho__css__'"";
            document.head.appendChild(style);
            var viewPortTag=document.createElement('meta');
            viewPortTag.id=""viewport"";
            viewPortTag.name = ""viewport"";
            viewPortTag.content = ""width={0}"";
            document.getElementsByTagName('head')[0].appendChild(viewPortTag);
        ";
        private const string disableJavaScript = "<meta http-equiv=\"Content-Security-Policy\" content=\"script-src 'none'\">";

        public BodyHtmlWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string html, NSUrl baseUrl, BodyWebView.LinkSelectedCallback onLinkSelected)
            : base (Y, preferredWidth, initialHeight, sizeChangedCallback, baseUrl, onLinkSelected)
        {
            SetHtml (html);
        }

        public BodyHtmlWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, NSUrl baseUrl, BodyWebView.LinkSelectedCallback onLinkSelected)
            : base (Y, preferredWidth, initialHeight, sizeChangedCallback, baseUrl, onLinkSelected)
        {
        }

        public static ConcurrentStack<BodyHtmlWebView> ViewCache = new ConcurrentStack<BodyHtmlWebView> ();

        public static BodyHtmlWebView Create (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string html, NSUrl baseUrl, BodyWebView.LinkSelectedCallback onLinkSelected)
        {
            BodyHtmlWebView retval = null;
            if (ViewCache.TryPop (out retval)) {
                retval.InitializeFrame (Y, preferredWidth, initialHeight);
                retval.InitializeRemaining (preferredWidth, sizeChangedCallback, baseUrl, onLinkSelected);
                retval.SetHtml (html);
                return retval;
            }
            return new BodyHtmlWebView (Y, preferredWidth, initialHeight, sizeChangedCallback, html, baseUrl, onLinkSelected);
        }

        public static void Release (BodyHtmlWebView webView)
        {
            if (3 > ViewCache.Count) {
                webView.Reset ();
                ViewCache.Push (webView);
            } else {
                webView.Dispose ();
            }
        }

        public void SetHtml (string html)
        {
            this.html = html;

            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                LoadContent ();
            }
        }

        protected override void LoadContent ()
        {
            var tmp = NachoCore.Model.NcModel.Instance.TmpPath (1) + ".html";
            File.WriteAllText (tmp, disableJavaScript + html);
            LoadRequest (new NSUrlRequest (new NSUrl (tmp, false)));
            //LoadHtmlString (disableJavaScript + html, baseUrl);
            PostLoadAdjustment ();
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

        public BodyRtfWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string rtf, NSUrl baseUrl, BodyWebView.LinkSelectedCallback onLinkSelected)
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

    /// <summary>
    /// Display plain text within a UIWebView, using our own custom font instead of the default fixed-space font.
    /// </summary>
    public class BodyPlainWebView : BodyHtmlWebView
    {
        public BodyPlainWebView (nfloat Y, nfloat preferredWidth, nfloat initialHeight, Action sizeChangedCallback, string text, NSUrl baseUrl, BodyWebView.LinkSelectedCallback onLinkSelected)
            : base (Y, preferredWidth, initialHeight, sizeChangedCallback, baseUrl, onLinkSelected)
        {
            var serializer = new HtmlTextDeserializer ();
            var doc = serializer.Deserialize (text);
            string html = "";
            using (var writer = new StringWriter ()) {
                doc.Save (writer);
                html = writer.ToString ();
            }

            SetHtml (html);
        }

    }
}
