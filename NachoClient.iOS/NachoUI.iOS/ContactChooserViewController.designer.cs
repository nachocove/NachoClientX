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
	[Register ("ContactChooserViewController")]
	partial class ContactChooserViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField AutocompleteTextField { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem ContactsButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView TableView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton ToButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (AutocompleteTextField != null) {
				AutocompleteTextField.Dispose ();
				AutocompleteTextField = null;
			}

			if (ContactsButton != null) {
				ContactsButton.Dispose ();
				ContactsButton = null;
			}

			if (TableView != null) {
				TableView.Dispose ();
				TableView = null;
			}

			if (ToButton != null) {
				ToButton.Dispose ();
				ToButton = null;
			}
		}
	}
}
