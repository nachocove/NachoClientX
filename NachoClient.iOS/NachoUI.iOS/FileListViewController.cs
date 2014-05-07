// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class FileListViewController : UITableViewController
    {
        public FileListViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();
            this.View.AddGestureRecognizer (this.RevealViewController ().PanGestureRecognizer);

            // Multiple buttons on the right side
            NavigationItem.RightBarButtonItems = new UIBarButtonItem[] { editButton };

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton, nachoButton };
            using (var nachoImage = UIImage.FromBundle ("Nacho-Cove-Icon")) {
                nachoButton.Image = nachoImage.ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            }
            nachoButton.Clicked += (object sender, EventArgs e) => {
                PerformSegue ("FilesToNachoNow", this);
            };
            // Watch for changes from the back end
            BackEnd.Instance.StatusIndEvent += (object sender, EventArgs e) => {
                var s = (StatusIndEventArgs)e;
                if (NcResult.SubKindEnum.Info_TaskSetChanged == s.Status.SubKind) {
                    RefreshFileList ();
                }
            };
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            RefreshFileList ();
        }

        protected void RefreshFileList()
        {
            this.TableView.DataSource = new FileTableSource ();
            this.TableView.ReloadData ();
        }

    }
}
