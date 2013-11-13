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
	[Register ("ComposeViewController")]
	partial class ComposeViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextView txtComposeMsg { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtSubjectField { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtToField { get; set; }

		[Action ("btnSendEmail:")]
		partial void btnSendEmail (MonoTouch.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (txtComposeMsg != null) {
				txtComposeMsg.Dispose ();
				txtComposeMsg = null;
			}

			if (txtSubjectField != null) {
				txtSubjectField.Dispose ();
				txtSubjectField = null;
			}

			if (txtToField != null) {
				txtToField.Dispose ();
				txtToField = null;
			}
		}
	}
}
