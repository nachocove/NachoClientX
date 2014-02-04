// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;
using System.CodeDom.Compiler;

namespace NachoClient.iOS
{
	[Register ("MessagePriorityViewController")]
	partial class MessagePriorityViewController
	{
		[Outlet]
		MonoTouch.UIKit.UILabel currentDelayLabel { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton customDateButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton dismissButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton foreverButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton laterButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton monthEndButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton nextMonthButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton nextWeekButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton nowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton scheduleMeetingButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton tomorrowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton tonightButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (currentDelayLabel != null) {
				currentDelayLabel.Dispose ();
				currentDelayLabel = null;
			}

			if (customDateButton != null) {
				customDateButton.Dispose ();
				customDateButton = null;
			}

			if (dismissButton != null) {
				dismissButton.Dispose ();
				dismissButton = null;
			}

			if (foreverButton != null) {
				foreverButton.Dispose ();
				foreverButton = null;
			}

			if (laterButton != null) {
				laterButton.Dispose ();
				laterButton = null;
			}

			if (monthEndButton != null) {
				monthEndButton.Dispose ();
				monthEndButton = null;
			}

			if (nextMonthButton != null) {
				nextMonthButton.Dispose ();
				nextMonthButton = null;
			}

			if (nextWeekButton != null) {
				nextWeekButton.Dispose ();
				nextWeekButton = null;
			}

			if (scheduleMeetingButton != null) {
				scheduleMeetingButton.Dispose ();
				scheduleMeetingButton = null;
			}

			if (tomorrowButton != null) {
				tomorrowButton.Dispose ();
				tomorrowButton = null;
			}

			if (tonightButton != null) {
				tonightButton.Dispose ();
				tonightButton = null;
			}

			if (nowButton != null) {
				nowButton.Dispose ();
				nowButton = null;
			}
		}
	}
}
