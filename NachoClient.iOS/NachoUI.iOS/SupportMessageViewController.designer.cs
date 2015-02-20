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
	[Register ("SupportMessageViewController")]
	partial class SupportMessageViewController
	{
		[Outlet]
		UIKit.UIBarButtonItem cancelButton { get; set; }

		[Outlet]
		UIKit.UIView contentView { get; set; }

		[Outlet]
		UIKit.UINavigationBar navigationBar { get; set; }

		[Outlet]
		UIKit.UIScrollView scrollView { get; set; }

		[Outlet]
		UIKit.UIBarButtonItem sendButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (cancelButton != null) {
				cancelButton.Dispose ();
				cancelButton = null;
			}

			if (contentView != null) {
				contentView.Dispose ();
				contentView = null;
			}

			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}

			if (sendButton != null) {
				sendButton.Dispose ();
				sendButton = null;
			}

			if (navigationBar != null) {
				navigationBar.Dispose ();
				navigationBar = null;
			}
		}
	}
}
