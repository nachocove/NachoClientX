// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public partial class StartupViewController : UIViewController
    {
        public StartupViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Initial view
            if (0 == NcModel.Instance.Db.Table<McAccount> ().Count ()) {
                PerformSegue ("StartupToLaunch", this); // modal
                PerformSegue ("StartupToHome", this);  // launch the documentation
            } else {
                PerformSegue ("StartupToNachoNow", this); // push
            }
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
