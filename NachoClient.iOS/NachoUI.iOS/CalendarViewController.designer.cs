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
		MonoTouch.UIKit.UIBarButtonItem addButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem searchButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem switcherButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (addButton != null) {
				addButton.Dispose ();
				addButton = null;
			}

			if (searchButton != null) {
				searchButton.Dispose ();
				searchButton = null;
			}

			if (switcherButton != null) {
				switcherButton.Dispose ();
				switcherButton = null;
			}
		}
	}
}
