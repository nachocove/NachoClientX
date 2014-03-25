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
		MonoTouch.UIKit.UIBarButtonItem nachoButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (nachoButton != null) {
				nachoButton.Dispose ();
				nachoButton = null;
			}
		}
	}
}
