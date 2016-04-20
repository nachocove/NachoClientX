//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore;

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
            switchButton.ImageView.ContentMode = UIViewContentMode.ScaleAspectFill;

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
            UpdateBeacon ();
        }

        public void SetImage (string imageName)
        {
            using (var image = UIImage.FromBundle (imageName).ImageWithRenderingMode (UIImageRenderingMode.AlwaysOriginal)) {
                switchButton.SetImage (image, UIControlState.Normal);
                switchButton.SetImage (image, UIControlState.Selected);
                switchButton.SetImage (image, UIControlState.Highlighted);
            }
            UpdateBeacon ();
        }

        public void SetHaloColor(UIColor color)
        {
            UIView.Animate (0.3, () => {
                haloView.BackgroundColor = color;
            });
        }

        public void UpdateBeacon()
        {
            if (NcAccountMonitor.Instance.HasNewEmail) {
                SetHaloColor (A.Color_NachoBlue);
            } else {
                SetHaloColor (A.Color_NachoGreen);
            }
        }
    }

    public class AddAccountCell : UIView
    {
        public delegate void AddAccountCallback ();

        AddAccountCallback callback;

        public AddAccountCell (CGRect rect, AddAccountCallback addAccountCallback) : base (rect)
        {
            callback = addAccountCallback;

            this.BackgroundColor = A.Color_NachoBackgroundGray;

            var newAccountButton = UIButton.FromType (UIButtonType.System);
            newAccountButton.Layer.CornerRadius = A.Card_Corner_Radius;
            newAccountButton.Frame = Util.CardContentRectangle (rect.Width, 40);
            newAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;

            newAccountButton.BackgroundColor = UIColor.White;
            newAccountButton.Font = A.Font_AvenirNextRegular14;
            newAccountButton.SetTitle ("Add Account", UIControlState.Normal);
            newAccountButton.SetTitleColor (A.Color_NachoBlack, UIControlState.Normal);

            Util.SetOriginalImagesForButton (newAccountButton, "email-add");
            newAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 12, 0, 36);
            newAccountButton.ImageEdgeInsets = new UIEdgeInsets (0, newAccountButton.Frame.Width - 36, 0, 0);
            newAccountButton.ContentEdgeInsets = new UIEdgeInsets ();

            newAccountButton.TouchUpInside += NewAccountButton_TouchUpInside;

            this.AddSubview (newAccountButton);
        }

        void NewAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            if (null != callback) {
                callback ();
            }
        }

    }

    public class ConnectToSalesforceCell : UIView
    {
        public delegate void ConnectToSalesforceCallback ();

        ConnectToSalesforceCallback callback;

        public ConnectToSalesforceCell (CGRect rect, ConnectToSalesforceCallback connectToSalesforceCallback) : base (rect)
        {
            callback = connectToSalesforceCallback;

            this.BackgroundColor = A.Color_NachoBackgroundGray;

            var newAccountButton = UIButton.FromType (UIButtonType.System);
            newAccountButton.Layer.CornerRadius = A.Card_Corner_Radius;
            newAccountButton.Frame = Util.CardContentRectangle (rect.Width, 40);
            newAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;

            newAccountButton.BackgroundColor = UIColor.White;
            newAccountButton.Font = A.Font_AvenirNextRegular14;
            newAccountButton.SetTitle ("Connect to Salesforce", UIControlState.Normal);
            newAccountButton.SetTitleColor (A.Color_NachoBlack, UIControlState.Normal);

            Util.SetOriginalImagesForButton (newAccountButton, "email-add");
            newAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 12, 0, 36);
            newAccountButton.ImageEdgeInsets = new UIEdgeInsets (0, newAccountButton.Frame.Width - 36, 0, 0);
            newAccountButton.ContentEdgeInsets = new UIEdgeInsets ();

            newAccountButton.TouchUpInside += ConnectAccountButton_TouchUpInside;

            this.AddSubview (newAccountButton);
        }

        void ConnectAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            if (null != callback) {
                callback ();
            }
        }

    }
}

