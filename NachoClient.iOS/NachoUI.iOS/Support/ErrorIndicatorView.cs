//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ErrorIndicatorView : UIView
    {

        UILabel Label;

        public ErrorIndicatorView (nfloat size) : base (new CGRect(0.0f, 0.0f, size, size))
        {
            ClipsToBounds = true;
            BackgroundColor = UIColor.Red;
            Label = new UILabel ();
            Label.Text = "!";
            Label.Font = A.Font_AvenirNextDemiBold14.WithSize (size * 0.6f);
            Label.TextColor = UIColor.White;
            Label.TextAlignment = UITextAlignment.Center;
            Label.LineBreakMode = UILineBreakMode.Clip;
            Label.SizeToFit ();
            AddSubview (Label);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var size = (nfloat)Math.Min (Bounds.Width, Bounds.Height);
            Layer.CornerRadius = size / 2.0f;
            Label.Font = Label.Font.WithSize (size * 0.6f);
            Label.SizeToFit ();
            Label.Center = new CGPoint (Bounds.Width / 2.0f, Bounds.Height / 2.0f);
        }
    }
}

