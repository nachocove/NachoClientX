//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using Foundation;

namespace NachoClient.iOS
{
    public class NcActivityIndicatorView : UIView
    {

        private CALayer stripImageLayer;
        private CALayer maskLayer;
        private nfloat indicatorSize;
        private bool isAnimating;
        private CABasicAnimation stripAnimation = null;

        private const string STRIP_ANIMATION_KEY = "strip";

        public bool IsAnimating {
            get {
                return isAnimating;
            }
        }

        public NcActivityIndicatorView (CGRect frame) : base(frame)
        {
            UIImage stripImage = UIImage.FromBundle ("NachoActivityIndicatorStrip");
            stripImageLayer = new CALayer();
            stripImageLayer.ContentsScale = stripImage.CurrentScale;
            stripImageLayer.Contents = stripImage.CGImage;
            stripImageLayer.Frame = new CGRect (0, 0, stripImage.Size.Width, stripImage.Size.Height);
            stripImageLayer.AnchorPoint = new CGPoint (0.5, 1.0);
            stripImageLayer.Opaque = true;
            maskLayer = new CALayer ();
            maskLayer.Frame = frame;
            maskLayer.MasksToBounds = true;
            stripImageLayer.Position = new CGPoint (0, frame.Size.Height);
            maskLayer.AddSublayer (stripImageLayer);
            Layer.AddSublayer (maskLayer);
            ResizeIndicator ();
        }

        private void ResizeIndicator ()
        {
            // Known issue: this resize code probably doesn't work while the animation is running.
            // Ideally, any fix would continue animation smoothly as the size changes.
            indicatorSize = (nfloat)Math.Min ((double)Frame.Width, (double)Frame.Height);
            maskLayer.Frame = new CGRect ((Frame.Width - indicatorSize) / 2.0, (Frame.Height - indicatorSize) / 2.0, indicatorSize, indicatorSize);
            maskLayer.CornerRadius = indicatorSize / 2.0f;
            stripImageLayer.Position = new CGPoint (maskLayer.Bounds.Width / 2.0, stripImageLayer.Position.Y);
            nfloat scale = indicatorSize / stripImageLayer.Bounds.Width;
            stripImageLayer.Transform = CATransform3D.MakeScale (scale, scale, 1.0f);
        }

        private void SetupAnimation ()
        {
            stripAnimation = CABasicAnimation.FromKeyPath ("position");
            stripAnimation.Duration = 2.0;
            stripAnimation.From = NSValue.FromCGPoint (stripImageLayer.Position);
            stripAnimation.To = NSValue.FromCGPoint (new CGPoint (stripImageLayer.Position.X, stripImageLayer.Position.Y + stripImageLayer.Frame.Height - maskLayer.Frame.Height));
            stripAnimation.RepeatCount = Single.PositiveInfinity;
        }

        public void StartAnimating ()
        {
            if (!isAnimating) {
                if (stripAnimation == null) {
                    SetupAnimation ();
                }
                isAnimating = true;
                stripImageLayer.AddAnimation (stripAnimation, STRIP_ANIMATION_KEY);
            }
        }

        public void StopAnimating ()
        {
            if (isAnimating) {
                isAnimating = false;
                stripImageLayer.RemoveAnimation (STRIP_ANIMATION_KEY);
            }
        }

    }
}

