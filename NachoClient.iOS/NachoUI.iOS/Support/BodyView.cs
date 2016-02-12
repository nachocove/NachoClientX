//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using Foundation;
using UIKit;
using MimeKit;
using CoreGraphics;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoPlatform;

namespace NachoClient.iOS
{
    public interface IBodyViewOwner
    {
        // Called when the size of the BodyView changes, allowing the parent view to adjust accordingly.
        void SizeChanged ();

        // Called when the user taps a link somewhere within the BodyView.  The parent view is expected
        // to handle the link.
        void LinkSelected (NSUrl url);

        // Called when the parent view should be dismissed.  The parent view may ignore this request.
        void DismissView ();
    }

    public class BodyView : UIView
    {
        // Which kind of BodyView is this?
        private bool variableHeight;

        // The item whose body is being shown
        private McAbstrItem item = null;

        // When refreshing, we want to know if the date changed
        private DateTime itemDateTime = DateTime.MinValue;

        // Information about size and position
        private nfloat preferredWidth;
        private nfloat yOffset = 0;
        private CGPoint contentOffset = CGPoint.Empty;
        private CGSize visibleArea;

        // UI elements
        private List<IBodyRender> childViews = new List<IBodyRender> ();
        private UILabel errorMessage;
        private UIActivityIndicatorView spinner;
        private UITapGestureRecognizer retryDownloadGestureRecognizer = null;
        private UITapGestureRecognizer.Token retryDownloadGestureRecognizerToken;

        // Other stuff
        private IBodyViewOwner owner;
        private string downloadToken = null;
        private bool statusIndicatorIsRegistered = false;
        private bool waitingForAppInForeground = false;
        private bool disposed = false;

        NcEmailMessageBundle Bundle;

        /// <summary>
        /// Create a BodyView that will be displayed in a fixed sized view. The BodyView
        /// will be cropped if it is too big for the parent view. The parent will not
        /// adjust its layout to fit the BodyView.
        /// </summary>
        /// <returns>A new BodyView object that still needs to be configured.</returns>
        /// <param name="frame">The location and size of the BodyView.</param>
        public static BodyView FixedSizeBodyView (CGRect frame, IBodyViewOwner owner)
        {
            BodyView newBodyView = new BodyView (frame);
            newBodyView.variableHeight = false;
            newBodyView.visibleArea = frame.Size;
            newBodyView.owner = owner;
            newBodyView.UserInteractionEnabled = false;
            return newBodyView;
        }

        /// <summary>
        /// Create a BodyView that will be displayed in a view that will adjust its layout
        /// based on the size of the BodyView.
        /// </summary>
        /// <returns>A new BodyView object that still needs to be configured.</returns>
        /// <param name="location">The location of the BodyView within its parent view.</param>
        /// <param name="preferredWidth">The preferred width of the BodyView.</param>
        /// <param name="visibleArea">The maximum amount of space that is visible at one time in the parent view.</param>
        /// <param name="sizeChangedCallback">A function to call when the size of the BodyView changes.</param>
        public static BodyView VariableHeightBodyView (CGPoint location, nfloat preferredWidth, CGSize visibleArea, IBodyViewOwner owner)
        {
            BodyView newBodyView = new BodyView (new CGRect (location.X, location.Y, preferredWidth, 1));
            newBodyView.variableHeight = true;
            newBodyView.visibleArea = visibleArea;
            newBodyView.owner = owner;
            return newBodyView;
        }

        private BodyView (CGRect frame)
            : base (frame)
        {
            preferredWidth = frame.Width;

            BackgroundColor = UIColor.White;

            spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.HidesWhenStopped = true;
            AddSubview (spinner);

            errorMessage = new UILabel (new CGRect (20, 10, frame.Width - 40, 1));
            errorMessage.Font = A.Font_AvenirNextDemiBold14;
            errorMessage.LineBreakMode = UILineBreakMode.WordWrap;
            errorMessage.TextColor = A.Color_808080;
            errorMessage.Hidden = true;
            AddSubview (errorMessage);
        }

        /// <summary>
        /// Display the body of the given item. The current contents of the BodyView are discarded.
        /// </summary>
        /// <param name="item">The item whose body should be displayed.</param>
        public void Configure (McAbstrItem itemParam, bool isRefresh)
        {
            this.item = itemParam;

            // McException without a body inherits from its McCalendar
            if ((item is McException) && (0 == item.BodyId)) {
                var exception = (McException)item;
                item = McCalendar.QueryById<McCalendar> ((int)exception.CalendarId);
            }

            var body = McBody.QueryById<McBody> (item.BodyId);

            if (item is McException) {
                // McException bodies are always inline; they better be complete
                NcAssert.True (McAbstrFileDesc.IsNontruncatedBodyComplete (body));
            }

            if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                if (isRefresh && (itemDateTime == body.LastModified)) {
                    return;
                }
            }
                
            RemoveChildViews ();

            downloadToken = null;
            if (statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                statusIndicatorIsRegistered = false;
            }

            if (!McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                StartDownloadWhenInForeground ();
                return;
            }

            spinner.StopAnimating ();

            itemDateTime = body.LastModified;

            if (item is McEmailMessage && null != ((McEmailMessage)item).MeetingRequest) {
                RenderCalendarPart ();
            }

            Bundle = new NcEmailMessageBundle (body);
            if (Bundle.NeedsUpdate) {
                NcTask.Run (delegate {
                    Bundle.Update ();
                    // While Bundle.Update() was running, the user may have closed the view,
                    // causing this BodyView object to be disposed.
                    if (!disposed) {
                        InvokeOnUIThread.Instance.Invoke (RenderBundle);
                    }
                }, "BodyView_UpdateBundle");
            } else {
                RenderBundle ();
            }

            LayoutQuietly ();
        }

        private McAbstrItem RefreshItem ()
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

        private void Reconfigure ()
        {
            // The status callback indicator callback isn't needed any more.
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            statusIndicatorIsRegistered = false;

            var refreshedItem = RefreshItem ();
            if (null != refreshedItem) {

                Configure (refreshedItem, false);
                // Configure() normally doesn't call the parent view's callback. But because
                // the download completed in the background, that callback needs to be called.
                owner.SizeChanged ();
            }
        }

        private void StartDownloadWhenInForeground ()
        {
            // Register for the status indicator event, so we will know when the body has
            // been downloaded, on when the app comes back into the foreground.
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            statusIndicatorIsRegistered = true;

            if (NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {
                StartDownload ();
            } else {
                // The app is in the background, which means the back end might be parked,
                // or it might get parked before the download completes.  Wait until the
                // app comes back into the foreground before starting the download.  The
                // StatusIndicatorCallback will take care of doing that.
                waitingForAppInForeground = true;
            }
        }

        private void StartDownload ()
        {
            // Download the body.
            NcResult nr;
            if (item is McEmailMessage) {
                nr = BackEnd.Instance.DnldEmailBodyCmd (item.AccountId, item.Id, true);
            } else if (item is McAbstrCalendarRoot) {
                nr = BackEnd.Instance.DnldCalBodyCmd (item.AccountId, item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (string.Format (
                    "Unhandled abstract item type {0}", item.GetType ().Name));
            }
            if (nr.isError ()) {
                Log.Warn (Log.LOG_UI, "DnldEmailBodyCmd({0}:{1}) failed with error: {2}", item.Id, item.AccountId, nr);
                downloadToken = null;
            } else {
                downloadToken = nr.GetValue<string> ();
            }
            if (null == downloadToken) {
                // There is a race condition where the download of the body could complete
                // in between checking the FilePresence value and calling DnldEmailBodyCmd.
                // Refresh the item and the body to see if that is the case.
                var refreshedItem = RefreshItem ();
                if (null != refreshedItem) {
                    item = refreshedItem;
                    var body = McBody.QueryById<McBody> (item.BodyId);
                    if (McAbstrFileDesc.IsNontruncatedBodyComplete (body)) {
                        // It was a race condition. We're good.
                        Reconfigure ();
                    } else {
                        Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}",
                            item.Id, item.AccountId);
                        ShowErrorMessage (nr);
                    }
                } else {
                    // The item seems to have been deleted from the database.  The best we
                    // can do is to show an error message, even though tapping to retry the
                    // download won't do any good.
                    Log.Warn (Log.LOG_UI,
                        "Failed to start body download for message {0} in account {1}, and it looks like the message has been deleted.",
                        item.Id, item.AccountId);
                    ShowErrorMessage (nr);
                }
            } else {
                // The download has started.
                if (variableHeight) {
                    // A variable height view should always be visible. A fixed size
                    // view might be off the screen. The Hot view will call
                    // PrioritizeBodyDownload() when the card becomes visible.
                    McPending.Prioritize (item.AccountId, downloadToken);
                }
                ActivateSpinner ();
            }
        }

        private void ActivateSpinner ()
        {
            RemoveChildViews ();

            if (variableHeight) {
                // Try to put the spinner in the center of the screen.
                spinner.Center = new CGPoint (visibleArea.Width / 2 - Frame.X, visibleArea.Height / 2 - Frame.Y);
            } else {
                // Put the spinner in the center of the BodyView.
                spinner.Center = new CGPoint (Frame.Width / 2, Frame.Height / 2);
            }
            spinner.StartAnimating ();
        }

        private static nfloat Min4 (nfloat a, nfloat b, nfloat c, nfloat d)
        {
            return NMath.Min (NMath.Min (a, b), NMath.Min (c, d));
        }

        private static bool AreClose (nfloat a, nfloat b)
        {
            nfloat ratio = a / b;
            return 0.99f < ratio && ratio < 1.01f;
        }

        private bool LayoutAndDetectSizeChange ()
        {
            CGSize oldSize = Frame.Size;
            nfloat zoomScale = ViewHelper.ZoomScale (this);

            nfloat screenTop = contentOffset.Y / zoomScale;
            nfloat screenBottom = (contentOffset.Y + visibleArea.Height) / zoomScale;

            nfloat subviewX = NMath.Max (0f, contentOffset.X / zoomScale);
            nfloat subviewY = 0;
            if (!errorMessage.Hidden) {
                subviewY = errorMessage.Frame.Bottom;
            }

            nfloat maxWidth = preferredWidth;

            foreach (var subview in childViews) {
                // If any part of the view should be visible, then adjust it.  If the view should
                // be off the screen, then mark it hidden.  If the width of the view changes,
                // then it is possible that the height will also change as a result.  In which
                // case we have to redo all the calculations.
                CGSize size;
                int loopCount = 0;
                do {
                    size = subview.ContentSize;
                    var currentFrame = subview.uiView ().Frame;
                    nfloat viewTop = subviewY;
                    nfloat viewBottom = viewTop + size.Height;
                    if (viewTop < screenBottom && screenTop < viewBottom) {
                        // Part of the view should be visible on the screen.
                        nfloat newX = NMath.Max (0f, NMath.Min (subviewX, size.Width - preferredWidth));
                        nfloat newWidth = NMath.Max (preferredWidth, NMath.Min (size.Width - subviewX, visibleArea.Width / zoomScale));
                        nfloat newXOffset = newX;

                        nfloat newY = NMath.Max (viewTop, screenTop);
                        nfloat newHeight = Min4 (viewBottom - screenTop, screenBottom - viewTop, size.Height, visibleArea.Height / zoomScale);
                        nfloat newYOffset = newY - viewTop;

                        subview.ScrollingAdjustment (new CGRect (newX, newY, newWidth, newHeight), new CGPoint (newXOffset, newYOffset));
                        subview.uiView ().Hidden = false;
                    } else {
                        // None of the view is currently on the screen.
                        subview.uiView ().Hidden = true;
                    }
                } while (size != subview.ContentSize && ++loopCount < 4);

                subviewY += size.Height;
                maxWidth = NMath.Max (maxWidth, size.Width);
            }

            bool sizeChanged = !AreClose (oldSize.Width, maxWidth * zoomScale) || !AreClose (oldSize.Height, subviewY * zoomScale);
            if (sizeChanged) {
                ViewFramer.Create (this).Width (maxWidth * zoomScale).Height (subviewY * zoomScale);
            }

            return sizeChanged;
        }

        public void LayoutAndNotifyParent ()
        {
            if (LayoutAndDetectSizeChange ()) {
                owner.SizeChanged ();
            }
        }

        public void LayoutQuietly ()
        {
            LayoutAndDetectSizeChange ();
        }

        public void SetVisibleArea (CGSize size)
        {
            visibleArea = size;
            preferredWidth = size.Width;
            if (childViews.Count > 0) {
                LayoutQuietly ();
            }
        }

        /// <summary>
        /// Adjust the subviews so that one screen worth of content is visible
        /// at the given offset.
        /// </summary>
        /// <param name="newContentOffset">The top left corner of the area that
        /// should be visible, in the BodyView's coordinates.</param>
        public void ScrollingAdjustment (CGPoint newContentOffset)
        {
            this.contentOffset = newContentOffset;
            LayoutAndNotifyParent ();
        }

        /// <summary>
        /// The view is front and center right now. If a download is in progress, tell the
        /// back end to speed it up.
        /// </summary>
        public void PrioritizeBodyDownload ()
        {
            if (null != downloadToken) {
                McPending.Prioritize (item.AccountId, downloadToken);
            }
        }

        protected override void Dispose (bool disposing)
        {
            disposed = true;
            if (statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                statusIndicatorIsRegistered = false;
            }
            if (null != retryDownloadGestureRecognizer) {
                retryDownloadGestureRecognizer.RemoveTarget (retryDownloadGestureRecognizerToken);
                errorMessage.RemoveGestureRecognizer (retryDownloadGestureRecognizer);
            }
            base.Dispose (disposing);
        }

        private void ShowErrorMessage (NcResult nr)
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            statusIndicatorIsRegistered = false;
            spinner.StopAnimating ();
            RemoveChildViews ();
            string preview = item.GetBodyPreviewOrEmpty ();
            bool hasPreview = !string.IsNullOrEmpty (preview);
            string message;
            if (!ErrorHelper.ExtractErrorString (nr, out message)) {
                message = "Download failed.";
            }
            bool isUnrecoverableError = nr.Why == NcResult.WhyEnum.MissingOnServer;
            if (UserInteractionEnabled && !isUnrecoverableError) {
                message += " Tap here to retry.";
            }
            if (hasPreview) {
                message += " Showing message preview only.";
            }
            RenderDownloadFailure (message, isUnrecoverableError);
            if (hasPreview) {
                RenderTextString (preview);
            }
            LayoutAndNotifyParent ();
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;

            if (waitingForAppInForeground &&
                NcResult.SubKindEnum.Info_ExecutionContextChanged == statusEvent.Status.SubKind &&
                NcApplication.ExecutionContextEnum.Foreground == NcApplication.Instance.ExecutionContext) {

                // We were waiting to start a download until the app was in the foreground.
                // The app is now in the foreground.
                waitingForAppInForeground = false;
                StartDownload ();
                return;
            }

            if (null == downloadToken) {
                // This shouldn't happen normally.  But it can happen if a
                // status event was queued up while this here function was
                // running.  That event won't be delivered until StatusIndicatorCallback
                // has been unregistered and downloadToken has been set to null.
                return;
            }

            if (null != statusEvent.Tokens && statusEvent.Tokens.FirstOrDefault () == downloadToken) {
                switch (statusEvent.Status.SubKind) {

                case NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded:
                case NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded:
                    Reconfigure ();
                    break;

                case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
                case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:

                    // The McPending isn't needed any more.
                    var localAccountId = item.AccountId;
                    var localDownloadToken = downloadToken;
                    NcTask.Run (delegate {
                        foreach (var request in McPending.QueryByToken (localAccountId, localDownloadToken)) {
                            if (McPending.StateEnum.Failed == request.State) {
                                request.Delete ();
                            }
                        }
                    }, "DelFailedMcPendingBodyDnld");

                    if (NcApplication.ExecutionContextEnum.Foreground != NcApplication.Instance.ExecutionContext &&
                        NcResult.WhyEnum.UnavoidableDelay == statusEvent.Status.Why) {

                        // The download probably failed because the back end was parked
                        // because the app is in the background.  Don't record this as
                        // a failure.  Instead, wait for the app to be in the foreground
                        // and then try the download again.
                        waitingForAppInForeground = true;
                        downloadToken = null;

                    } else {
                        // The download really did fail.  Let the user know.
                        ShowErrorMessage (NcResult.Error (statusEvent.Status.SubKind, statusEvent.Status.Why));
                    }
                    break;
                }
            }
        }

        private void onLinkSelected (NSUrl url)
        {
            owner.LinkSelected (url);
        }

        private void onDismissView ()
        {
            owner.DismissView ();
        }

        void  RenderBundle ()
        {
            if (disposed) {
                return;
            }
            var webView = BodyWebView.ResuableWebView (yOffset, preferredWidth, visibleArea.Height);
            webView.OnLinkSelected = onLinkSelected;
            webView.LoadBundle (Bundle, LayoutAndNotifyParent);
            AddSubview (webView);
            childViews.Add (webView);
            yOffset += webView.ContentSize.Height;
        }

        void RenderTextString (string text)
        {
            var webView = BodyWebView.ResuableWebView (yOffset, preferredWidth, visibleArea.Height);
            var serializer = new NachoCore.Utils.HtmlTextDeserializer ();
            var doc = serializer.Deserialize (text);
            var head = doc.DocumentNode.Element ("html").Element ("head");
            var style = doc.CreateElement ("link");
            style.SetAttributeValue ("rel", "stylesheet");
            style.SetAttributeValue ("type", "text/css");
            style.SetAttributeValue ("href", "nacho.css");
            head.AppendChild (style);
            var baseUrl = new NSUrl (String.Format ("file://{0}/", Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments)));
            var html = "";
            using (var writer = new StringWriter ()) {
                doc.Save (writer);
                html = writer.ToString ();
            }
            webView.LoadHtmlString (html, baseUrl);
            AddSubview (webView);
            childViews.Add (webView);
            yOffset += webView.ContentSize.Height;
        }

        private void RenderCalendarPart ()
        {
            var calView = new BodyCalendarView (yOffset, preferredWidth, (McEmailMessage)item, !UserInteractionEnabled, onDismissView, onLinkSelected);
            AddSubview (calView);
            childViews.Add (calView);
            yOffset += calView.Frame.Height;
        }

        private void RenderDownloadFailure (string message, bool isUnrecoverableError)
        {
            var attributedString = new NSAttributedString (message);
            errorMessage.Lines = 0;
            errorMessage.AttributedText = attributedString;
            errorMessage.SizeToFit ();
            errorMessage.Hidden = false;

            yOffset = errorMessage.Frame.Bottom;

            if (UserInteractionEnabled) {
                if (retryDownloadGestureRecognizer == null) {
                    errorMessage.UserInteractionEnabled = true;
                    retryDownloadGestureRecognizer = new UITapGestureRecognizer ();
                    retryDownloadGestureRecognizer.NumberOfTapsRequired = 1;
                    retryDownloadGestureRecognizerToken = retryDownloadGestureRecognizer.AddTarget (StartDownloadWhenInForeground);
                    retryDownloadGestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                        return false;
                    };
                    errorMessage.AddGestureRecognizer (retryDownloadGestureRecognizer);
                }
                retryDownloadGestureRecognizer.Enabled = !isUnrecoverableError;
            }
        }

        private void RemoveChildViews()
        {
            // Remove everything else from the view. There can be stuff visible if the
            // download was started by the user tapping on the "Download failed" message.
            foreach (var subview in childViews) {
                var view = subview.uiView ();
                view.RemoveFromSuperview ();
                ViewHelper.DisposeViewHierarchy (view);
            }
            childViews.Clear ();
            errorMessage.Hidden = true;
        }
    }

    /// <summary>
    /// Wrap a BodyView within a UIScrollView that uses two-fingered scrolling.  The BodyView
    /// will be the only thing within the scroll view.  This is designed to be used within
    /// the Nacho Hot view.
    /// </summary>
    public class ScrollableBodyView : UIScrollView, IBodyViewOwner
    {
        private BodyView bodyView;
        private IBodyViewOwner owner;
        private int displayedBodyId = 0;

        /// <summary>
        /// Create a scrollable BodyView with the given frame.
        /// </summary>
        public ScrollableBodyView (CGRect frame, IBodyViewOwner owner)
            : base (frame)
        {
            this.owner = owner;

            // UIScrollView comes with a gesture recognizer for scrolling.
            // Change it to use two fingers instead of one.
            PanGestureRecognizer.MinimumNumberOfTouches = 2;
            PanGestureRecognizer.MaximumNumberOfTouches = 2;

            ScrollsToTop = false;

            Scrolled += ScrollViewScrolled;
            bodyView = BodyView.FixedSizeBodyView (new CGRect (0, 0, frame.Width, frame.Height), this);
            AddSubview (bodyView);
        }

        public void SetItem (McAbstrItem item)
        {
            if (0 == displayedBodyId || item.BodyId != displayedBodyId) {
                // Displaying a different message. Scroll back to the top.
                ContentOffset = new CGPoint (0, 0);
                displayedBodyId = item.BodyId;
                bodyView.Configure (item, false);
                ContentSize = bodyView.Frame.Size;
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            bodyView.SetVisibleArea (Bounds.Size);
        }

        protected override void Dispose (bool disposing)
        {
            Scrolled -= ScrollViewScrolled;
            base.Dispose (disposing);
        }

        private void ScrollViewScrolled (object sender, EventArgs e)
        {
            bodyView.ScrollingAdjustment (ContentOffset);
        }

        private void BodyViewSizeChanged ()
        {
            ContentSize = bodyView.Frame.Size;
        }

        void IBodyViewOwner.SizeChanged ()
        {
            ContentSize = bodyView.Frame.Size;
            owner.SizeChanged ();
        }

        void IBodyViewOwner.LinkSelected (NSUrl url)
        {
            owner.LinkSelected (url);
        }

        void IBodyViewOwner.DismissView ()
        {
            owner.DismissView ();
        }

        // I'm not sure exactly how this works, but it seems to do what we want.
        // The intention is to pass all touch events, other then the scrolling
        // that is recognized by the pan gesture recognizer, on up the chain.
        // In the case of the Nacho Hot view, this causes a single tap on the
        // message body to open the message detail view.

        public override void TouchesBegan (NSSet touches, UIEvent evt)
        {
            NextResponder.TouchesBegan (touches, evt);
        }

        public override void TouchesMoved (NSSet touches, UIEvent evt)
        {
            NextResponder.TouchesMoved (touches, evt);
        }

        public override void TouchesEnded (NSSet touches, UIEvent evt)
        {
            NextResponder.TouchesEnded (touches, evt);
        }

        public override void TouchesCancelled (NSSet touches, UIEvent evt)
        {
            NextResponder.TouchesCancelled (touches, evt);
        }
    }
}
