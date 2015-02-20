//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using UIKit;

namespace NachoClient.iOS
{
    [Foundation.Register ("LabeledIconButton")]
    public class LabeledIconButton : UIButton
    {
        /// <summary>
        /// Button with vertically stacked and centered icon and label.
        /// </summary>
        public LabeledIconButton (IntPtr p) : base (p)
        {
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();

            CGRect buttonFrame = this.Frame;
            CGRect imageFrame = this.ImageView.Frame;
            nfloat imageX = (buttonFrame.Width / 2f) - (imageFrame.Width / 2);
            imageFrame.Location = new CGPoint (imageX, 2.0f);
            this.ImageView.Frame = imageFrame;

            this.TitleLabel.TextAlignment = UITextAlignment.Center;
            CGRect titleFrame = this.TitleLabel.Frame;
            titleFrame.Width = buttonFrame.Width;
            titleFrame.Location = new CGPoint (0f, imageFrame.Top + imageFrame.Height + 4f);
            this.TitleLabel.Frame = titleFrame;
        }
    }
}
