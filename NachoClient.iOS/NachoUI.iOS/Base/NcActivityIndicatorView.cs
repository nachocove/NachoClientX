//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using Foundation;

namespace NachoClient.iOS
{
    [Register ("NcActivityIndicatorView")]
    public class NcActivityIndicatorView : UIView
    {

        private CALayer StripImageLayer;
        private CAReplicatorLayer StripImageReplicatorLayer;
        private CALayer MaskLayer;
        private nfloat IndicatorSize;
        private bool _IsAnimating;
        private CABasicAnimation StripAnimation = null;
        private CGSize RawIndicatorSize;
        private nfloat CompleteCycleHeight;
        private nfloat StripHeight;
        private nfloat? OriginalOffset;
        private nfloat _Speed = 1.0f;
        public nfloat Speed {
            get {
                return _Speed;
            }
            set {
                _Speed = value;
                if (IsAnimating) {
                    StopAnimating ();
                    StartAnimating ();
                }
            }
        }

        private const string STRIP_ANIMATION_KEY = "strip";

        public bool IsAnimating {
            get {
                return _IsAnimating;
            }
        }


        public NcActivityIndicatorView (IntPtr handle) : base (handle)
        {
            SetupIndicator ();
        }

        public NcActivityIndicatorView (CGRect frame) : base (frame)
        {
            SetupIndicator ();
        }

        private void SetupIndicator ()
        {
            _IsAnimating = false;
            UIImage stripImage = UIImage.FromBundle ("NachoActivityIndicatorStrip");
            RawIndicatorSize = stripImage.Size;
            CompleteCycleHeight = RawIndicatorSize.Height;
            Layer.MasksToBounds = true;
            StripImageLayer = new CALayer ();
            StripImageLayer.Frame = new CGRect (0.0f, 0.0f, RawIndicatorSize.Width, RawIndicatorSize.Height);
            StripImageLayer.Contents = stripImage.CGImage;
            StripImageLayer.ContentsScale = stripImage.CurrentScale;
            StripImageLayer.Opaque = true;
            StripImageReplicatorLayer = new CAReplicatorLayer ();
            StripImageReplicatorLayer.InstanceCount = 3;
            StripHeight = StripImageLayer.Frame.Size.Height * StripImageReplicatorLayer.InstanceCount;
            StripImageReplicatorLayer.Frame = new CGRect (0.0f, 0.0f, RawIndicatorSize.Width, StripHeight);
            StripImageReplicatorLayer.InstanceTransform = CATransform3D.MakeTranslation (0.0f, StripImageLayer.Frame.Size.Height, 0.0f);
            StripImageReplicatorLayer.AddSublayer (StripImageLayer);
            StripImageReplicatorLayer.Opaque = true;
            MaskLayer = new CALayer ();
            MaskLayer.Bounds = new CGRect (0.0f, StripHeight - RawIndicatorSize.Width, RawIndicatorSize.Width, RawIndicatorSize.Width);
            MaskLayer.Position = new CGPoint (Layer.Bounds.Width / 2.0f, Layer.Bounds.Height / 2.0f);
            MaskLayer.MasksToBounds = true;
            MaskLayer.CornerRadius = MaskLayer.Frame.Width / 2.0f;
            MaskLayer.AddSublayer (StripImageReplicatorLayer);
            Layer.AddSublayer (MaskLayer);
            ResizeIndicator ();
        }

        public void StartAnimating ()
        {
            if (!_IsAnimating) {
                if (StripAnimation == null) {
                    SetupAnimation ();
                }
                StripAnimation.Duration = 2.0 / _Speed;
                StripAnimation.From = new NSNumber (MaskLayer.Bounds.Y);
                StripAnimation.To = new NSNumber (MaskLayer.Bounds.Y - CompleteCycleHeight);
                _IsAnimating = true;
                MaskLayer.AddAnimation (StripAnimation, STRIP_ANIMATION_KEY);
            }
        }

        public void StopAnimating ()
        {
            if (_IsAnimating) {
                _IsAnimating = false;
                nfloat y = MaskLayer.Bounds.Y;
                if (MaskLayer.PresentationLayer != null) {
                    // We should always want the value from the presentation layer because that's what's currently being displayed
                    // however, if Stop is called immedately after Start, the presentation layer will still be null, so we need a safety check
                    y = MaskLayer.PresentationLayer.Bounds.Y;
                }
                MaskLayer.RemoveAnimation (STRIP_ANIMATION_KEY);
                CATransaction.Begin ();
                CATransaction.DisableActions = true;
                if (y < StripHeight - CompleteCycleHeight - MaskLayer.Bounds.Height) {
                    y += CompleteCycleHeight;
                }
                MaskLayer.Bounds = new CGRect (0.0f, y, MaskLayer.Bounds.Width, MaskLayer.Bounds.Height);
                CATransaction.Commit ();
            }
        }

        public void SetOffset (nfloat offset)
        {
            if (!_IsAnimating) {
                if (!OriginalOffset.HasValue) {
                    OriginalOffset = MaskLayer.Bounds.Y;
                }
                nfloat scale = IndicatorSize / RawIndicatorSize.Width;
                offset = offset / scale;
                CATransaction.Begin ();
                CATransaction.DisableActions = true;
                nfloat y = OriginalOffset.Value - offset % CompleteCycleHeight;
                MaskLayer.Bounds = new CGRect (0.0f, y, MaskLayer.Bounds.Width, MaskLayer.Bounds.Height);
                CATransaction.Commit ();
            }
        }

        public void ClearOffset ()
        {
            if (OriginalOffset.HasValue) {
                var offset = OriginalOffset.Value;
                OriginalOffset = null;
                if (!IsAnimating) {
                    CATransaction.Begin ();
                    CATransaction.DisableActions = true;
                    MaskLayer.Bounds = new CGRect (0.0f, offset, MaskLayer.Bounds.Width, MaskLayer.Bounds.Height);
                    CATransaction.Commit ();
                }
            }
        }

        public void ResetOffset ()
        {
            CATransaction.Begin ();
            CATransaction.DisableActions = true;
            MaskLayer.Bounds = new CGRect (0.0f, StripHeight - RawIndicatorSize.Width, MaskLayer.Bounds.Width, MaskLayer.Bounds.Height);
            CATransaction.Commit ();
        }

        #region Layout

        public override void LayoutSubviews ()
        {
            ResizeIndicator ();
        }

        private void ResizeIndicator ()
        {
            var keys = Layer.AnimationKeys;
            bool isPartOfExternalAnimation = false;
            CATransaction.Begin ();
            if (keys != null) {
                foreach (var key in keys) {
                    if (key != STRIP_ANIMATION_KEY) {
                        isPartOfExternalAnimation = true;
                        var animation = Layer.AnimationForKey (key);
                        CATransaction.AnimationDuration = animation.Duration;
                        CATransaction.AnimationTimingFunction = animation.TimingFunction;
                        break;
                    }
                }
            }
            if (!isPartOfExternalAnimation) {
                CATransaction.DisableActions = true;
            }
            IndicatorSize = (nfloat)Math.Min ((double)Frame.Width, (double)Frame.Height);
            nfloat scale = IndicatorSize / RawIndicatorSize.Width;
            MaskLayer.AffineTransform = CGAffineTransform.MakeScale (scale, scale);
            MaskLayer.Position = new CGPoint (Layer.Bounds.Width / 2.0f, Layer.Bounds.Height / 2.0f);
            CATransaction.Commit ();
        }

        private void SetupAnimation ()
        {
            StripAnimation = CABasicAnimation.FromKeyPath ("bounds.origin.y");
            StripAnimation.RepeatCount = Single.PositiveInfinity;
        }

        #endregion

    }
}

