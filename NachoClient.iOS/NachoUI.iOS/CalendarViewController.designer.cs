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
	[Register ("CalendarViewController")]
	partial class CalendarViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITableView calendarTableView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIView dateView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem monthViewButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem newCalEventButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem todayButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (todayButton != null) {
				todayButton.Dispose ();
				todayButton = null;
			}

			if (calendarTableView != null) {
				calendarTableView.Dispose ();
				calendarTableView = null;
			}

			if (dateView != null) {
				dateView.Dispose ();
				dateView = null;
			}

			if (newCalEventButton != null) {
				newCalEventButton.Dispose ();
				newCalEventButton = null;
			}

			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (monthViewButton != null) {
				monthViewButton.Dispose ();
				monthViewButton = null;
			}
		}
	}
}
