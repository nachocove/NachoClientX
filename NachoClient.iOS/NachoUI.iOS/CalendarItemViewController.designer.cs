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
		MonoTouch.UIKit.UIBarButtonItem cancelButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem doneButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem editButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (editButton != null) {
				editButton.Dispose ();
				editButton = null;
			}

			if (doneButton != null) {
				doneButton.Dispose ();
				doneButton = null;
			}

			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}
		}
	}
}
