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

    public static nint ShadeViewTag = 200; 

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
        if ((null == SourceViewController.View) || (null == SourceViewController.View.Window)) {
            Log.Error (Log.LOG_UI, "SwitchAccountCustomSegue: SourceViewController {0} is null.", (null == SourceViewController.View ? "view" : "window"));
            this.SourceViewController.PresentViewController (this.DestinationViewController, true, null);
            return;
        }

        var sourceSnapshot = SourceViewController.View.Window.SnapshotView (false);
        // taking a second snapshot and adding it to the source window to prevent a button flicker
        var sourceSnapshot2 = SourceViewController.View.Window.SnapshotView (false);
        SourceViewController.View.Window.AddSubview (sourceSnapshot2);
        var sourceNavbarSnapshot = SourceViewController.NavigationController.NavigationBar.SnapshotView (false);
        var sourceNavbarFrame = SourceViewController.NavigationController.View.ConvertRectToView (SourceViewController.NavigationController.NavigationBar.Frame, SourceViewController.View.Window); 
        var destinationSnapshot = new UIImageView (DestinationViewController.View.Frame);
        var statusGapView = new UIView(new CGRect(0, 0, sourceNavbarFrame.Width, sourceNavbarFrame.Top));
        var shadeView = new UIView (DestinationViewController.View.Frame);
        shadeView.BackgroundColor = UIColor.Black;
        shadeView.Alpha = 0.0f;
        shadeView.Tag = ShadeViewTag;
        statusGapView.BackgroundColor = SourceViewController.NavigationController.NavigationBar.BarTintColor;
        using (var image = NachoClient.Util.captureView(DestinationViewController.View)){
            destinationSnapshot.Image = image;
        }
        sourceNavbarSnapshot.Frame = sourceNavbarFrame;
        DestinationViewController.View.AddSubview (sourceSnapshot);
        DestinationViewController.View.AddSubview (shadeView);
        DestinationViewController.View.AddSubview (destinationSnapshot);
        DestinationViewController.View.AddSubview (statusGapView);
        DestinationViewController.View.AddSubview (sourceNavbarSnapshot);
        ViewFramer.Create (sourceSnapshot).Y (-DestinationViewController.View.Frame.Top);
        ViewFramer.Create (statusGapView).Y (-DestinationViewController.View.Frame.Top);
        ViewFramer.Create (sourceNavbarSnapshot).Y (sourceNavbarFrame.Top - DestinationViewController.View.Frame.Top);
        ViewFramer.Create (destinationSnapshot).Y (-DestinationViewController.View.Frame.Top);
        destinationSnapshot.Transform = CGAffineTransform.MakeTranslation (0, -destinationSnapshot.Frame.Height + sourceNavbarSnapshot.Frame.Top + sourceNavbarSnapshot.Frame.Height);
        destinationSnapshot.Layer.ShadowColor = UIColor.Black.CGColor;
        destinationSnapshot.Layer.ShadowOpacity = 0.4f;
        destinationSnapshot.Layer.ShadowRadius = 10.0f;
        SourceViewController.PresentViewController (this.DestinationViewController, false, () => {
            destinationSnapshot.Transform = CGAffineTransform.MakeTranslation (0, -destinationSnapshot.Frame.Height);
            sourceSnapshot2.RemoveFromSuperview ();
            UIView.Animate (0.3, 0.0, UIViewAnimationOptions.CurveEaseOut, () => {
                shadeView.Alpha = 0.6f;
                destinationSnapshot.Transform = CGAffineTransform.MakeTranslation (0, 20);
            }, () => {
                DestinationViewController.View.SendSubviewToBack(shadeView);
                DestinationViewController.View.SendSubviewToBack(sourceSnapshot);
                sourceNavbarSnapshot.RemoveFromSuperview ();
                statusGapView.RemoveFromSuperview ();
                destinationSnapshot.RemoveFromSuperview ();
            });
        });

    }
}