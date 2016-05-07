//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using Foundation;

namespace NachoClient.iOS
{
    public class SwitchAccountControl : UIView
    {

        UIView BackgroundView;
        UIImageView SelectedAccountView;
        nfloat BorderWidth = 2.0f;

        public SwitchAccountControl () : base (new CGRect(0.0f, 0.0f, 44.0f, 44.0f))
        {
            BackgroundView = new UIView (Bounds);
            BackgroundView.Layer.CornerRadius = Bounds.Width / 2.0f;
            BackgroundView.BackgroundColor = A.Color_NachoGreen;

            SelectedAccountView = new UIImageView (UIImage.FromBundle ("avatar-office365")); 
            SelectedAccountView.Frame = Bounds.Inset (BorderWidth, BorderWidth);
            SelectedAccountView.Layer.CornerRadius = SelectedAccountView.Frame.Width / 2.0f;
            SelectedAccountView.ClipsToBounds = true;

            AddSubview (BackgroundView);
            AddSubview (SelectedAccountView);
        }
    }
}

