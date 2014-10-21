//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyTextView : UITextView, IBodyRender
    {
        // No need to implement ContentSize (for IBodyRender). Re-use UITextView's
        // ContentSize (from its parent UIScrollView)

        public BodyTextView (IntPtr ptr) : base (ptr)
        {
        }

        public BodyTextView (RectangleF frame) : base (frame)
        {
            ViewHelper.SetDebugBorder (this, UIColor.Blue);

            Editable = false;
            Font = A.Font_AvenirNextRegular17;
            Tag = (int)BodyView.TagType.MESSAGE_PART_TAG;
            // TODO - In UIWebView, setting ScrollEnabled = false results in disabling the scroll bar
            // interactively but still allows it to function programmatically. This is required for
            // implementing ScrollTo(). But in UITextView, this technique does not seem to work.
            //
            // So, the current solution is to re-enable scrolling but disable user interaction. The
            // body view scroll view has been disabled. So, including the view controller's, there are
            // only two of them. So, this should be ok.
            ScrollEnabled = true;
            UserInteractionEnabled = false;
            ShowsHorizontalScrollIndicator = false;
            ShowsVerticalScrollIndicator = false;
            // We are using double tap for zoom toggling. So, we want to disable 
            // double tap to select action. A long tap can still select text.
            foreach (var gr in GestureRecognizers) {
                var tapGr = gr as UITapGestureRecognizer;
                if (null == tapGr) {
                    continue;
                }
                if ((1 == tapGr.NumberOfTouchesRequired) && (2 == tapGr.NumberOfTapsRequired)) {
                    tapGr.Enabled = false;
                }
            }
        }

        public override void SizeToFit ()
        {
            // Intentionally disable SizeToFit(). By allowing the base class SizeToFit() to
            // take effect, it will create a UIWebView with its frame equal to the content size
            // for large HTML email, it will consume a lot of memory. And ViewHelper.LayoutCursor
            // always call SizeToFit(). So, we need to disable it.
            base.ContentSize = SizeThatFits (Frame.Size);
        }

        public void Configure (NSAttributedString attributedString)
        {
            AttributedText = attributedString;
            base.ContentSize = SizeThatFits (Frame.Size);
        }

        public void ScrollTo (PointF upperLeft)
        {
            SetContentOffset (upperLeft, false);
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyTextView: offset={0}  frame={1}  content={2}",
                Pretty.PointF (Frame.Location), Pretty.SizeF (Frame.Size), Pretty.SizeF (ContentSize));
        }
    }
}

