//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    public class BodyTextView : UITextView, IBodyRender
    {
        public BodyTextView (RectangleF frame) : base (frame)
        {
            ViewHelper.SetDebugBorder (this, UIColor.Blue);

            Editable = false;
            Font = A.Font_AvenirNextRegular17;
            Tag = (int)BodyView.TagType.MESSAGE_PART_TAG;
            // We don't need this view to scroll. This would result in a triple nesting of
            // UIScrollView, which iOS doesn't handle well.  It handles two nested UIScrollViews,
            // but seems to have problems with three.
            ScrollEnabled = false;
            // We are using double tap for zoom toggling. So, we want to disable 
            // double tap to select action. A long tap can still select text.
            foreach (var gc in GestureRecognizers) {
                var tapGc = gc as UITapGestureRecognizer;
                if (null == tapGc) {
                    continue;
                }
                if ((1 == tapGc.NumberOfTouchesRequired) && (2 == tapGc.NumberOfTapsRequired)) {
                    tapGc.Delegate = new BodyViewTextTapBlocker ();
                }
            }
        }

        public void Configure (NSAttributedString attributedString)
        {
            AttributedText = attributedString;
            SizeToFit ();
        }

        public void ScrollTo (PointF upperLeft)
        {
            // Text view is scrollable but we make it not scrollable
        }

        public string LayoutInfo ()
        {
            return String.Format ("BodyTextView: offset=({0},{1})  frame=({2},{3})  content=({4},{5})",
                Frame.X, Frame.Y, Frame.Width, Frame.Height, ContentSize.Width, ContentSize.Height);
        }
    }

    public class BodyViewTextTapBlocker : UIGestureRecognizerDelegate
    {
        public BodyViewTextTapBlocker ()
        {
        }

        public override bool ShouldReceiveTouch (UIGestureRecognizer recognizer, UITouch touch)
        {
            return false;
        }

        public override bool ShouldBegin (UIGestureRecognizer recognizer)
        {
            return false;
        }
    }
}

