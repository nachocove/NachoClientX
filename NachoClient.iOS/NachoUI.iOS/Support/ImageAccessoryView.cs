//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ImageAccessoryView : UIView
    {
        protected UIImageView ImageView;

        public ImageAccessoryView (string imageName, float width = 30.0f) : base (new CGRect(0.0f, 0.0f, (nfloat)width, 0.0f))
        {
            BackgroundColor = UIColor.White;
            using (var image = UIImage.FromBundle (imageName)){
                ImageView = new UIImageView (image);
            }
            AddSubview (ImageView);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            ImageView.Center = new CGPoint (ImageView.Frame.Width / 2.0f, Bounds.Height / 2.0f);
        }
    }
}

