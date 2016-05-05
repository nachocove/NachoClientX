using System;
using UIKit;
using Foundation;

namespace NachoClient.iOS
{
    public class PressGestureRecognizer : UIGestureRecognizer
    {

        public bool IsInsideView { get; private set; }
        public bool IsCanceledByPanning = false;
        public bool DelaysStart = false;

        int DelayInMilliseconds = 150;

        NSTimer StartDelayTimer;

        public PressGestureRecognizer (Action action) : base(action)
        {
            IsInsideView = false;
        }

        public override void TouchesBegan (Foundation.NSSet touches, UIEvent evt)
        {
            if (DelaysStart) {
                StartDelayTimer = NSTimer.CreateScheduledTimer (TimeSpan.FromMilliseconds (DelayInMilliseconds), StartTimerFired);
                NSRunLoop.Current.AddTimer (StartDelayTimer, NSRunLoopMode.UITracking);
                State = UIGestureRecognizerState.Possible;
            }else{
                State = UIGestureRecognizerState.Began;
            }
        }

        void StartTimerFired (NSTimer timer)
        {
            StartDelayTimer = null;
            State = UIGestureRecognizerState.Began;
        }

        void ClearStartTimer ()
        {
            if (StartDelayTimer != null) {
                StartDelayTimer.Invalidate ();
                StartDelayTimer = null;
            }
        }

        public override void TouchesMoved (Foundation.NSSet touches, UIEvent evt)
        {
            if (StartDelayTimer == null) {
                var touch = touches.AnyObject as UITouch;
                UpdateIsInsideView (touch);
                State = UIGestureRecognizerState.Changed;
            }
        }

        public override void TouchesEnded (Foundation.NSSet touches, UIEvent evt)
        {
            ClearStartTimer ();
            var touch = touches.AnyObject as UITouch;
            UpdateIsInsideView (touch);
            if (IsInsideView) {
                State = UIGestureRecognizerState.Ended;
                IsInsideView = false;
            } else {
                State = UIGestureRecognizerState.Failed;
            }
        }

        public override void TouchesCancelled (Foundation.NSSet touches, UIEvent evt)
        {
            ClearStartTimer ();
            State = UIGestureRecognizerState.Cancelled;
        }

        void UpdateIsInsideView (UITouch touch)
        {
            var location = touch.LocationInView (View);
            IsInsideView = location.X >= View.Bounds.X && location.X < View.Bounds.X + View.Bounds.Width && location.Y >= View.Bounds.Y && location.Y < View.Bounds.Y + View.Bounds.Height;
        }

        public override bool CanPreventGestureRecognizer (UIGestureRecognizer preventedGestureRecognizer)
        {
            // There are two reasons for overriding this method:
            // 1. So pan gestures are not prevented (they are by the base implementation)
            // 2. This will be called when pan gesture changes to Began state, giving us an opportunity
            //    to cancel our own gesture, if we've already moved to Began state.
            var canPrevent = base.CanPreventGestureRecognizer (preventedGestureRecognizer);
            if (IsCanceledByPanning && preventedGestureRecognizer is UIPanGestureRecognizer) {
                canPrevent = false;
                if (preventedGestureRecognizer.State == UIGestureRecognizerState.Began) {
                    ClearStartTimer ();
                    Enabled = false;
                    Enabled = true;
                }
            }
            return canPrevent;
        }

        public override bool CanBePreventedByGestureRecognizer (UIGestureRecognizer preventingGestureRecognizer)
        {
            // This method is overridden for the case where DelaysStart is true, and a pan gesture begins before
            // our start timer fires.  In such a case, iOS calls this method instead of CanPreventGestureRecognizer,
            // because we're not yet in the Began state.
            var canBePrevented = base.CanBePreventedByGestureRecognizer (preventingGestureRecognizer);
            if (IsCanceledByPanning && preventingGestureRecognizer is UIPanGestureRecognizer) {
                if (preventingGestureRecognizer.State == UIGestureRecognizerState.Began) {
                    State = UIGestureRecognizerState.Failed;
                    ClearStartTimer ();
                    canBePrevented = true;
                }
            }
            return canBePrevented;
        }
    }
}

