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
	[Register ("ImapAdvancedFieldsViewController")]
	partial class ImapAdvancedFieldsViewController
	{
		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField incomingPortField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField incomingServerField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField outgoingPortField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField outgoingServerField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField usernameField { get; set; }

		[Action ("textFieldChanged:")]
		partial void textFieldChanged (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (usernameField != null) {
				usernameField.Dispose ();
				usernameField = null;
			}

			if (incomingServerField != null) {
				incomingServerField.Dispose ();
				incomingServerField = null;
			}

			if (incomingPortField != null) {
				incomingPortField.Dispose ();
				incomingPortField = null;
			}

			if (outgoingServerField != null) {
				outgoingServerField.Dispose ();
				outgoingServerField = null;
			}

			if (outgoingPortField != null) {
				outgoingPortField.Dispose ();
				outgoingPortField = null;
			}
		}
	}
}
