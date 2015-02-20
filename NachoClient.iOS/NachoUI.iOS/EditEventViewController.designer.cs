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
	[Register ("EditEventViewController")]
	partial class EditEventViewController
	{
		[Outlet]
		UIKit.UIView contentView { get; set; }

		[Outlet]
		UIKit.UIDatePicker endDatePicker { get; set; }

		[Outlet]
		UIKit.UIScrollView scrollView { get; set; }

		[Outlet]
		UIKit.UIDatePicker startDatePicker { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (contentView != null) {
				contentView.Dispose ();
				contentView = null;
			}

			if (endDatePicker != null) {
				endDatePicker.Dispose ();
				endDatePicker = null;
			}

			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}

			if (startDatePicker != null) {
				startDatePicker.Dispose ();
				startDatePicker = null;
			}
		}
	}
}
