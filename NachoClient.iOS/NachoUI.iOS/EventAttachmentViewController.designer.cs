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
	[Register ("EventAttachmentViewController")]
	partial class EventAttachmentViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem addAttachmentButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView EventAttachmentsTableView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (addAttachmentButton != null) {
				addAttachmentButton.Dispose ();
				addAttachmentButton = null;
			}

			if (EventAttachmentsTableView != null) {
				EventAttachmentsTableView.Dispose ();
				EventAttachmentsTableView = null;
			}
		}
	}
}
