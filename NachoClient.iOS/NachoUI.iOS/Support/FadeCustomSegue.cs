using System;
using CoreGraphics;
using CoreAnimation;
using Foundation;
using UIKit;

[Register ("FadeCustomSegue")]
public class FadeCustomSegue : UIStoryboardSegue
{

    public FadeCustomSegue (IntPtr i) : base (i)
    {
    }

    public FadeCustomSegue (string identifier, UIViewController source, UIViewController destination) : base (identifier, source, destination)
    {
    }

    public override void Perform ()
    {
        Transition (this.SourceViewController, this.DestinationViewController);
    }

    public static void Transition (UIViewController source, UIViewController destination)
    {
        var transition = CATransition.CreateAnimation ();
        transition.Duration = 0.3;
        transition.Type = CATransition.TransitionFade;

        source.NavigationController.View.Layer.AddAnimation (transition, CALayer.Transition);
        source.NavigationController.PushViewController (destination, false);
    }
}