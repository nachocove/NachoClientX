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
	[Register ("ChooseCalendarViewController")]
	partial class ChooseCalendarViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIView contentView { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIScrollView scrollView { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (scrollView != null) {
				scrollView.Dispose ();
				scrollView = null;
			}

			if (contentView != null) {
				contentView.Dispose ();
				contentView = null;
			}
		}
	}
}
