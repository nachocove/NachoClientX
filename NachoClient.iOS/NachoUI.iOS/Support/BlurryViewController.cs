//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using Foundation;
using UIKit;
using UIImageEffectsBinding;

namespace NachoClient.iOS
{
    public class BlurryViewController : UIViewController
    {
        protected UIImage backgroundImage;
        protected UIView backgroundOverlayView;
        protected UIImageView backgroundBlurredImageView;

        public BlurryViewController () : base ()
        {
        }

        public BlurryViewController (IntPtr handle) : base (handle)
        {
        }

        public void CaptureView (UIView view)
        {
            UIImage clonedImage = null;

            try {
                UIGraphics.BeginImageContextWithOptions (view.Bounds.Size, false, 0.0f);
                view.Layer.RenderInContext (UIGraphics.GetCurrentContext ());
                clonedImage = UIGraphics.GetImageFromCurrentImageContext ();
                UIGraphics.EndImageContext ();
                backgroundImage = clonedImage.ApplySubtleEffect ();
            } finally {
                clonedImage.Dispose ();
            }
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            backgroundBlurredImageView = new UIImageView (new CGRect (0.0f, 0.0f, 0.0f, 0.0f));
            backgroundBlurredImageView.ContentMode = UIViewContentMode.Center;
            backgroundBlurredImageView.ClipsToBounds = true;
            backgroundBlurredImageView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            View.AddSubview (backgroundBlurredImageView);
            View.SendSubviewToBack (backgroundBlurredImageView);

            backgroundOverlayView = new UIView (new CGRect (0.0f, 0.0f, 0.0f, 0.0f));
            backgroundOverlayView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            backgroundOverlayView.ClipsToBounds = true;
            backgroundBlurredImageView.AddSubview (backgroundOverlayView);

            if (null != backgroundImage) {
                backgroundBlurredImageView.Image = backgroundImage;
                backgroundImage.Dispose ();
                backgroundImage = null;
            }
            backgroundOverlayView.Alpha = 0.7f;
            backgroundOverlayView.BackgroundColor = UIColor.Black;
        }
        // E.g. resizes after rotation
        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            backgroundBlurredImageView.Frame = View.Bounds;
            backgroundOverlayView.Frame = View.Bounds;
        }

        // Show light colored status bar over our dark background
        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }
    }
}

