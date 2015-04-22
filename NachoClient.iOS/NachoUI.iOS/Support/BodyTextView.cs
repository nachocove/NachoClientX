//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using Foundation;
using UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyTextView : UITextView, IBodyRender
    {
        protected BodyView.LinkSelectedCallback onLinkSelected;

        public BodyTextView (nfloat Y, nfloat preferredWidth, NSAttributedString text, BodyView.LinkSelectedCallback onLinkSelected)
            : base (new CGRect(0, Y, preferredWidth, 1))
        {
            this.onLinkSelected = onLinkSelected;

            DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber;
            Delegate = new BodyTextViewDelegate (this);

            Editable = false;
            Selectable = true;
            AttributedText = text;
            base.ContentSize = SizeThatFits (Frame.Size);

            // There is a bug in UITextView where scrolling programatically (i.e. setting
            // ContentOffset) doesn't work if ScrollEnabled is false.  If both ScrollEnabled
            // and UserInteractionEnabled are true, then the UITextView will intercept the
            // scrolling gestures that should be handled by an outer UIScrollView.  If
            // ScrollEnabled is true and UserInteractionEnabled is false, then scrolling
            // programatically will work, but links within the text are not clickable.
            // The only way to have clickable links and to not mess up the scrolling is to
            // have UserInteractionEnabled be true and ScrollEnabled be false.  But that
            // means we can't scroll programatically and instead have to render the entire
            // UITextView.  That will result in greater memory usage for large pieces of
            // text.  But really large plain text is much less common than really large
            // UIWebView, so this is not expected to be a serious problem.
            ScrollEnabled = false;
            UserInteractionEnabled = true;
            ViewFramer.Create (this).Height (base.ContentSize.Height);
        }

        public UIView uiView ()
        {
            return this;
        }

        public new CGSize ContentSize {
            get {
                return base.ContentSize;
            }
        }

        public void ScrollingAdjustment (CGRect frame, CGPoint contentOffset)
        {
            // Scrolling and resizing is not supported.  The only thing that can be adjusted
            // is the view's origin.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y);
        }

        protected override void Dispose (bool disposing)
        {
            var bodyTextViewDelegate = (BodyTextViewDelegate)this.Delegate;
            bodyTextViewDelegate.owner = null;
            this.onLinkSelected = null;
            this.Delegate = null;
            base.Dispose (disposing);
        }

        protected class BodyTextViewDelegate : UITextViewDelegate
        {
            public BodyTextView owner;

            public BodyTextViewDelegate(BodyTextView owner)
            {
                this.owner = owner;
            }

            public override bool ShouldInteractWithUrl (UITextView textView, NSUrl URL, NSRange characterRange)
            {
                if ((null != owner) && (null != owner.onLinkSelected)) {
                    owner.onLinkSelected (URL);
                }
                return false;
            }
        }

    }
}

