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
	[Register ("NachoNowViewController")]
	partial class NachoNowViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIImageView calendarCloseView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView calendarTableView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIImageView calendarThumbView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIView calendarView { get; set; }

		[Outlet]
		iCarouselBinding.iCarousel carouselView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem composeButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem newMeetingButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (calendarCloseView != null) {
				calendarCloseView.Dispose ();
				calendarCloseView = null;
			}

			if (calendarTableView != null) {
				calendarTableView.Dispose ();
				calendarTableView = null;
			}

			if (calendarThumbView != null) {
				calendarThumbView.Dispose ();
				calendarThumbView = null;
			}

			if (calendarView != null) {
				calendarView.Dispose ();
				calendarView = null;
			}

			if (carouselView != null) {
				carouselView.Dispose ();
				carouselView = null;
			}

			if (composeButton != null) {
				composeButton.Dispose ();
				composeButton = null;
			}

			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}

			if (newMeetingButton != null) {
				newMeetingButton.Dispose ();
				newMeetingButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}
		}
	}
}
