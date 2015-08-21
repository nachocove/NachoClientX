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
	[Register ("StandardCredentialsViewController")]
	partial class StandardCredentialsViewController
	{
		[Outlet]
		UIKit.UIImageView accountIconView { get; set; }

		[Outlet]
		NachoClient.iOS.NcActivityIndicatorView activityIndicatorView { get; set; }

		[Outlet]
		UIKit.UIButton advancedButton { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint advancedHeightConstraint { get; set; }

		[Outlet]
		UIKit.UIView advancedView { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField emailField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField passwordField { get; set; }

		[Outlet]
		UIKit.UIScrollView scrollView { get; set; }

		[Outlet]
		UIKit.UILabel statusLabel { get; set; }

		[Outlet]
		NachoClient.iOS.NcSimpleColorButton submitButton { get; set; }

		[Outlet]
		UIKit.UIButton supportButton { get; set; }

		[Action ("Advanced:")]
		partial void Advanced (Foundation.NSObject sender);

		[Action ("Submit:")]
		partial void Submit (Foundation.NSObject sender);

		[Action ("Support:")]
		partial void Support (Foundation.NSObject sender);

		[Action ("TextFieldChanged:")]
		partial void TextFieldChanged (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (accountIconView != null) {
				accountIconView.Dispose ();
				accountIconView = null;
			}

			if (activityIndicatorView != null) {
				activityIndicatorView.Dispose ();
				activityIndicatorView = null;
			}

			if (advancedButton != null) {
				advancedButton.Dispose ();
				advancedButton = null;
			}

			if (emailField != null) {
				emailField.Dispose ();
				emailField = null;
			}

			if (passwordField != null) {
				passwordField.Dispose ();
				passwordField = null;
			}

			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}

			if (statusLabel != null) {
				statusLabel.Dispose ();
				statusLabel = null;
			}

			if (submitButton != null) {
				submitButton.Dispose ();
				submitButton = null;
			}

			if (supportButton != null) {
				supportButton.Dispose ();
				supportButton = null;
			}

			if (advancedHeightConstraint != null) {
				advancedHeightConstraint.Dispose ();
				advancedHeightConstraint = null;
			}

			if (advancedView != null) {
				advancedView.Dispose ();
				advancedView = null;
			}
		}
	}
}
