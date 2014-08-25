using System;
using System.Drawing;
using MonoTouch.CoreAnimation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

[Register ("FadeCustomSegue")]
public class FadeCustomSegue : UIStoryboardSegue
{

    public  FadeCustomSegue( IntPtr i) : base(i)
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