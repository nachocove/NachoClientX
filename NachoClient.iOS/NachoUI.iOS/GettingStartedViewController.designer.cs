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
	[Register ("GettingStartedViewController")]
	partial class GettingStartedViewController
	{
		[Outlet]
		UIKit.NSLayoutConstraint circleHeightConstraint { get; set; }

		[Outlet]
		UIKit.UIImageView circleImageView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint circleVerticalSpaceConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint circleWidthContstraint { get; set; }

		[Outlet]
		UIKit.UIButton getStartedButton { get; set; }

		[Outlet]
		UIKit.UILabel introLabel { get; set; }

		[Outlet]
		UIKit.UIImageView leftTriangleImageView { get; set; }

		[Outlet]
		UIKit.UIImageView rightTriangleImageView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (circleHeightConstraint != null) {
				circleHeightConstraint.Dispose ();
				circleHeightConstraint = null;
			}

			if (circleImageView != null) {
				circleImageView.Dispose ();
				circleImageView = null;
			}

			if (circleVerticalSpaceConstraint != null) {
				circleVerticalSpaceConstraint.Dispose ();
				circleVerticalSpaceConstraint = null;
			}

			if (circleWidthContstraint != null) {
				circleWidthContstraint.Dispose ();
				circleWidthContstraint = null;
			}

			if (getStartedButton != null) {
				getStartedButton.Dispose ();
				getStartedButton = null;
			}

			if (introLabel != null) {
				introLabel.Dispose ();
				introLabel = null;
			}

			if (leftTriangleImageView != null) {
				leftTriangleImageView.Dispose ();
				leftTriangleImageView = null;
			}

			if (rightTriangleImageView != null) {
				rightTriangleImageView.Dispose ();
				rightTriangleImageView = null;
			}
		}
	}
}
