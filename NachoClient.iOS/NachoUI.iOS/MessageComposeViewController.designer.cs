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
	[Register ("MessageComposeViewController")]
	partial class MessageComposeViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton attachButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIView contentView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton quckButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIScrollView scrollView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem sendButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIButton taskButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (attachButton != null) {
				attachButton.Dispose ();
				attachButton = null;
			}

			if (contentView != null) {
				contentView.Dispose ();
				contentView = null;
			}

			if (quckButton != null) {
				quckButton.Dispose ();
				quckButton = null;
			}

			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}

			if (sendButton != null) {
				sendButton.Dispose ();
				sendButton = null;
			}

			if (taskButton != null) {
				taskButton.Dispose ();
				taskButton = null;
			}
		}
	}
}
