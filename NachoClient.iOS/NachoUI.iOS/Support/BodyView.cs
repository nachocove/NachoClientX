//#define DEBUG_UI

//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MimeKit;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore.ActiveSync;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class BodyView : UIView
    {
        // Which kind of BodyView is this?
        private bool variableHeight;

        // The item whose body is being shown
        private McAbstrItem item = null;

        // When refreshing, we want to know if the date changed
        private DateTime itemDateTime = DateTime.MinValue;

        // Information about size and position
        private float preferredWidth;
        private float yOffset = 0;
        private PointF contentOffset = PointF.Empty;
        private SizeF visibleArea;

        // UI elements
        private List<IBodyRender> childViews = new List<IBodyRender> ();
        private UILabel errorMessage;
        private UIActivityIndicatorView spinner;
        private UITapGestureRecognizer retryDownloadGestureRecognizer = null;
        private UITapGestureRecognizer.Token retryDownloadGestureRecognizerToken;

        // Other stuff
        private Action sizeChangedCallback = null;
        private string downloadToken = null;
        private bool statusIndicatorIsRegistered = false;

        /// <summary>
        /// Create a BodyView that will be displayed in a fixed sized view. The BodyView
        /// will be cropped if it is too big for the parent view. The parent will not
        /// adjust its layout to fit the BodyView.
        /// </summary>
        /// <returns>A new BodyView object that still needs to be configured.</returns>
        /// <param name="frame">The location and size of the BodyView.</param>
        public static BodyView FixedSizeBodyView (RectangleF frame)
        {
            BodyView newBodyView = new BodyView (frame);
            newBodyView.variableHeight = false;
            newBodyView.visibleArea = frame.Size;
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
        public static BodyView VariableHeightBodyView (PointF location, float preferredWidth, SizeF visibleArea, Action sizeChangedCallback)
        {
            BodyView newBodyView = new BodyView (new RectangleF(location.X, location.Y, preferredWidth, 1));
            newBodyView.variableHeight = true;
            newBodyView.visibleArea = visibleArea;
            newBodyView.sizeChangedCallback = sizeChangedCallback;
            return newBodyView;
        }

        private BodyView (RectangleF frame)
            : base (frame)
        {
            preferredWidth = frame.Width;

            BackgroundColor = UIColor.White;

            spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.HidesWhenStopped = true;
            AddSubview (spinner);

            errorMessage = new UILabel (new RectangleF (0, 10, frame.Width, 1));
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
        public void Configure (McAbstrItem item, bool isRefresh, bool isOnNow)
        {
            this.item = item;

            var body = McBody.QueryById<McBody> (item.BodyId);

            if (McAbstrFileDesc.IsNontruncatedBodyComplete(body)) {
                if (isRefresh && (itemDateTime == body.LastModified)) {
                    return;
                }
            }
                
            // Clear out the existing BodyView
            foreach (var subview in childViews) {
                var view = subview.uiView ();
                view.RemoveFromSuperview ();
                ViewHelper.DisposeViewHierarchy (view);
            }
            childViews.Clear ();
            errorMessage.Hidden = true;
            downloadToken = null;
            if (statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                statusIndicatorIsRegistered = false;
            }

            if (!McAbstrFileDesc.IsNontruncatedBodyComplete(body)) {
                StartDownload ();
                return;
            }

            spinner.StopAnimating ();

            itemDateTime = body.LastModified;

            switch (body.BodyType) {
            case McAbstrFileDesc.BodyTypeEnum.PlainText_1:
                RenderTextString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.HTML_2:
                RenderHtmlString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.RTF_3:
                RenderRtfString (body.GetContentsString ());
                break;
            case McAbstrFileDesc.BodyTypeEnum.MIME_4:
                RenderMime (body, isOnNow);
                break;
            default:
                Log.Error (Log.LOG_UI, "Body {0} has an unknown body type {1}.", body.Id, (int)body.BodyType);
                RenderTextString (body.GetContentsString ());
                break;
            }

            LayoutQuietly ();
        }

        private McAbstrItem RefreshItem ()
        {
            McAbstrItem refreshedItem;
            if (item is McEmailMessage) {
                refreshedItem = McEmailMessage.QueryById<McEmailMessage> (item.Id);
            } else if (item is McAbstrCalendarRoot) {
                refreshedItem = McCalendar.QueryById<McCalendar> (item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (string.Format ("Unhandled abstract item type {0}", item.GetType ().Name));
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

                Configure (refreshedItem, false, false);

                // Configure() normally doesn't call the parent view's callback. But because
                // the download completed in the background, that callback needs to be called.
                if (null != sizeChangedCallback) {
                    sizeChangedCallback ();
                }
            }
        }

        private void StartDownload ()
        {
            // Register for the status indicator event, so we will know when the body has
            // been downloaded.
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            statusIndicatorIsRegistered = true;

            // Download the body.
            if (item is McEmailMessage) {
                downloadToken = BackEnd.Instance.DnldEmailBodyCmd (item.AccountId, item.Id, true);
            } else if (item is McAbstrCalendarRoot) {
                downloadToken = BackEnd.Instance.DnldCalBodyCmd (item.AccountId, item.Id);
            } else {
                throw new NcAssert.NachoDefaultCaseFailure (string.Format (
                    "Unhandled abstract item type {0}", item.GetType ().Name));
            }

            if (null == downloadToken) {
                // There is a race condition where the download of the body could complete
                // in between checking the FilePresence value and calling DnldEmailBodyCmd.
                // Refresh the item and the body to see if that is the case.
                var refreshedItem = RefreshItem ();
                if (null != refreshedItem) {
                    item = refreshedItem;
                    var body = McBody.QueryById<McBody> (item.BodyId);
                    if (McAbstrFileDesc.IsNontruncatedBodyComplete(body)) {
                        // It was a race condition. We're good.
                        Reconfigure ();
                    } else {
                        Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}", item.Id, item.AccountId);
                        ShowErrorMessage ();
                    }
                } else {
                    // The item seems to have been deleted from the database.  The best we
                    // can do is to show an error message, even though tapping to retry the
                    // download won't do any good.
                    Log.Warn (Log.LOG_UI, "Failed to start body download for message {0} in account {1}, and it looks like the message has been deleted.", item.Id, item.AccountId);
                    ShowErrorMessage ();
                }
            } else {
                // The download has started.
                if (variableHeight) {
                    // A variable height view should always be visible. A fixed size
                    // view might be off the screen. The Now view will call
                    // PrioritizeBodyDownload() when the card becomes visible.
                    BackEnd.Instance.Prioritize (item.AccountId, downloadToken);
                }
                ActivateSpinner ();
            }
        }

        private void ActivateSpinner ()
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

            if (variableHeight) {
                // Try to put the spinner in the center of the screen.
                spinner.Center = new PointF (visibleArea.Width / 2 - Frame.X, visibleArea.Height / 2 - Frame.Y);
            } else {
                // Put the spinner in the center of the BodyView.
                spinner.Center = new PointF (Frame.Width / 2, Frame.Height / 2);
            }
            spinner.StartAnimating ();
        }

        private static float Min4 (float a, float b, float c, float d)
        {
            return Math.Min (Math.Min (a, b), Math.Min (c, d));
        }

        private static bool AreClose (float a, float b)
        {
            float ratio = a / b;
            return 0.99f < ratio && ratio < 1.01f;
        }

        private bool LayoutAndDetectSizeChange ()
        {
            SizeF oldSize = Frame.Size;
            float zoomScale = ViewHelper.ZoomScale (this);

            float screenTop = contentOffset.Y / zoomScale;
            float screenBottom = (contentOffset.Y + visibleArea.Height) / zoomScale;

            float subviewX = Math.Max (0f, contentOffset.X / zoomScale);
            float subviewY = 0;
            if (!errorMessage.Hidden) {
                subviewY = errorMessage.Frame.Bottom;
            }

            float maxWidth = preferredWidth;

            foreach (var subview in childViews) {
                // If any part of the view is currently visible or should be visible,
                // then adjust it.  If the view is completely off the screen, leave
                // it alone.  If we adjust the width of the view, it is possible that
                // the height will change as a result.  In which case we have to redo
                // all the calculations.
                SizeF size;
                int loopCount = 0;
                do {
                    size = subview.ContentSize;
                    var currentFrame = subview.uiView ().Frame;
                    float viewTop = subviewY;
                    float viewBottom = viewTop + size.Height;
                    if (viewTop < screenBottom && screenTop < viewBottom) {
                        // Part of the view should be visible on the screen.
                        float newX = Math.Max (0f, Math.Min (subviewX, size.Width - preferredWidth));
                        float newWidth = Math.Max (preferredWidth, Math.Min (size.Width - subviewX, visibleArea.Width / zoomScale));
                        float newXOffset = newX;

                        float newY = Math.Max (viewTop, screenTop);
                        float newHeight = Min4 (viewBottom - screenTop, screenBottom - viewTop, size.Height, visibleArea.Height / zoomScale);
                        float newYOffset = newY - viewTop;

                        subview.ScrollingAdjustment (new RectangleF (newX, newY, newWidth, newHeight), new PointF (newXOffset, newYOffset));
                        subview.uiView ().Hidden = false;
                    } else {
                        // None of the view is currently on the screen.
                        subview.uiView ().Hidden = true;
                    }
                } while (size != subview.ContentSize && ++loopCount < 4);

                subviewY += size.Height;
                maxWidth = Math.Max (maxWidth, size.Width);
            }

            bool sizeChanged = !AreClose (oldSize.Width, maxWidth * zoomScale) || !AreClose (oldSize.Height, subviewY * zoomScale);
            if (sizeChanged && variableHeight) {
                ViewFramer.Create (this).Width (maxWidth * zoomScale).Height (subviewY * zoomScale);
            }

            return sizeChanged;
        }

        public void LayoutAndNotifyParent ()
        {
            if (LayoutAndDetectSizeChange ()) {
                if (null != sizeChangedCallback) {
                    sizeChangedCallback ();
                }
            }
        }

        public void LayoutQuietly ()
        {
            LayoutAndDetectSizeChange ();
        }

        /// <summary>
        /// Adjust the subviews so that one screen worth of content is visible
        /// at the given offset.
        /// </summary>
        /// <param name="newContentOffset">The top left corner of the area that
        /// should be visible, in the BodyView's coordinates.</param>
        public void ScrollingAdjustment (PointF newContentOffset)
        {
            this.contentOffset = newContentOffset;
            LayoutAndNotifyParent ();
        }

        /// <summary>
        /// Resize and relocate the BodyView. This can only be called for a
        /// fixed size BodyView.
        /// </summary>
        /// <param name="newFrame">The new location and size for the BodyView.</param>
        public void Resize (RectangleF newFrame)
        {
            // Resizing only makes sense for a fixed size BodyView
            NcAssert.True (!variableHeight);
            preferredWidth = newFrame.Width;
            visibleArea = newFrame.Size;
            LayoutQuietly ();
        }

        /// <summary>
        /// The view is front and center right now. If a download is in progress, tell the
        /// back end to speed it up.
        /// </summary>
        public void PrioritizeBodyDownload ()
        {
            if (null != downloadToken) {
                BackEnd.Instance.Prioritize (item.AccountId, downloadToken);
            }
        }

        protected override void Dispose (bool disposing)
        {
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

        private void ShowErrorMessage ()
        {
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            statusIndicatorIsRegistered = false;
            spinner.StopAnimating ();
            string preview = item.GetBodyPreviewOrEmpty ();
            bool hasPreview = !string.IsNullOrEmpty (preview);
            string message;
            if (variableHeight) {
                if (hasPreview) {
                    message = "[ Download failed. Tap here to retry. Message preview only. ]";
                } else {
                    message = "[ Download failed. Tap here to retry. ]";
                }
            } else {
                if (hasPreview) {
                    message = "[ Download failed. Message preview only. ]";
                } else {
                    message = "[ Download failed. ]";
                }
            }
            RenderDownloadFailure (message);
            if (hasPreview) {
                RenderTextString (preview);
            }
            LayoutQuietly ();
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (null == downloadToken) {
                // This shouldn't happen normally.  But it can happen if a
                // status event was queued up while this here function was
                // running.  That event won't be delivered until StatusIndicatorCallback
                // has been unregistered and downloadToken has been set to null.
                return;
            }

            var statusEvent = (StatusIndEventArgs)e;
            if (null != statusEvent.Tokens && statusEvent.Tokens.FirstOrDefault () == downloadToken) {
                switch (statusEvent.Status.SubKind) {

                case NcResult.SubKindEnum.Info_EmailMessageBodyDownloadSucceeded:
                case NcResult.SubKindEnum.Info_CalendarBodyDownloadSucceeded:
                    Reconfigure ();
                    break;

                case NcResult.SubKindEnum.Error_EmailMessageBodyDownloadFailed:
                case NcResult.SubKindEnum.Error_CalendarBodyDownloadFailed:
                    ShowErrorMessage ();
                    break;
                }
            }
        }

        private void RenderAttributedString (NSAttributedString text)
        {
            var textView = new BodyTextView (yOffset, Frame.Width, text);
            AddSubview (textView);
            childViews.Add (textView);
            yOffset += textView.ContentSize.Height;
        }

        private void RenderTextString (string text)
        {
            if (string.IsNullOrWhiteSpace (text)) {
                return;
            }
            UIFont uiFont = A.Font_AvenirNextRegular17;
            var attributes = new MonoTouch.CoreText.CTStringAttributes ();
            attributes.Font = new MonoTouch.CoreText.CTFont (uiFont.Name, uiFont.PointSize);
            RenderAttributedString (new NSAttributedString (text, attributes));
        }

        private void RenderRtfString (string rtf)
        {
            if (string.IsNullOrEmpty (rtf)) {
                return;
            }
            var nsError = new NSError ();
            var attributes = new NSAttributedStringDocumentAttributes ();
            attributes.DocumentType = NSDocumentType.RTF;
            RenderAttributedString (new NSAttributedString (rtf, attributes, ref nsError));
        }

        private void RenderHtmlString (string html)
        {
            var webView = new BodyWebView (
                yOffset, preferredWidth, visibleArea.Height, LayoutAndNotifyParent,
                html, NSUrl.FromString (string.Format ("cid://{0}", item.BodyId)));
            AddSubview (webView);
            childViews.Add (webView);
            yOffset += webView.ContentSize.Height;
        }

        private void RenderTextPart (MimePart part)
        {
            RenderTextString ((part as TextPart).Text);
        }

        private void RenderRtfPart (MimePart part)
        {
            RenderRtfString ((part as TextPart).Text);
        }

        private void RenderHtmlPart (MimePart part)
        {
            RenderHtmlString ((part as TextPart).Text);
        }

        private void RenderImagePart (MimePart part)
        {
            using (var image = PlatformHelpers.RenderImage (part)) {
                if (null == image) {
                    Log.Error (Log.LOG_UI, "Unable to render image {0}", part.ContentType);
                    RenderTextString ("[ Unable to display image ]");
                } else {
                    var imageView = new BodyImageView (yOffset, preferredWidth, image);
                    AddSubview (imageView);
                    childViews.Add (imageView);
                    yOffset += imageView.Frame.Height;
                }
            }
        }

        private void RenderCalendarPart (MimePart part, bool isOnNow)
        {
            var calView = new BodyCalendarView (yOffset, preferredWidth, part, isOnNow);
            AddSubview (calView);
            childViews.Add (calView);
            yOffset += calView.Frame.Height;
        }

        private void RenderMime (McBody body, bool isOnNow)
        {
            var message = MimeHelpers.LoadMessage (body);
            MimeHelpers.DumpMessage (message);
            var list = new List<MimeEntity> ();
            MimeHelpers.MimeDisplayList (message, ref list);

            foreach (var entity in list) {
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtmlPart (part);
                } else if (part.ContentType.Matches ("text", "calendar")) {
                    RenderCalendarPart (part, isOnNow);
                } else if (part.ContentType.Matches ("text", "rtf")) {
                    RenderRtfPart (part);
                } else if (part.ContentType.Matches ("text", "*")) {
                    RenderTextPart (part);
                } else if (part.ContentType.Matches ("image", "*")) {
                    RenderImagePart (part);
                }
            }
        }

        private void RenderDownloadFailure (string message)
        {
            var attributedString = new NSAttributedString (message);
            errorMessage.Lines = 0;
            errorMessage.AttributedText = attributedString;
            errorMessage.SizeToFit ();
            errorMessage.Hidden = false;

            yOffset = errorMessage.Frame.Bottom;

            if (variableHeight && null == retryDownloadGestureRecognizer) {
                errorMessage.UserInteractionEnabled = true;
                retryDownloadGestureRecognizer = new UITapGestureRecognizer ();
                retryDownloadGestureRecognizer.NumberOfTapsRequired = 1;
                retryDownloadGestureRecognizerToken = retryDownloadGestureRecognizer.AddTarget (StartDownload);
                retryDownloadGestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                    return false;
                };
                errorMessage.AddGestureRecognizer (retryDownloadGestureRecognizer);
            }
        }
    }
}

