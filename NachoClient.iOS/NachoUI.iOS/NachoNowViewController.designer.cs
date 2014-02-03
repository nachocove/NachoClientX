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
		iCarouselBinding.iCarousel Carousel { get; set; }

		[Outlet]
		MonoTouch.UIKit.UITableView folderDataTableview { get; set; }

		[Outlet]
		MonoTouch.UIKit.UIBarButtonItem revealButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (Carousel != null) {
				Carousel.Dispose ();
				Carousel = null;
			}

			if (revealButton != null) {
				revealButton.Dispose ();
				revealButton = null;
			}

			if (folderDataTableview != null) {
				folderDataTableview.Dispose ();
				folderDataTableview = null;
			}
		}
	}
}
