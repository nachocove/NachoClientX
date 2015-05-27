//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class SwitchAccountButton : UIView
    {
        public delegate void SwitchAccountCallback ();

        UIView haloView;
        UIButton switchButton;
        SwitchAccountCallback callback;

        public SwitchAccountButton (SwitchAccountCallback switchAccountCallback) : base (new CGRect (0, 0, 44, 44))
        {
            callback = switchAccountCallback;

            haloView = new UIView (new CGRect (0, 0, 44, 44));
            haloView.Layer.CornerRadius = 22;
            haloView.BackgroundColor = A.Color_NachoGreen;
            haloView.Layer.AllowsEdgeAntialiasing = true;
            ViewFramer.Create (haloView).Y (4);

            switchButton = UIButton.FromType (UIButtonType.System);
            switchButton.Frame = new CGRect (2, 2, 40, 40);
            switchButton.Layer.MasksToBounds = true;
            switchButton.Layer.CornerRadius = 20;
            switchButton.BackgroundColor = A.Color_NachoGreen;
            switchButton.Layer.AllowsEdgeAntialiasing = true;
            switchButton.TouchUpInside += SwitchButton_TouchUpInside;
            switchButton.AccessibilityLabel = "Switch account";

            switchButton.AdjustsImageWhenHighlighted = false;

            haloView.AddSubview (switchButton);
            this.AddSubview (haloView);

            this.BackgroundColor = A.Color_NachoGreen;
        }

        void SwitchButton_TouchUpInside (object sender, EventArgs e)
        {
            if (null != callback) {
                callback ();
            }
        }

        public void SetAccountImage (McAccount account)
        {
            using (var image = Util.ImageForAccount (account).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                switchButton.SetImage (image, UIControlState.Normal);
                switchButton.SetImage (image, UIControlState.Selected);
                switchButton.SetImage (image, UIControlState.Highlighted);
            }
        }

        public void SetImage (string imageName)
        {
            using (var image = UIImage.FromBundle (imageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                switchButton.SetImage (image, UIControlState.Normal);
                switchButton.SetImage (image, UIControlState.Selected);
                switchButton.SetImage (image, UIControlState.Highlighted);
            }
        }

    }
}

