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
	[Register ("StartupRecoveryViewController")]
	partial class StartupRecoveryViewController
	{
		[Outlet]
		UIKit.NSLayoutConstraint activityCenterYConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint activityHeightConstraint { get; set; }

		[Outlet]
		NachoClient.iOS.NcActivityIndicatorView activityIndicator { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint activityWidthConstraint { get; set; }

		[Outlet]
		UIKit.UILabel infoLabel { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (activityIndicator != null) {
				activityIndicator.Dispose ();
				activityIndicator = null;
			}

			if (infoLabel != null) {
				infoLabel.Dispose ();
				infoLabel = null;
			}

			if (activityWidthConstraint != null) {
				activityWidthConstraint.Dispose ();
				activityWidthConstraint = null;
			}

			if (activityHeightConstraint != null) {
				activityHeightConstraint.Dispose ();
				activityHeightConstraint = null;
			}

			if (activityCenterYConstraint != null) {
				activityCenterYConstraint.Dispose ();
				activityCenterYConstraint = null;
			}
		}
	}
}
