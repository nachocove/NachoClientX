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
	[Register ("MessagePriorityViewController")]
	partial class MessagePriorityViewController
	{
		[Outlet]
		MonoTouch.UIKit.UIButton deferButton { get; set; }
		
		void ReleaseDesignerOutlets ()
		{
			if (deferButton != null) {
				deferButton.Dispose ();
				deferButton = null;
			}
		}
	}
}
