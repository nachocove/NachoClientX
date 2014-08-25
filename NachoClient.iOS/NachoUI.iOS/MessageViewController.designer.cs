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
	[Register ("MessageViewController")]
	partial class MessageViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem archiveButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem deleteButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem fixedSpaceButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem flexibleSpaceButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem forwardButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem quickReplyButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem replyAllButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem replyButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem saveButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIScrollView scrollView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (archiveButton != null) {
				archiveButton.Dispose ();
				archiveButton = null;
			}

			if (deleteButton != null) {
				deleteButton.Dispose ();
				deleteButton = null;
			}

			if (fixedSpaceButton != null) {
				fixedSpaceButton.Dispose ();
				fixedSpaceButton = null;
			}

			if (flexibleSpaceButton != null) {
				flexibleSpaceButton.Dispose ();
				flexibleSpaceButton = null;
			}

			if (forwardButton != null) {
				forwardButton.Dispose ();
				forwardButton = null;
			}

			if (quickReplyButton != null) {
				quickReplyButton.Dispose ();
				quickReplyButton = null;
			}

			if (replyAllButton != null) {
				replyAllButton.Dispose ();
				replyAllButton = null;
			}

			if (replyButton != null) {
				replyButton.Dispose ();
				replyButton = null;
			}

			if (saveButton != null) {
				saveButton.Dispose ();
				saveButton = null;
			}

			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}
		}
	}
}
