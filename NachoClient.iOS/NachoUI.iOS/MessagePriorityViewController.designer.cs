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
		NachoClient.iOS.LabeledIconButton deadlineButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton deferDatePicker { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton dismissButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton laterButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton meetingButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton nextMonthButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton nextWeekButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton taskButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton tomorrowButton { get; set; }

		[Outlet]
		NachoClient.iOS.LabeledIconButton tonightButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (currentDelayLabel != null) {
				currentDelayLabel.Dispose ();
				currentDelayLabel = null;
			}

			if (deadlineButton != null) {
				deadlineButton.Dispose ();
				deadlineButton = null;
			}

			if (deferDatePicker != null) {
				deferDatePicker.Dispose ();
				deferDatePicker = null;
			}

			if (dismissButton != null) {
				dismissButton.Dispose ();
				dismissButton = null;
			}

			if (laterButton != null) {
				laterButton.Dispose ();
				laterButton = null;
			}

			if (meetingButton != null) {
				meetingButton.Dispose ();
				meetingButton = null;
			}

			if (nextMonthButton != null) {
				nextMonthButton.Dispose ();
				nextMonthButton = null;
			}

			if (nextWeekButton != null) {
				nextWeekButton.Dispose ();
				nextWeekButton = null;
			}

			if (taskButton != null) {
				taskButton.Dispose ();
				taskButton = null;
			}

			if (tomorrowButton != null) {
				tomorrowButton.Dispose ();
				tomorrowButton = null;
			}

			if (tonightButton != null) {
				tonightButton.Dispose ();
				tonightButton = null;
			}
		}
	}
}
