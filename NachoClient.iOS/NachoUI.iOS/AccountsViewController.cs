// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;

namespace NachoClient.iOS
{
    public partial class AccountsViewController : UITableViewController
    {
        public AccountsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("AccountsToNachoNow", this);
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }
    }
}
