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
	[Register ("LaunchViewController")]
	partial class LaunchViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextField txtPassword { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtServerName { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtUserName { get; set; }

		[Action ("btnLaunchAcct:")]
		partial void btnLaunchAcct (MonoTouch.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (txtPassword != null) {
				txtPassword.Dispose ();
				txtPassword = null;
			}

			if (txtServerName != null) {
				txtServerName.Dispose ();
				txtServerName = null;
			}

			if (txtUserName != null) {
				txtUserName.Dispose ();
				txtUserName = null;
			}
		}
	}
}
