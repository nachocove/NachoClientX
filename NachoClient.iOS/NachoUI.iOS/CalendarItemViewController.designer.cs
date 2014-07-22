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
	[Register ("CalendarItemViewController")]
	partial class CalendarItemViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton acceptButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem cancelButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton declineButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem doneButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem editButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton tenativeButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}

			if (acceptButton != null) {
				acceptButton.Dispose ();
				acceptButton = null;
			}

			if (declineButton != null) {
				declineButton.Dispose ();
				declineButton = null;
			}

			if (tenativeButton != null) {
				tenativeButton.Dispose ();
				tenativeButton = null;
			}

			if (doneButton != null) {
				doneButton.Dispose ();
				doneButton = null;
			}

			if (editButton != null) {
				editButton.Dispose ();
				editButton = null;
			}

			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}
		}
	}
}
