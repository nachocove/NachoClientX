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
	[Register ("DatePickerViewController")]
	partial class DatePickerViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton cancelButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIDatePicker datePicker { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton OKButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}

			if (datePicker != null) {
				datePicker.Dispose ();
				datePicker = null;
			}

			if (OKButton != null) {
				OKButton.Dispose ();
				OKButton = null;
			}
		}
	}
}
