//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;
using MimeKit;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class BodyImageView : UIImageView, IBodyRender
    {
        public BodyImageView (float Y, float preferredWidth, UIImage image)
            : base (new RectangleF(0, Y, preferredWidth, 1))
        {
            if (image.Size.Width > preferredWidth) {
                // Shrink the image so it fits in the given width
                float newHeight = image.Size.Height * (preferredWidth / image.Size.Width);
                Image = image.Scale (new SizeF (preferredWidth, newHeight));
            } else {
                Image = image;
            }
            ViewFramer.Create (this).Width (Image.Size.Width).Height (Image.Size.Height);
        }

        public UIView uiView ()
        {
            return this;
        }

        public SizeF ContentSize {
            get {
                return Image.Size;
            }
        }

        public void ScrollingAdjustment (RectangleF frame, PointF contentOffset)
        {
            // Image does not scroll or resize.
            // The only thing that can be adjusted is the view's origin.
            ViewFramer.Create (this).X (frame.X - contentOffset.X).Y (frame.Y - contentOffset.Y);
        }
    }
}

