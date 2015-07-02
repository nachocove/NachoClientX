// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace NachoClient.iOS
{
	[Register ("CalendarViewController")]
	partial class CalendarViewController
	{
		[Outlet]
		UIKit.UIView dateView { get; set; }

		[Outlet]
		UIKit.UIBarButtonItem monthViewButton { get; set; }

		[Outlet]
		UIKit.UIBarButtonItem newCalEventButton { get; set; }

		[Outlet]
		UIKit.UIBarButtonItem todayButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (dateView != null) {
				dateView.Dispose ();
				dateView = null;
			}

			if (monthViewButton != null) {
				monthViewButton.Dispose ();
				monthViewButton = null;
			}

			if (newCalEventButton != null) {
				newCalEventButton.Dispose ();
				newCalEventButton = null;
			}

			if (todayButton != null) {
				todayButton.Dispose ();
				todayButton = null;
			}
		}
	}
}
