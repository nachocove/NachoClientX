//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using Foundation;
using UIKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    /// <summary>
    /// Similar to body view; however we need user interaction enabled
    /// </summary>
    public class UcLocationView : UITextView
    {
        protected BodyView.LinkSelectedCallback onLinkSelected;

        public UcLocationView (nfloat Y, nfloat preferredWidth, BodyView.LinkSelectedCallback onLinkSelected)
            : base (new CGRect (0, Y, preferredWidth, 1))
        {
            this.onLinkSelected = onLinkSelected;

            DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber;
            Delegate = new UcLocationViewDelegate (this);

            Editable = false;
            Selectable = true;

            ScrollEnabled = false;
            UserInteractionEnabled = true;
            ShowsHorizontalScrollIndicator = false;
            ShowsVerticalScrollIndicator = false;
        }

        public void SetText(string text)
        {
            Text = text;
            SizeToFit ();
        }

        public void Cleanup ()
        {
            var locationViewDelegate = (UcLocationViewDelegate)this.Delegate;
            locationViewDelegate.owner = null;
            this.onLinkSelected = null;
            this.Delegate = null;
        }

        protected class UcLocationViewDelegate : UITextViewDelegate
        {
            public UcLocationView owner;

            public UcLocationViewDelegate (UcLocationView owner)
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

