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
	[Register ("EventAttendeeViewController")]
	partial class EventAttendeeViewController
	{
		[Outlet]
		UIKit.UIBarButtonItem addAttendeeButton { get; set; }

		[Outlet]
		UIKit.UITableView EventAttendeesTableView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (EventAttendeesTableView != null) {
				EventAttendeesTableView.Dispose ();
				EventAttendeesTableView = null;
			}

			if (addAttendeeButton != null) {
				addAttendeeButton.Dispose ();
				addAttendeeButton = null;
			}
		}
	}
}
