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

            switchButton = UIButton.FromType (UIButtonType.System);
            switchButton.Frame = new CGRect (2, 2, 40, 40);
            switchButton.Layer.MasksToBounds = true;
            switchButton.Layer.CornerRadius = 20;
            switchButton.BackgroundColor = A.Color_NachoGreen;
            switchButton.Layer.AllowsEdgeAntialiasing = true;
            switchButton.TouchUpInside += SwitchButton_TouchUpInside;

            haloView = new UIView (new CGRect (0, 0, 44, 44));
            haloView.Layer.CornerRadius = 22;
            haloView.BackgroundColor = A.Color_NachoGreen;
            switchButton.Layer.AllowsEdgeAntialiasing = true;

            haloView.AddSubview (switchButton);

            this.BackgroundColor = A.Color_NachoGreen;
            this.AddSubview (haloView);

            ViewFramer.Create (haloView).Y (4);
        }

        void SwitchButton_TouchUpInside (object sender, EventArgs e)
        {
            if (null != callback) {
                callback ();
            }
        }

        public void SetAccountImage (McAccount account)
        {
            var image = Util.ImageForAccount (account).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal);
            switchButton.SetImage (image, UIControlState.Normal);
        }

    }
}

