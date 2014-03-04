//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    [MonoTouch.Foundation.Register ("LabeledIconButton")]
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

            RectangleF buttonFrame = this.Frame;
            RectangleF imageFrame = this.ImageView.Frame;
            float imageX = (buttonFrame.Width / 2f) - (imageFrame.Width / 2);
            imageFrame.Location = new PointF (imageX, 2.0f);
            this.ImageView.Frame = imageFrame;

            this.TitleLabel.TextAlignment = UITextAlignment.Center;
            RectangleF titleFrame = this.TitleLabel.Frame;
            titleFrame.Width = buttonFrame.Width;
            titleFrame.Location = new PointF (0f, imageFrame.Top + imageFrame.Height + 4f);
            this.TitleLabel.Frame = titleFrame;
        }
    }
}
