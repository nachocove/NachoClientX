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
	[Register ("NachoNowViewController")]
	partial class NachoNowViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem calendarNowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem contactsNowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem emailNowButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView tableView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem tasksNowButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (calendarNowButton != null) {
				calendarNowButton.Dispose ();
				calendarNowButton = null;
			}

			if (contactsNowButton != null) {
				contactsNowButton.Dispose ();
				contactsNowButton = null;
			}

			if (emailNowButton != null) {
				emailNowButton.Dispose ();
				emailNowButton = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (tableView != null) {
				tableView.Dispose ();
				tableView = null;
			}

			if (tasksNowButton != null) {
				tasksNowButton.Dispose ();
				tasksNowButton = null;
			}
		}
	}
}
