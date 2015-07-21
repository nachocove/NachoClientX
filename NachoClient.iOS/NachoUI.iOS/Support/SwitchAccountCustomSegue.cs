using System;
using CoreGraphics;
using CoreAnimation;
using Foundation;
using UIKit;
using NachoClient.iOS;
using NachoCore.Utils;

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
        if (null == this.SourceViewController) {
            Log.Error (Log.LOG_UI, "SwitchAccountCustomSegue: SourceViewController is null.");
            return;
        }
        if (null == this.SourceViewController.NavigationController) {
            Log.Error (Log.LOG_UI, "SwitchAccountCustomSegue: SourceViewController.NavigationController is null.");
            return;
        }
        if ((null == SourceViewController.View) || (null == SourceViewController.View.Window)) {
            Log.Error (Log.LOG_UI, "SwitchAccountCustomSegue: SourceViewController {0} is null.", (null == SourceViewController.View ? "view" : "window"));
            this.SourceViewController.NavigationController.PushViewController (this.DestinationViewController, false);
            return;
        }
            
        using (var image = NachoClient.Util.captureView (this.SourceViewController.View.Window)) {
            var imageView = new UIImageView (image);
            ViewFramer.Create (imageView).Y (-64);
            this.DestinationViewController.View.AddSubview (imageView);
            this.DestinationViewController.View.SendSubviewToBack (imageView);
        }
        this.SourceViewController.NavigationController.PushViewController (this.DestinationViewController, false);
    }
}