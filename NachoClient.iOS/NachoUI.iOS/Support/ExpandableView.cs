//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;
using MonoTouch.Foundation;

using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class ExpandableView : UIView
    {
        const float EXPAND_INSET = 5.0f;
        public const float EXPAND_BUTTON_WIDTH = 30.0f;
        public const float EXPAND_BUTTON_HEIGHT = 20.0f;

        private bool isExpanded;
        private UIView gestureView;
        protected UITapGestureRecognizer singleTapGesture;
        protected UITapGestureRecognizer.Token singleTapGestureHandlerToken;
        private UIImageView buttonImageView;

        private float _CollapsedHeight;

        public float CollapsedHeight {
            get {
                return _CollapsedHeight;
            }
            set {
                _CollapsedHeight = value;
                ViewFramer.Create (gestureView).Height (value);
                if (!isExpanded) {
                    Layout ();
                }
            }
        }

        private float _ExpandedHeight;

        public float ExpandedHeight {
            get {
                return _ExpandedHeight;
            }
            set {
                _ExpandedHeight = value;
                if (isExpanded) {
                    Layout ();
                }
            }
        }

        public ExpandButton.StateChangedCallback OnStateChanged;

        public ExpandableView (RectangleF initialFrame, bool isExpanded) : base (initialFrame)
        {
            CheckFrameSize (initialFrame);

            ClipsToBounds = true;
            BackgroundColor = UIColor.Clear;
            UserInteractionEnabled = true;

            gestureView = new UIView (new RectangleF(0, 0, initialFrame.Width, initialFrame.Height));
            gestureView.BackgroundColor = UIColor.Clear;
            gestureView.UserInteractionEnabled = true;
            this.AddSubview (gestureView);

            // A single tap on the header section toggles between the compact and expanded
            // views of the header.
            singleTapGesture = new UITapGestureRecognizer ();
            singleTapGesture.NumberOfTapsRequired = 1;
            singleTapGestureHandlerToken = singleTapGesture.AddTarget (HeaderSingleTapHandler);
            singleTapGesture.ShouldRecognizeSimultaneously = SingleTapGestureRecognizer;
            gestureView.AddGestureRecognizer (singleTapGesture);

            var buttonX = initialFrame.Width - EXPAND_INSET - EXPAND_BUTTON_WIDTH;
            var buttonY = (initialFrame.Height / 2) - (EXPAND_BUTTON_HEIGHT / 2);
            buttonImageView = new UIImageView (new RectangleF (buttonX, buttonY, EXPAND_BUTTON_WIDTH, EXPAND_BUTTON_HEIGHT));
            this.AddSubview (buttonImageView);
            UpdateImage ();
        }

        void CheckFrameSize (RectangleF frame)
        {
            NcAssert.True ((ExpandButton.WIDTH + EXPAND_INSET) < frame.Width);
            NcAssert.True ((ExpandButton.HEIGHT + EXPAND_INSET) < frame.Height);
        }

        void Layout ()
        {
            UIView.Animate (0.2, 0, UIViewAnimationOptions.CurveLinear, () => {
                UpdateImage();
                ViewFramer.Create (this).Height (isExpanded ? ExpandedHeight : CollapsedHeight);
            }, () => {
            });
        }

        protected void Cleanup ()
        {
            // Clean up gesture recognizers.
            singleTapGesture.RemoveTarget (singleTapGestureHandlerToken);
            singleTapGesture.ShouldRecognizeSimultaneously = null;
            gestureView.RemoveGestureRecognizer (singleTapGesture);
        }

        private void HeaderSingleTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                // PointF touch = gesture.LocationInView (gestureView);
                isExpanded = !isExpanded;
                Layout ();
                if (null != OnStateChanged) {
                    OnStateChanged (isExpanded);
                }
            }
        }

        private bool SingleTapGestureRecognizer (UIGestureRecognizer a, UIGestureRecognizer b)
        {
            return true;
        }

        private void UpdateImage ()
        {
            var imageName = (isExpanded ? "gen-readmore-active" : "gen-readmore");
            using (var image = UIImage.FromBundle (imageName)) {
                buttonImageView.Image = image;
            }
        }
    }
}

