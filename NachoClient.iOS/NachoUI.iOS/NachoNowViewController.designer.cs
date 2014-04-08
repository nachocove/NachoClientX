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
		MonoTouch.UIKit.UIBarButtonItem calendarNowButton { get; set; }

		[Outlet]
		iCarouselBinding.iCarousel carouselView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem composeButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem contactButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIView currentEventView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem emailNowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem tasksNowButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (calendarNowButton != null) {
				calendarNowButton.Dispose ();
				calendarNowButton = null;
			}

			if (carouselView != null) {
				carouselView.Dispose ();
				carouselView = null;
			}

			if (contactButton != null) {
				contactButton.Dispose ();
				contactButton = null;
			}

			if (currentEventView != null) {
				currentEventView.Dispose ();
				currentEventView = null;
			}

			if (emailNowButton != null) {
				emailNowButton.Dispose ();
				emailNowButton = null;
			}

			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (tasksNowButton != null) {
				tasksNowButton.Dispose ();
				tasksNowButton = null;
			}

			if (composeButton != null) {
				composeButton.Dispose ();
				composeButton = null;
			}
		}
	}
}
