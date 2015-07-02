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
        var transition = CATransition.CreateAnimation ();

        transition.Duration = 0.3;
        transition.Type = CATransition.TransitionFade;

        this.SourceViewController.NavigationController.View.Layer.AddAnimation (transition, CALayer.Transition);
        this.SourceViewController.NavigationController.PushViewController (this.DestinationViewController, false);
    }
}