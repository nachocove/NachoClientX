// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using SWRevealViewControllerBinding;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class SettingsViewController : NcDialogViewController
    {
        public SettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("SettingsToNachoNow", this);
            };

            // Test
            //var e = new PasswordElement (name, "", value ?? "");

            var root = new RootElement ("Settings");

            var PasswordSection = new Section ("Exchange Accounts");
            var Password = new StringElement ("Edit Account Password", delegate {
                EditAccount ();
            });
            PasswordSection.Add (Password);
            root.Add (PasswordSection);

            var ResetSection = new Section ("Reset");
            var KickstartButton = new StringElement ("Touch to kickstart", delegate {
                Kickstart ();
            });
            ResetSection.Add (KickstartButton);
            var ResetButton = new StringElement ("Touch to reset database", delegate {
                Reset ();
            });
            ResetSection.Add (ResetButton);

            root.Add (ResetSection);

            Root = root;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        void EditAccount ()
        {
            Log.Info (Log.LOG_UI, "Edit account");
            var editViewController = new EditAccountViewController (null);
            NavigationController.PushViewController (editViewController, true);
        }

        void Kickstart ()
        {
            Log.Info (Log.LOG_UI, "Kickstart pressed");
            // TODO: Kickstart
        }

        void Reset ()
        {
            Log.Info (Log.LOG_UI, "Reset pressed");
            // TODO: Reset
        }
    }
}
