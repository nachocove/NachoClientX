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
	[Register ("AccountSyncingViewController")]
	partial class AccountSyncingViewController
	{
		[Outlet]
		NachoClient.iOS.NcActivityIndicatorView activityIndicatorView { get; set; }

		[Outlet]
		UIKit.UIBarButtonItem skipButton { get; set; }

		[Outlet]
		UIKit.UILabel statusLabel { get; set; }

		[Action ("Skip:")]
		partial void Skip (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (activityIndicatorView != null) {
				activityIndicatorView.Dispose ();
				activityIndicatorView = null;
			}

			if (skipButton != null) {
				skipButton.Dispose ();
				skipButton = null;
			}

			if (statusLabel != null) {
				statusLabel.Dispose ();
				statusLabel = null;
			}
		}
	}
}
