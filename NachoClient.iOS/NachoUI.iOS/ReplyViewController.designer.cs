// WARNING
//
// This file has been generated automatically by Xamarin Studio to store outlets and
// actions made in the Xcode designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
	[Register ("ReplyViewController")]
	partial class ReplyViewController
	{
		[Outlet]
		MonoTouch.UIKit.UITextView txtComposeReply { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtCopyList { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtReplyList { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITextField txtSubjectField { get; set; }

		[Action ("btnSend:")]
		partial void btnSend (MonoTouch.Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (txtCopyList != null) {
				txtCopyList.Dispose ();
				txtCopyList = null;
			}

			if (txtReplyList != null) {
				txtReplyList.Dispose ();
				txtReplyList = null;
			}

			if (txtSubjectField != null) {
				txtSubjectField.Dispose ();
				txtSubjectField = null;
			}

			if (txtComposeReply != null) {
				txtComposeReply.Dispose ();
				txtComposeReply = null;
			}
		}
	}
}
