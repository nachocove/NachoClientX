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
        protected BodyWebView.LinkSelectedCallback onLinkSelected;

        public UcLocationView (nfloat X, nfloat Y, nfloat preferredWidth, BodyWebView.LinkSelectedCallback onLinkSelected)
            : base (new CGRect (X, Y, preferredWidth, 1))
        {
            this.onLinkSelected = onLinkSelected;

            TextContainerInset = new UIEdgeInsets (0, 0, 0, 0);

            DataDetectorTypes = UIDataDetectorType.Link | UIDataDetectorType.PhoneNumber | UIDataDetectorType.Address;
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
                if (URL.Scheme == "x-apple-data-detectors") {
                    // That scheme is used for addresses that should be opened in the Maps app.  But the
                    // important data is missing from the URL, so passing it to UIApplication.OpenURL()
                    // won't work.  The OS needs to handle this one directly.
                    return true;
                }
                if ((null != owner) && (null != owner.onLinkSelected)) {
                    owner.onLinkSelected (URL);
                }
                return false;
            }
        }

    }
}

