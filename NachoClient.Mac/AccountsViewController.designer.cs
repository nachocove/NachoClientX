// WARNING
//
// This file has been generated automatically by Xamarin Studio Business to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace NachoClient.Mac
{
	[Register ("AccountsViewController")]
	partial class AccountsViewController
	{
		[Outlet]
		AppKit.NSButton AddButton { get; set; }

		[Outlet]
		AppKit.NSButton RemoveButton { get; set; }

		[Outlet]
		AppKit.NSTableView TableView { get; set; }

		[Action ("AddAccount:")]
		partial void AddAccount (Foundation.NSObject sender);

		[Action ("RemoveAccount:")]
		partial void RemoveAccount (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (TableView != null) {
				TableView.Dispose ();
				TableView = null;
			}

			if (AddButton != null) {
				AddButton.Dispose ();
				AddButton = null;
			}

			if (RemoveButton != null) {
				RemoveButton.Dispose ();
				RemoveButton = null;
			}
		}
	}
}
