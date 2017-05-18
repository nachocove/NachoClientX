//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class ErrorAccessoryView : UIView
    {
        private ErrorIndicatorView ErrorIndicator;

        public ErrorAccessoryView (float width = 30.0f, float indicatorSize = 24.0f) : base (new CGRect(0.0f, 0.0f, (nfloat)width, 0.0f))
        {
            BackgroundColor = UIColor.White;
            ErrorIndicator = new ErrorIndicatorView ((nfloat)indicatorSize);
            AddSubview (ErrorIndicator);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            ErrorIndicator.Center = new CGPoint (ErrorIndicator.Frame.Width / 2.0f, Bounds.Height / 2.0f);
        }
    }
}

