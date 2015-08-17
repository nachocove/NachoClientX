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
	[Register ("StartupViewController")]
	partial class StartupViewController
	{
		[Outlet]
		UIKit.NSLayoutConstraint circleHeightConstraint { get; set; }

		[Outlet]
		UIKit.UIImageView circleImageView { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint circleWidthConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint circleXConstraint { get; set; }

		[Outlet]
		UIKit.NSLayoutConstraint circleYConstraint { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (circleImageView != null) {
				circleImageView.Dispose ();
				circleImageView = null;
			}

			if (circleHeightConstraint != null) {
				circleHeightConstraint.Dispose ();
				circleHeightConstraint = null;
			}

			if (circleWidthConstraint != null) {
				circleWidthConstraint.Dispose ();
				circleWidthConstraint = null;
			}

			if (circleYConstraint != null) {
				circleYConstraint.Dispose ();
				circleYConstraint = null;
			}

			if (circleXConstraint != null) {
				circleXConstraint.Dispose ();
				circleXConstraint = null;
			}
		}
	}
}
