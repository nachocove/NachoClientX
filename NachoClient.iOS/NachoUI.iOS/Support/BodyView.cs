﻿//#define DEBUG_UI

//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

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
    public delegate void LayoutCompleteCallback ();

    public class RecursionCounter
    {
        private int count;
        private object lockObject;
        public Action DecrementToZero;

        public int Count {
            get {
                return count;
            }
        }

        public RecursionCounter (Action decrementToZero = null)
        {
            count = 0;
            lockObject = new object ();
            DecrementToZero = decrementToZero;
        }

        public void Reset ()
        {
            lock (lockObject) {
                count = 0;
            }
        }

        public void Increment ()
        {
            lock (lockObject) {
                count += 1;
            }
        }

        public void Decrement ()
        {
            lock (lockObject) {
                NcAssert.True (1 <= count);
                count -= 1;
                if (0 == count) {
                    InvokeOnUIThread.Instance.Invoke (() => {
                        DecrementToZero ();
                    });
                }
            }
        }
    }

    /// <summary>
    /// BodyView is a special type of view 
    /// </summary>
    public class BodyView : UIScrollView
    {
        public delegate void RenderStart ();
        public delegate void RenderComplete ();
        public delegate void DownloadStart ();

        public enum TagType {
            MESSAGE_PART_TAG = 300,
            DOWNLOAD_TAG = 304,
            SPINNER_TAG = 600
        };

        protected delegate bool IterateCallback (UIView subview);

        static UIColor SCROLLVIEW_BDCOLOR = UIColor.Magenta;
        static UIColor MESSAGEVIEW_BDCOLOR = UIColor.Brown;

        static UIColor SCROLLVIEW_BGCOLOR = UIColor.White;
        static UIColor MESSAGEVIEW_BGCOLOR = UIColor.White;
        public const int MESSAGEVIEW_INSET = 1;
        public const int BODYVIEW_INSET = 1;

        protected enum LoadState {
            IDLE,    // body is already there
            LOADING, // body is being loaded
            ERROR    // body was being loaded
        }

        public bool HorizontalScrollingEnabled {
            get {
                return ShowsHorizontalScrollIndicator;
            }
            set {
                ShowsHorizontalScrollIndicator = value;
            }
        }

        public bool VerticalScrollingEnabled {
            get {
                return ShowsVerticalScrollIndicator;
            }
            set {
                ShowsVerticalScrollIndicator = value;
            }
        }

        public bool AutomaticallyScaleHtmlContent { get; set; }

        // If false, center on view frame
        public bool SpinnerCenteredOnParentFrame { get; set; }

        private float leftMargin;
        private float htmlLeftMargin;

        protected UIView parentView;
        protected UIView messageView;
        protected LoadState loadState;
        protected UIActivityIndicatorView spinner;
        protected BodyWebView webView;
        protected PointF scrollStartingOffset;

        protected McAbstrItem abstrItem;
        protected string downloadToken;

        public new float MinimumZoomScale {
            get {
                return base.MinimumZoomScale;
            }
            set {
                base.MinimumZoomScale = Math.Min (base.MinimumZoomScale, value);
            }
        }

        // Various delegates for notification
        public RenderStart OnRenderStart;
        public RenderComplete OnRenderComplete;
        public DownloadStart OnDownloadStart;

        public BodyView (RectangleF initialFrame, UIView parentView)
            : this(initialFrame, parentView, 15, 0)
        {
        }

        public BodyView (RectangleF initialFrame, UIView parentView, float leftMargin, float htmlLeftMargin)
        {
            ViewHelper.SetDebugBorder (this, SCROLLVIEW_BDCOLOR);

            HorizontalScrollingEnabled = true;
            VerticalScrollingEnabled = true;
            AutomaticallyScaleHtmlContent = true;
            this.leftMargin = leftMargin;
            this.htmlLeftMargin = htmlLeftMargin;
            ScrollEnabled = false;

            this.parentView = parentView;
            BackgroundColor = SCROLLVIEW_BGCOLOR;
            Frame = initialFrame;
            MinimumZoomScale = 1.0f;
            MaximumZoomScale = 1.0f;

            ViewForZoomingInScrollView = delegate {
                return messageView;
            };
            // TODO - Scrolling and zooming was originally planned but after
            // the 2nd version of BodyView, the scrolling is either entirely disabled
            // (in hot list) or completely delegated to the parent view (message view).
            // Hence, the base class UIScrollView is no longer used. It is conceivable
            // that we enable scrolling (and zooming) for hot list eventually so this
            // is kept around but disabled for now.
            DidZoom += OnDidZoom;
            ZoomingStarted += OnZoomingStarted;
            ZoomingEnded += OnZoomingEnded;

            // messageView contains all content views of the body
            messageView = new UIView ();
            ViewHelper.SetDebugBorder (messageView, MESSAGEVIEW_BDCOLOR);
            messageView.BackgroundColor = MESSAGEVIEW_BGCOLOR;
            messageView.Frame = ViewHelper.InnerFrameWithInset(Frame, MESSAGEVIEW_INSET);
            AddSubview (messageView);

            // spinner indicates download activity
            spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.HidesWhenStopped = true;
            spinner.Tag = (int)TagType.SPINNER_TAG;
            AddSubview (spinner);
        }

        public bool Configure (McAbstrItem item)
        {
            abstrItem = item;
            downloadToken = null;
            ZoomScale = 1.0f;

            PointF center = !SpinnerCenteredOnParentFrame ? Center : Superview.Center;
            center.X -= Frame.X;
            center.Y -= Frame.Y;
            spinner.Center = center;

            // TODO: Revisit
            for (int i = messageView.Subviews.Length - 1; i >= 0; i--) {
                messageView.Subviews [i].RemoveFromSuperview ();
            }
            // Web view consumes a lot of memory. We manually dispose it to force all ObjC
            // objects to be freed.
            if (null != webView) {
                if (webView.IsLoading) {
                    webView.StopLoading ();
                }
                webView.Dispose ();
            }
            webView = new BodyWebView (messageView, htmlLeftMargin);

            if (item.IsDownloaded ()) {
                loadState = LoadState.IDLE;
                spinner.StopAnimating ();
            } else {
                if (LoadState.ERROR == loadState) {
                    Log.Info (Log.LOG_UI, "Previous download resulted in error");
                    RenderPartialDownloadMessage ("[ Message preview only. Tap here to download ]");
                    RenderTextString (item.GetBodyPreviewOrEmpty ());
                    return false;
                }
                if (!item.IsDownloaded ()) {
                    Log.Info (Log.LOG_UI, "Starting download of whole message body");
                    if (LoadState.LOADING != loadState) {
                        switch (item.GetType ().Name) {
                        case "McEmailMessage":
                            StartDownload ();
                            break;
                        default:
                            var msg = String.Format ("unhandle abstract item type {0}", item.GetType ().Name);
                            throw new NcAssert.NachoDefaultCaseFailure (msg);
                        }
                    }
                    IndicateDownloadStarted ();
                    return false;
                }
            }

            var bodyPath = item.GetBodyPath ();
            if (null == bodyPath) {
                return false;
            }
            switch (item.GetBodyType ()) {
            case McBody.PlainText:
                RenderTextString (item.GetBody ());
                break;
            case McBody.HTML:
                RenderHtmlString (item.GetBody ());
                break;
            case McBody.RTF:
                RenderRtfString (item.GetBody ());
                break;
            case McBody.MIME:
                RenderMime (bodyPath);
                break;
            default:
                Log.Info (Log.LOG_EMAIL, "BodyType zero; likely old client");
                RenderMime (bodyPath);
                break;
            }

            //if (null != message.MeetingRequest && !calendarRendered) {
            //    var UID = Util.GlobalObjIdToUID (message.MeetingRequest.GlobalObjId);
            //    MakeStyledCalendarInvite (UID, message.Subject, message.MeetingRequest.AllDayEvent, message.MeetingRequest.StartTime, message.MeetingRequest.EndTime, message.MeetingRequest.Location, view);
            //}

            return true;
        }

        private void OnDidZoom (object sender, EventArgs e)
        {
            throw new NotImplementedException ();
        }

        private void OnZoomingStarted (object sender, UIScrollViewZoomingEventArgs e)
        {
            throw new NotImplementedException ();
        }

        private void OnZoomingEnded (object sender, ZoomingEndedEventArgs e)
        {
            throw new NotImplementedException ();
        }

        protected void IterateAllRenderSubViews (IterateCallback callback)
        {
            NcAssert.True (null != callback);
            foreach (var subview in messageView.Subviews) {
                if ((int)TagType.MESSAGE_PART_TAG != subview.Tag) {
                    continue;
                }
                if (!callback (subview)) {
                    return;
                }
            }
        }

        protected void RenderMime (string bodyPath)
        {
            try {
                using (var bodySource = new FileStream (bodyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                    var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                    var mime = bodyParser.ParseMessage ();
                    MimeHelpers.DumpMessage (mime, 0);
                    var list = new List<MimeEntity> ();
                    MimeHelpers.MimeDisplayList (mime, ref list);
                    RenderDisplayList (list);
                }
            }
            catch (System.IO.IOException e) {
                Log.DumpFileDescriptors ();
            }
        }

        protected void RenderDisplayList (List<MimeEntity> list)
        {
            foreach (var entity in list) {
                var part = (MimePart)entity;
                if (null != part.ContentId) {
                    PlatformHelpers.AddCidPartToDict (part.ContentId, part);
                }
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtml (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "calendar")) {
                    RenderCalendar (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "rtf")) {
                    RenderRtf (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "*")) {
                    RenderText (part);
                    continue;
                }
                if (part.ContentType.Matches ("image", "*")) {
                    RenderImage (part);
                    continue;
                }
            }
        }

        void RenderText (MimePart part)
        {
            var textPart = part as TextPart;
            var text = textPart.Text;
            RenderTextString (text);
        }

        void RenderRtf (MimePart part)
        {
            var textPart = part as TextPart;
            var text = textPart.Text;
            RenderRtfString (text);
        }

        protected void RenderAttributedString (NSAttributedString attributedString)
        {
            var label = new BodyTextView (messageView.Frame);
            label.Configure (attributedString);
            messageView.AddSubview (label);
        }

        public void RenderTextString (string text)
        {
            MonoTouch.CoreText.CTStringAttributes attributes;
            MonoTouch.CoreText.CTFont font;
            UIFont uiFont = A.Font_AvenirNextRegular17;
            font = new MonoTouch.CoreText.CTFont (uiFont.Name, uiFont.PointSize);

            attributes = new MonoTouch.CoreText.CTStringAttributes ();
            attributes.Font = font;

            var attributedString = new NSAttributedString (text, attributes);
            RenderAttributedString (attributedString);
        }

        void RenderPartialDownloadMessage (string message)
        {
            var attributedString = new NSAttributedString (message);
            var label = new UILabel (new RectangleF (leftMargin, 0.0f, 290.0f, 1.0f));
            label.Lines = 0;
            label.Font = A.Font_AvenirNextDemiBold14;
            label.LineBreakMode = UILineBreakMode.WordWrap;
            label.AttributedText = attributedString;
            label.TextColor = A.Color_808080;
            label.SizeToFit ();
            var frame = label.Frame;
            frame.Height = frame.Height + 30;
            label.Frame = frame;
            label.Tag = (int)TagType.DOWNLOAD_TAG;
            label.UserInteractionEnabled = true;

            // Detect tap of the partially download mesasge label
            var singletap = new UITapGestureRecognizer ();
            singletap.NumberOfTapsRequired = 1;
            singletap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("DownloadMessage:"));
            singletap.ShouldRecognizeSimultaneously = delegate {
                return false;
            };
            label.AddGestureRecognizer (singletap);

            messageView.AddSubview (label);
        }

        void RenderRtfString (string rtf)
        {
            var nsError = new NSError ();
            var nsAttributes = new NSAttributedStringDocumentAttributes ();
            nsAttributes.DocumentType = NSDocumentType.RTF;
            var attributedString = new NSAttributedString (rtf, nsAttributes, ref nsError);
            RenderAttributedString (attributedString);
        }

        void RenderImage (MimePart part)
        {
            // Outlook and Thunderbird do not render MIME image parts. Embedded images
            // are referenced via CID in HTML part.
            #if (RENDER_IMAGE)
            var iv = new BodyImageView (Frame);
            iv.Configure (part);
            messageView.AddSubview (iv);
            #endif
        }

        void RenderHtml (MimePart part)
        {
            var textPart = part as TextPart;
            var html = textPart.Text;
            RenderHtmlString (html);
        }

        void RenderHtmlString (string html)
        {
            webView.OnRenderStart = () => {
                if (null != OnRenderStart) {
                    OnRenderStart ();
                }
            };
            webView.OnRenderComplete = (float minimumZoomScale) => {
                if (null != OnRenderComplete) {
                    OnRenderComplete ();
                }
            };
            messageView.Add (webView);
            webView.LoadHtmlString (html);
        }

        /// TODO: Guard against malformed calendars
        public void RenderCalendar (MimePart part)
        {
            var calView = new BodyCalendarView (this);
            calView.Configure (part);
            messageView.AddSubview (calView);
        }

        public void Layout (float X, float Y, float width, float height)
        {
            // Layout all message parts in messageView
            var messageCursor = new VerticalLayoutCursor (messageView);
            messageCursor.IteratorSubviewsWithFilter ((v) => {
                return true;
            });

            // Sum up all the render parts for height
            float messageWidth = 0.0f, messageHeight = 0.0f;
            float contentWidth = 0.0f, contentHeight = 0.0f;
            IterateAllRenderSubViews ((UIView subview) => {
                messageHeight += subview.Frame.Height;
                messageWidth = Math.Max (messageWidth, subview.Frame.Width);

                IBodyRender renderView = subview as IBodyRender;
                contentHeight += renderView.ContentSize.Height;
                contentWidth = Math.Max(contentWidth, renderView.ContentSize.Width);

                return true;
            });

            // Decide the message view size based on the bounding frame.
            messageWidth = Math.Max (width - 2 * MESSAGEVIEW_INSET, messageView.Frame.Width);
            messageHeight = Math.Max (height - 2 * MESSAGEVIEW_INSET, messageView.Frame.Height);
            ViewFramer.Create (messageView)
                .Width (messageWidth)
                .Height (messageHeight);

            ContentSize = new SizeF (contentWidth, contentHeight);

            // Put the view at the right location
            ViewFramer.Create (this)
                .X (X)
                .Y (Y);

            Console.WriteLine ("BODYVIEW LAYOUT");
            Console.WriteLine (LayoutInfo ());
        }

        protected void StartDownload ()
        {
            downloadToken = BackEnd.Instance.DnldEmailBodyCmd (abstrItem.AccountId, abstrItem.Id);
            if (null != downloadToken) {
                BackEnd.Instance.Prioritize (abstrItem.AccountId, downloadToken);
            } else {
                var newAbstrItem = ReReadItem ();
                if ((null == newAbstrItem) || newAbstrItem.IsDownloaded ()) {
                    // Download must have complete in the window from it was checked to
                    // download command here is issued. Must have a status indication
                    // pending to stop the spinner or remove the item from UI. Just need
                    // to not print error message
                    return;
                }
                // Duplicate download command returns the first (highest priority)
                // download's token. So, a null really means something has gone wrong.
                Log.Warn (Log.LOG_UI, "Fail to start download for message {0} in account {1}",
                    abstrItem.Id, abstrItem.AccountId);
                RenderPartialDownloadMessage ("[ Message preview only. Tap here to download ]");
                RenderTextString (abstrItem.GetBodyPreviewOrEmpty ());
                return;
            }
        }

        [MonoTouch.Foundation.Export ("DownloadMessage:")]
        public void OnDownloadMessage (UIGestureRecognizer sender)
        {
            IndicateDownloadStarted ();
            StartDownload ();
        }

        protected void IndicateDownloadStarted ()
        {
            loadState = LoadState.LOADING;
            spinner.StartAnimating ();
            if (null != OnDownloadStart) {
                OnDownloadStart ();
            }
        }

        public bool DownloadComplete (bool succeed, string token)
        {
            if (token != downloadToken) {
                return false; // indication for a different message
            }
            loadState = succeed ? LoadState.IDLE : LoadState.ERROR;
            spinner.StopAnimating ();
            return true;
        }

        public bool WasDownloadStartedAndNowComplete ()
        {
            if (LoadState.LOADING != loadState) {
                return false;
            }
            var newAbstrItem = ReReadItem ();
            if (null == newAbstrItem) {
                return false; // must have been deleted
            }
            return newAbstrItem.IsDownloaded ();
        }

        public void Focus ()
        {
            if (null != downloadToken) {
                // Possible to issue redundant prioritize requests which becomes no op
                BackEnd.Instance.Prioritize (abstrItem.AccountId, downloadToken);
            }
        }

        public string LayoutInfo ()
        {
            string desc = "\n";
            desc += String.Format ("scrollView: offset={0}  frame={1}  content={2}\n",
                Pretty.PointF(Frame.Location), Pretty.SizeF(Frame.Size), Pretty.SizeF(ContentSize));
            desc += String.Format ("  messageView: offset={0}  frame={1}\n",
                Pretty.PointF (messageView.Frame.Location), Pretty.SizeF (messageView.Frame.Size));
            foreach (var subview in messageView.Subviews) {
                if ((int)TagType.MESSAGE_PART_TAG != subview.Tag) {
                    continue;
                }
                IBodyRender renderView = subview as IBodyRender;

                desc += String.Format ("    {0}\n", renderView.LayoutInfo ());
            }
            return desc;
        }

        public void ScrollTo (PointF contentOffset)
        {
            PointF subviewOffset = new PointF (contentOffset.X, contentOffset.Y);
            IterateAllRenderSubViews ((UIView subview) => {
                IBodyRender renderView = subview as IBodyRender;
                NcAssert.True (null != renderView);
                PointF clippedOffset = ClipContentOffset(subview, subviewOffset);
                renderView.ScrollTo (clippedOffset);
                subviewOffset.Y -= renderView.ContentSize.Height;
                return true;
            });
        }

        public static PointF ClipContentOffset (UIView view, PointF offset)
        {
            IBodyRender renderView = view as IBodyRender;
            NcAssert.True (null != renderView);

            float x = offset.X;
            float y = offset.Y;
            if (0.0f > x) {
                x = 0.0f;
            } else if (renderView.ContentSize.Width < x) {
                x = renderView.ContentSize.Width;
            }
            if (0.0f > y) {
                y = 0.0f;
            } else if (renderView.ContentSize.Height < y) {
                y = renderView.ContentSize.Height;
            }
            return new PointF (x, y);
        }

        protected McAbstrItem ReReadItem ()
        {
            McAbstrItem newAbstrItem;
            string className = abstrItem.GetType ().Name;
            switch (className) {
            case "McEmailMessage":
                newAbstrItem = (McAbstrItem)McEmailMessage.QueryById<McEmailMessage> (abstrItem.Id);
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format("Unhandled class type {0}", className));
            }
            return newAbstrItem;
        }
    }
}

