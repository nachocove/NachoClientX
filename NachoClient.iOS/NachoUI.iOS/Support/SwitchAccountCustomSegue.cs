using System;
using CoreGraphics;
using CoreAnimation;
using Foundation;
using UIKit;
using NachoClient.iOS;

[Register ("SwitchAccountCustomSegue")]
public class SwitchAccountCustomSegue : UIStoryboardSegue
{

    public  SwitchAccountCustomSegue (IntPtr i) : base (i)
    {
    }

    public SwitchAccountCustomSegue (string identifier, UIViewController source, UIViewController destination) : base (identifier, source, destination)
    {
    }

    public override void Perform ()
    {
        using (var image = NachoClient.Util.captureView (this.SourceViewController.View.Window)) {
            var imageView = new UIImageView (image);
            ViewFramer.Create (imageView).Y (-64);
            this.DestinationViewController.View.AddSubview (imageView);
            this.DestinationViewController.View.SendSubviewToBack (imageView);
        }
        this.SourceViewController.NavigationController.PushViewController (this.DestinationViewController, false);
    }
}