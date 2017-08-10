//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ImageAccessoryView : UIView
    {
        public UIImageView ImageView { get; private set; }

        public ImageAccessoryView (string imageName, float width = 30.0f, UIViewContentMode contentMode = UIViewContentMode.Left) : base (new CGRect (0.0f, 0.0f, (nfloat)width, (nfloat)width))
        {
            BackgroundColor = UIColor.White;
            using (var image = UIImage.FromBundle (imageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysTemplate)) {
                ImageView = new UIImageView (image);
            }
            ContentMode = contentMode;
            AddSubview (ImageView);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            switch (ContentMode) {
            default:
            case UIViewContentMode.Left:
                ImageView.Center = new CGPoint (ImageView.Frame.Width / 2.0f, Bounds.Height / 2.0f);
                break;
            case UIViewContentMode.Center:
                ImageView.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height / 2.0f);
                break;
            }
        }

        public override CGSize IntrinsicContentSize {
            get {
                return ImageView.Image.Size;
            }
        }
    }
}

