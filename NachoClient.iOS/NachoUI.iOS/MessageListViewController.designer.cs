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
	[Register ("MessageListViewController")]
	partial class MessageListViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem cancelButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem composeButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem deleteButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem saveButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem searchButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}

			if (composeButton != null) {
				composeButton.Dispose ();
				composeButton = null;
			}

			if (deleteButton != null) {
				deleteButton.Dispose ();
				deleteButton = null;
			}

			if (saveButton != null) {
				saveButton.Dispose ();
				saveButton = null;
			}

			if (searchButton != null) {
				searchButton.Dispose ();
				searchButton = null;
			}
		}
	}
}
