//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class PortraitView : UIView, ThemeAdopter
    {

        UILabel InitialsLabel;
        UIImageView PhotoView;
        nfloat FontSizeRatio = 0.6f;

        public PortraitView (CGRect frame) : base (frame)
        {
            ClipsToBounds = true;
            InitialsLabel = new UILabel (Bounds);
            InitialsLabel.TextColor = UIColor.White;
            InitialsLabel.TextAlignment = UITextAlignment.Center;
            InitialsLabel.LineBreakMode = UILineBreakMode.Clip;
            InitialsLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            PhotoView = new UIImageView (Bounds);
            PhotoView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            AddSubview (InitialsLabel);
            AddSubview (PhotoView);
        }

        public void AdoptTheme (Theme theme)
        {
            InitialsLabel.Font = theme.DefaultFont.WithSize (Bounds.Height * FontSizeRatio);
        }

        public void SetPortrait (int portraitId, int color, string initials)
        {
            InitialsLabel.Text = initials;
            UIImage portraitImage = null;
            if (portraitId != 0) {
                portraitImage = Util.PortraitToImage (portraitId);
            }
            if (portraitImage == null) {
                InitialsLabel.Hidden = false;
                PhotoView.Hidden = true;
                PhotoView.Image = null;
                BackgroundColor = Util.ColorForUser (color);
                InitialsLabel.BackgroundColor = BackgroundColor;
            } else {
                PhotoView.Image = portraitImage;
                PhotoView.Hidden = false;
                InitialsLabel.Hidden = true;
                BackgroundColor = UIColor.White;
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            Layer.CornerRadius = Bounds.Height / 2.0f;
            InitialsLabel.Font = InitialsLabel.Font.WithSize (Bounds.Height * FontSizeRatio);
        }
    }
}

