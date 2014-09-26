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
                //NcAssert.True (1 < count);
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

        #if (DEBUG_UI)
        static UIColor SCROLLVIEW_BGCOLOR = A.Color_NachoGreen;
        static UIColor MESSAGEVIEW_BGCOLOR = A.Color_NachoYellow;
        const int MESSAGEVIEW_INSET = 4;
        const int BODYVIEW_INSET = 4;
        #else
        static UIColor SCROLLVIEW_BGCOLOR = UIColor.White;
        static UIColor MESSAGEVIEW_BGCOLOR = UIColor.White;
        const int MESSAGEVIEW_INSET = 2;
        const int BODYVIEW_INSET = 0;
        //const int BODYVIEW_INSET = 4;
        #endif

        protected enum LoadState {
            IDLE,    // body is already there
            LOADING, // body is being loaded
            ERROR    // body was being loaded
        }

        public bool HorizontalScrollingEnabled { get; set; }

        public bool VeriticalScrollingEnabled { get; set; }

        public bool AutomaticallyScaleHtmlContent { get; set; }

        // If false, center on view frame
        public bool SpinnerCenteredOnParentFrame { get; set; }

        protected UIView parentView;
        protected UIView messageView;
        protected UITapGestureRecognizer doubleTap;
        protected LoadState loadState;
        protected UIActivityIndicatorView spinner;
        protected BodyWebView webView;

        protected McAbstrItem abstrItem;

        // Various delegates for notification
        public RenderStart OnRenderStart;
        public RenderComplete OnRenderComplete;
        public DownloadStart OnDownloadStart;

        public BodyView (RectangleF initialFrame, UIView parentView)
        {
            HorizontalScrollingEnabled = true;
            VeriticalScrollingEnabled = true;
            AutomaticallyScaleHtmlContent = true;

            this.parentView = parentView;
            BackgroundColor = SCROLLVIEW_BGCOLOR;
            Frame = initialFrame;
            DidZoom += (object sender, EventArgs e) => {
                Log.Info (Log.LOG_UI, "body view scroll view did zoom");
            };
            MinimumZoomScale = 0.5f;
            MaximumZoomScale = 4.0f;
            ViewForZoomingInScrollView = delegate {
                return messageView;
            };
            ZoomingStarted += delegate(object sender, UIScrollViewZoomingEventArgs e) {
                OnRenderStart ();
            };
            ZoomingEnded += delegate(object sender, ZoomingEndedEventArgs e) {
                Log.Debug (Log.LOG_UI, "body view scrollview zoomed (AtScale={0})", e.AtScale);
                OnRenderComplete ();
            };

            // doubleTap handles zoom in and out
            doubleTap = new UITapGestureRecognizer ();
            doubleTap.NumberOfTapsRequired = 2;
            doubleTap.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("DoubleTapSelector:"));
            doubleTap.ShouldRecognizeSimultaneously = delegate {
                return true;
            };

            // messageView contains all content views of the body
            messageView = new UIView ();
            messageView.BackgroundColor = MESSAGEVIEW_BGCOLOR;
            messageView.Frame = ViewHelper.InnerFrameWithInset(Frame, MESSAGEVIEW_INSET);
            messageView.AddGestureRecognizer (doubleTap);
            AddSubview (messageView);

            // spinner indicates download activity
            spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.HidesWhenStopped = true;
            spinner.Tag = (int)TagType.SPINNER_TAG;
            AddSubview (spinner);

            // webView holds all HTML content
            webView = new BodyWebView (this);
        }

        public void Configure (McAbstrItem item)
        {
            abstrItem = item;

            PointF center = !SpinnerCenteredOnParentFrame ? Center : Superview.Center;
            center.X -= Frame.X;
            center.Y -= Frame.Y;
            spinner.Center = center;

            // TODO: Revisit
            for (int i = messageView.Subviews.Length - 1; i >= 0; i--) {
                messageView.Subviews [i].RemoveFromSuperview ();
            }

            if (item.IsDownloaded ()) {
                loadState = LoadState.IDLE;
            } else {
                if (LoadState.ERROR == loadState) {
                    Log.Info (Log.LOG_UI, "Previous download resulted in error");
                    RenderPartialDownloadMessage ("[ Message preview only. Tap here to download ]");
                    RenderTextString (item.GetBodyPreviewOrEmpty ());
                    return;
                }
                if (!item.IsDownloaded ()) {
                    Log.Info (Log.LOG_UI, "Starting download of whole message body");
                    if (LoadState.LOADING != loadState) {
                        switch (item.GetType ().Name) {
                        case "McEmailMessage":
                            BackEnd.Instance.DnldEmailBodyCmd (item.AccountId, item.Id);
                            break;
                        default:
                            var msg = String.Format ("unhandle abstract item type {0}", item.GetType ().Name);
                            throw new NcAssert.NachoDefaultCaseFailure (msg);
                        }
                    }
                    IndicateDownloadStarted ();
                    return;
                }
            }

            var bodyPath = item.GetBodyPath ();
            if (null == bodyPath) {
                return;
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
        }

        protected void RenderMime (string bodyPath)
        {
            using (var bodySource = new FileStream (bodyPath, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                var mime = bodyParser.ParseMessage ();
                PlatformHelpers.motd = mime; // for cid handler
                MimeHelpers.DumpMessage (mime, 0);
                var list = new List<MimeEntity> ();
                MimeHelpers.MimeDisplayList (mime, ref list);
                RenderDisplayList (list);
            }
        }

        protected void RenderDisplayList (List<MimeEntity> list)
        {
            for (var i = 0; i < list.Count; i++) {
                var entity = list [i];
                var part = (MimePart)entity;
                if (part.ContentType.Matches ("text", "html")) {
                    RenderHtml (part);
                    continue;
                }
                if (part.ContentType.Matches ("text", "calendar")) {
                    RenderCalendar (part);
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

        UITextView RenderAttributedString (NSAttributedString attributedString)
        {
            var label = new UITextView (new RectangleF (15.0f, 0.0f, 290.0f, 1.0f));
            label.Editable = false;
            #if (DEBUG_UI)
            label.BackgroundColor = A.Color_NachoBlue;
            #endif
            label.Font = A.Font_AvenirNextRegular17;
            label.AttributedText = attributedString;
            label.SizeToFit ();
            ViewFramer.Create (label).AdjustHeight (30.0f);
            label.Tag = (int)TagType.MESSAGE_PART_TAG;
            messageView.AddSubview (label);
            return label;
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
            var label = new UILabel (new RectangleF (15.0f, 0.0f, 290.0f, 1.0f));
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
            var image = PlatformHelpers.RenderImage (part);

            float width = Frame.Width;
            float height = image.Size.Height * (width / image.Size.Width);
            image = image.Scale (new SizeF (width, height));

            var iv = new UIImageView (image);
            iv.Tag = (int)TagType.MESSAGE_PART_TAG;
            messageView.AddSubview (iv);
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
                OnRenderStart ();
            };
            webView.OnRenderComplete = (float minimumZoomScale) => {
                MinimumZoomScale = minimumZoomScale;
                if (AutomaticallyScaleHtmlContent && (minimumZoomScale < 1.0)) {
                    SetZoomScale (ZoomOutScale (), false);
                }
                OnRenderComplete ();
            };
            messageView.Add (webView);
            webView.LoadHtmlString (html);
        }


        /// TODO: Guard against malformed calendars
        public void RenderCalendar (MimePart part)
        {
            var calView = new BodyCalendarView (this);
            messageView.AddSubview (calView);
        }

        float ZoomOutScale ()
        {
            // Minimum zoom scale should scale the content to just a bit narrower
            // than the bounding frame. However, scaling to this value often results
            // in unreadable content. So, we lower bound the zoom out scale to 0.7.
            return Math.Max (0.7f, MinimumZoomScale);
        }

        float ZoomInScale ()
        {
            return 2.0f * ZoomOutScale ();
        }

        [MonoTouch.Foundation.Export ("DoubleTapSelector:")]
        public void OnDoubleTap (UIGestureRecognizer sender)
        {
            if (ZoomScale == ZoomOutScale ()) {
                SetZoomScale (ZoomInScale (), true);
            } else {
                SetZoomScale (ZoomOutScale (), true);
            }
        }

        public void Layout (float X, float Y, float width, float height)
        {
            // Layout all message parts in messageView
            var messageCursor = new VerticalLayoutCursor (messageView);
            messageCursor.IteratorSubviewsWithFilter ((v) => {
                return true;
            });

            // Update the scroll view with the up-to-date message view size
            ContentSize = new SizeF (messageView.Frame.Width, messageView.Frame.Height);

            // Adjust for outer boundary for insets
            height -= 2 * BODYVIEW_INSET;
            height -= 2 * MESSAGEVIEW_INSET;
            width -= 2 * BODYVIEW_INSET;

            // Set up scroll view frame based on the configured scrolling options
            float messageWidth = width, messageHeight = height;
            if (!HorizontalScrollingEnabled) {
                messageWidth = Math.Max (width, messageView.Frame.Width * ZoomScale);
            }
            if (!VeriticalScrollingEnabled) {
                messageHeight = Math.Max (width, messageView.Frame.Height * ZoomScale);
            }
            Frame = new RectangleF (X, Y, messageWidth, messageHeight);
        }

        [MonoTouch.Foundation.Export ("DownloadMessage:")]
        public void OnDownloadMessage (UIGestureRecognizer sender)
        {
            IndicateDownloadStarted ();
            BackEnd.Instance.DnldEmailBodyCmd (abstrItem.AccountId, abstrItem.Id);
        }

        protected void IndicateDownloadStarted ()
        {
            loadState = LoadState.LOADING;
            spinner.StartAnimating ();
            OnDownloadStart ();
        }

        public void DownloadComplete (bool succeed)
        {
            loadState = succeed ? LoadState.IDLE : LoadState.ERROR;
            spinner.StopAnimating ();
        }

        public bool WasDownloadStartedAndNowComplete ()
        {
            if (LoadState.LOADING != loadState) {
                return false;
            }
            // Read the item again to get the new body state
            McAbstrItem newAbstrItem;
            string className = abstrItem.GetType ().Name;
            switch (className) {
            case "McEmailMessage":
                newAbstrItem = (McAbstrItem)McEmailMessage.QueryById<McEmailMessage> (abstrItem.Id);
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format("Unhandled class type {0}", className));
            }
            return newAbstrItem.IsDownloaded ();
        }
    }
}

