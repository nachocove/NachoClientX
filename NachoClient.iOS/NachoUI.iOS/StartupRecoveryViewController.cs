// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using CoreGraphics;
using UIKit;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class StartupRecoveryViewController : NcUIViewController
    {

        public CGRect? AnimateFromLaunchImageFrame = null;
        private CGSize originalIndiatorSize;

        public StartupRecoveryViewController (IntPtr handle) : base (handle)
        {
        }

        public override UIStatusBarStyle PreferredStatusBarStyle ()
        {
            return UIStatusBarStyle.LightContent;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (AnimateFromLaunchImageFrame != null) {
                View.LayoutIfNeeded ();
                originalIndiatorSize = activityIndicator.Frame.Size;
                activityWidthConstraint.Constant = AnimateFromLaunchImageFrame.Value.Width;
                activityHeightConstraint.Constant = AnimateFromLaunchImageFrame.Value.Height;
                activityIndicator.Superview.RemoveConstraint (activityCenterYConstraint);
                activityIndicator.Superview.LayoutIfNeeded ();
                activityIndicator.Frame = activityIndicator.Superview.ConvertRectFromView (AnimateFromLaunchImageFrame.Value, View);
                infoLabel.Alpha = 0.0f;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (!NcApplication.Instance.InSafeMode ()) {
                DismissViewController (false, null);
                return;
            }
            if (AnimateFromLaunchImageFrame != null) {
                AnimateFromLaunchImageFrame = null;
                activityWidthConstraint.Constant = originalIndiatorSize.Width;
                activityHeightConstraint.Constant = originalIndiatorSize.Height;
                activityIndicator.Superview.AddConstraint (activityCenterYConstraint);
                UIView.Animate (0.5, () => {
                    activityIndicator.Superview.LayoutIfNeeded ();
                }, () => {
                    activityIndicator.StartAnimating ();
                });
                UIView.Animate (0.2, 0.3, 0, () => {
                    infoLabel.Alpha = 1.0f;
                }, null);
            } else {
                activityIndicator.StartAnimating ();
            }
        }
    }
}
