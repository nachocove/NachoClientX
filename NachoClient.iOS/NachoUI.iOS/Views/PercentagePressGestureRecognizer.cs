using System;
using UIKit;
using Foundation;
using CoreGraphics;
using CoreAnimation;

namespace NachoClient.iOS
{
    public class PercentagePressGestureRecognizer : UIGestureRecognizer
    {

        public double MinimumTime = 0.1;
        public double MaximumTime = 0.3;
        public UIOffset MinimumOffset = new UIOffset(0.0f, 5.0f);
        public UIOffset MaximumOffset = new UIOffset(0.0f, 44.0f);

        CGPoint StartingPoint;
        UIOffset Offset;
        double StartTime;
        double Time;
        bool PastMinimum;

        public double PercentComplete { get; private set; }

        CADisplayLink DisplayLink;

        public PercentagePressGestureRecognizer (Action action) : base(action)
        {
        }

        public override void TouchesBegan (Foundation.NSSet touches, UIEvent evt)
        {
            var touch = touches.AnyObject as UITouch;
            StartingPoint = touch.LocationInView (View);
            Offset = new UIOffset (0.0f, 0.0f);
            PercentComplete = 0.0f;
            StartTime = -1.0;
            Time = 0.0;
            PastMinimum = false;
            State = UIGestureRecognizerState.Began;
            DisplayLink = View.Window.Screen.CreateDisplayLink (DisplayFrame);
            DisplayLink.AddToRunLoop (NSRunLoop.Current, NSRunLoopMode.Default);
        }

        void DisplayFrame ()
        {
            if (StartTime < 0.0) {
                StartTime = DisplayLink.Timestamp;
            } else {
                Time = DisplayLink.Timestamp - StartTime;
                UpdatePercentage ();
            }
        }

        public override void TouchesMoved (Foundation.NSSet touches, UIEvent evt)
        {
            var touch = touches.AnyObject as UITouch;
            var point = touch.LocationInView (View);
            Offset.Horizontal = point.X - StartingPoint.X;
            Offset.Vertical = point.Y - StartingPoint.Y;
            UpdatePercentage ();
        }

        void UpdatePercentage ()
        {
            if (!PastMinimum) {
                if (MaximumTime > 0.0 && Time >= MinimumTime) {
                    PastMinimum = true;
                } else if (Offset.Horizontal >= MinimumOffset.Horizontal && Offset.Vertical >= MinimumOffset.Vertical) {
                    PastMinimum = true;
                }
            }
            if (PastMinimum){
                var timePercentage = Math.Min (1.0, MaximumTime != 0.0 ? Time / MaximumTime : 0.0);
                var horizontalPercentage = 1.0;
                var verticalPercentage = 1.0;
                if (MaximumOffset.Horizontal != 0.0) {
                    horizontalPercentage = Math.Max (0.0, Offset.Horizontal / MaximumOffset.Horizontal);
                }
                if (MaximumOffset.Vertical != 0.0) {
                    verticalPercentage = Math.Max (0.0, Offset.Vertical / MaximumOffset.Vertical);
                }
                var offsetPercentage = Math.Min (1.0, Math.Min (horizontalPercentage, verticalPercentage));
                PercentComplete = Math.Max(timePercentage, offsetPercentage);
                State = UIGestureRecognizerState.Changed;
                if (PercentComplete > 0.99){
                    PercentComplete = 1.0;
                    State = UIGestureRecognizerState.Recognized;
                }
            }
        }

        public override void Reset ()
        {
            base.Reset ();
            DisplayLink.Invalidate ();
            DisplayLink = null;
        }

        public override void TouchesEnded (Foundation.NSSet touches, UIEvent evt)
        {
            if (!PastMinimum) {
                StartTime += MinimumTime;
            }
        }

        public override void TouchesCancelled (Foundation.NSSet touches, UIEvent evt)
        {
            State = UIGestureRecognizerState.Cancelled;
        }

    }
}

