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
           
            // Workaround a bug.  UITextView, which is a kind of UIScrollView, only allows
            // scrolling programatically if ScrollEnabled is true.  If ScrollEnabled is
            // false, then setting ContentOffset has no effect (or the wrong effect). To
            // work around this, set ScrollEnabled to true but UserInteraction to false.
            // This allows scrolling programatically but doesn't intercept any of the
            // scroll gestures.
            ScrollEnabled = true;
            UserInteractionEnabled = false;
            ShowsHorizontalScrollIndicator = false;
            ShowsVerticalScrollIndicator = false;

            // Render at most one screenful of text at a time.
            ViewFramer.Create (this)
                .Height (NMath.Min (ContentSize.Height, UIScreen.MainScreen.Bounds.Height));
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
            this.Frame = frame;
            this.ContentOffset = contentOffset;
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

