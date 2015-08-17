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
            
        var outermostView = NachoClient.Util.FindOutermostView(this.SourceViewController.View);
        var screenLocation = this.SourceViewController.View.Superview.ConvertPointToView (this.SourceViewController.View.Frame.Location, outermostView);

        // Capture the outermost view & adjust the bounds so it appears that
        // the original view is still around as the account view animates down.
        // Keep in mind the in-call and navigation status bars.
        using (var image = NachoClient.Util.captureView (outermostView)) {
            var imageView = new UIImageView (image);
            ViewFramer.Create (imageView).Y (-screenLocation.Y);
            this.DestinationViewController.View.AddSubview (imageView);
            this.DestinationViewController.View.SendSubviewToBack (imageView);
        }

        this.SourceViewController.NavigationController.PushViewController (this.DestinationViewController, false);
    }
}