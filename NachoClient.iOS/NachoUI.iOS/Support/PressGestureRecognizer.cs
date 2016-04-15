using System;
using UIKit;

namespace NachoClient.iOS
{
	public class PressGestureRecognizer : UIGestureRecognizer
	{

		public bool IsInsideView { get; private set; }

		public PressGestureRecognizer (Action action) : base(action)
		{
			IsInsideView = false;
		}

		public override void TouchesBegan (Foundation.NSSet touches, UIEvent evt)
		{
			State = UIGestureRecognizerState.Began;
		}

		public override void TouchesMoved (Foundation.NSSet touches, UIEvent evt)
		{
			var touch = touches.AnyObject as UITouch;
			UpdateIsInsideView (touch);
			State = UIGestureRecognizerState.Changed;
		}

		public override void TouchesEnded (Foundation.NSSet touches, UIEvent evt)
		{
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
			State = UIGestureRecognizerState.Cancelled;
		}

		void UpdateIsInsideView (UITouch touch)
		{
			var location = touch.LocationInView (View);
			IsInsideView = location.X >= View.Bounds.X && location.X < View.Bounds.X + View.Bounds.Width && location.Y >= View.Bounds.Y && location.Y < View.Bounds.Y + View.Bounds.Height;
		}
	}
}

