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
	[Register ("ExchangeAdvancedFieldsViewController")]
	partial class ExchangeAdvancedFieldsViewController
	{
		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField domainField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField serverField { get; set; }

		[Outlet]
		NachoClient.iOS.NcAdjustableLayoutTextField usernameField { get; set; }

		[Action ("textFieldChanged:")]
		partial void textFieldChanged (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (serverField != null) {
				serverField.Dispose ();
				serverField = null;
			}

			if (domainField != null) {
				domainField.Dispose ();
				domainField = null;
			}

			if (usernameField != null) {
				usernameField.Dispose ();
				usernameField = null;
			}
		}
	}
}
