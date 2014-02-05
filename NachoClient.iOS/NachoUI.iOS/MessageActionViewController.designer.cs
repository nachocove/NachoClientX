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
	[Register ("MessageActionViewController")]
	partial class MessageActionViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton dismissButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView folderTableView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (folderTableView != null) {
				folderTableView.Dispose ();
				folderTableView = null;
			}

			if (dismissButton != null) {
				dismissButton.Dispose ();
				dismissButton = null;
			}
		}
	}
}
