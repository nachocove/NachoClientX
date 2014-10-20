//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    // A BodyView consists of a list of render views arranged vertically.
    // Each type of body render view must implement this interface
    public interface IBodyRender
    {
        SizeF ContentSize { get; }

        void ScrollTo (PointF upperLeftCorner);

        string LayoutInfo ();
    }

    public class BodyRenderZoomRecognizer : UITapGestureRecognizer
    {
        private const float ZOOM_OUT_MARGIN = 3.0f;

        private UIScrollView scrollView;

        private bool zoomIn;

        public float ZoomInScale { get; protected set; }

        public float ZoomOutScale { get; protected set; }

        public Action OnTap;

        public BodyRenderZoomRecognizer (UIScrollView view) : base ()
        {
            zoomIn = true;
            scrollView = view;
            NumberOfTapsRequired = 2;
            NumberOfTouchesRequired = 1;
            ShouldRecognizeSimultaneously =
                (UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer) => {
                return true;
            };
            AddTarget (onTapped);
        }

        public void Configure ()
        {
            // Try to set min zoom scale to the value that will fit the content to
            // the width of the screen. But set a lower limit so that we don't shrink
            // content to such small scale that nothing is readable anymore.
            ZoomInScale = scrollView.Frame.Width / (scrollView.ContentSize.Width + (2 * ZOOM_OUT_MARGIN));
            // Min zoom scale must be between 0.7 and 1.0
            ZoomInScale = Math.Min (1.0f, ZoomInScale);
            ZoomInScale = Math.Max (0.7f, ZoomInScale);

            ZoomOutScale = 2.0f * ZoomInScale;
        }

        private void onTapped ()
        {
            if (null == scrollView) {
                return;
            }
            scrollView.ContentOffset = new PointF ();
            if (zoomIn) {
                UIView.Animate (0.1f, 0.0f, UIViewAnimationOptions.CurveLinear, () => {
                    scrollView.SetZoomScale (ZoomOutScale, false);
                }, () => {
                });
            } else {
                UIView.Animate (0.1f, 0.0f, UIViewAnimationOptions.CurveLinear, () => {
                    scrollView.SetZoomScale (ZoomInScale, false);
                }, () => {
                });
            }
            zoomIn = !zoomIn;
            if (null != OnTap) {
                OnTap ();
            }
        }
    }
}

