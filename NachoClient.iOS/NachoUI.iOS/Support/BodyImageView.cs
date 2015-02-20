//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using UIKit;
using MimeKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyImageView : UIImageView, IBodyRender
    {
        public BodyImageView (nfloat Y, nfloat preferredWidth, UIImage image)
            : base (new CGRect(0, Y, preferredWidth, 1))
        {
            if (image.Size.Width > preferredWidth) {
                // Shrink the image so it fits in the given width
                nfloat newHeight = image.Size.Height * (preferredWidth / image.Size.Width);
                Image = image.Scale (new CGSize (preferredWidth, newHeight));
            } else {
                Image = image;
            }
            ViewFramer.Create (this).Width (Image.Size.Width).Height (Image.Size.Height);
        }

        public UIView uiView ()
        {
            return this;
        }

        public CGSize ContentSize {
            get {
                return Image.Size;
            }
        }

        public void ScrollingAdjustment (CGRect frame, CGPoint contentOffset)
        {
            // Image does not scroll or resize.
            // The only thing that can be adjusted is the view's origin.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y);
        }
    }
}

