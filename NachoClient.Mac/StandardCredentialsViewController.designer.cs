// WARNING
//
// This file has been generated automatically by Xamarin Studio Business to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace NachoClient.Mac
{
	[Register ("StandardCredentialsViewController")]
	partial class StandardCredentialsViewController
	{
		[Outlet]
		AppKit.NSButton ConnectButton { get; set; }

		[Outlet]
		AppKit.NSTextField EmailField { get; set; }

		[Outlet]
		AppKit.NSSecureTextField PasswordField { get; set; }

		[Action ("connect:")]
		partial void Connect (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (ConnectButton != null) {
				ConnectButton.Dispose ();
				ConnectButton = null;
			}

			if (EmailField != null) {
				EmailField.Dispose ();
				EmailField = null;
			}

			if (PasswordField != null) {
				PasswordField.Dispose ();
				PasswordField = null;
			}
		}
	}
}
