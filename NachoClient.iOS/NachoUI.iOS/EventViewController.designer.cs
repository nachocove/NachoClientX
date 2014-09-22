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
	[Register ("EventViewController")]
	partial class EventViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton acceptButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem cancelButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIView contentView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton declineButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem doneButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem editButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIDatePicker endDatePicker { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIScrollView scrollView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIDatePicker startDatePicker { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton tentativeButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (acceptButton != null) {
				acceptButton.Dispose ();
				acceptButton = null;
			}

			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}

			if (contentView != null) {
				contentView.Dispose ();
				contentView = null;
			}

			if (declineButton != null) {
				declineButton.Dispose ();
				declineButton = null;
			}

			if (doneButton != null) {
				doneButton.Dispose ();
				doneButton = null;
			}

			if (editButton != null) {
				editButton.Dispose ();
				editButton = null;
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

			if (tentativeButton != null) {
				tentativeButton.Dispose ();
				tentativeButton = null;
			}
		}
	}
}
