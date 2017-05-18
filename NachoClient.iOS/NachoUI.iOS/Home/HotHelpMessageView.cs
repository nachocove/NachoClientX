//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class HotHelpMessageView : UIView
    {

        private UIImageView nachoMailIcon;
        private UIImageView chipsLeftIcon;
        private UIImageView chipsRightIcon;
        public UILabel hotListLabel;

        public HotHelpMessageView (CGRect frame) : base (frame)
        {
            nachoMailIcon = new UIImageView ();
            nachoMailIcon.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            nachoMailIcon.Image = UIImage.FromBundle ("Bootscreen-1");
            AddSubview (nachoMailIcon);

            chipsLeftIcon = new UIImageView ();
            chipsLeftIcon.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin;
            chipsLeftIcon.Image = UIImage.FromBundle ("gen-nacholeft");
            AddSubview (chipsLeftIcon);

            chipsRightIcon = new UIImageView ();
            chipsRightIcon.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleLeftMargin;
            chipsRightIcon.Image = UIImage.FromBundle ("gen-nachoright");
            AddSubview (chipsRightIcon);

            hotListLabel = new UILabel (new CGRect (0, 0, Bounds.Width, 50));
            hotListLabel.TextAlignment = UITextAlignment.Center;
            hotListLabel.BackgroundColor = UIColor.White;
            hotListLabel.Lines = 0;
            hotListLabel.LineBreakMode = UILineBreakMode.WordWrap;
            hotListLabel.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin | UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            AddSubview (hotListLabel);
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            var isFourS = false;
            var isSixOrGreater = false;
            var isSixPlusOrGreater = false;

            if (100 > Bounds.Height) {
                isFourS = true;
            } else {
                if (360 <= Bounds.Width) {
                    isSixOrGreater = true;
                }
                if (390 < Bounds.Width) {
                    isSixPlusOrGreater = true;
                }
            }
            nachoMailIcon.Hidden = isFourS;
            chipsLeftIcon.Hidden = isFourS;
            chipsRightIcon.Hidden = isFourS;
            nachoMailIcon.Frame = (isSixOrGreater ? new CGRect (Bounds.Width / 2 - 32.5f, A.Card_Vertical_Indent, 65, 65) : new CGRect (Bounds.Width / 2 - 22.5f, A.Card_Horizontal_Indent, 45, 45));
            chipsLeftIcon.Frame = (isSixOrGreater ? new CGRect (0, Bounds.Height - 45, 115, 45) : new CGRect (0, Bounds.Height - 33, 85, 33));
            chipsRightIcon.Frame = (isSixOrGreater ? new CGRect (Bounds.Width - 115, Bounds.Height - 45, 115, 45) : new CGRect (Bounds.Width - 85, Bounds.Height - 33, 85, 33));
            var messageWidth = (isSixPlusOrGreater ? Bounds.Width - 4 * A.Card_Horizontal_Indent : 320 - 4 * A.Card_Horizontal_Indent);
            hotListLabel.Frame = new CGRect (0.0, 0.0, messageWidth, 20.0);
            var hotListLabelYOffset = (isFourS ? Bounds.Height / 2 : ((chipsLeftIcon.Frame.Top - nachoMailIcon.Frame.Bottom) / 2) + nachoMailIcon.Frame.Bottom + 5);            
            hotListLabel.SizeToFit ();
            hotListLabel.Center = new CGPoint (Bounds.Width / 2, hotListLabelYOffset); 
        }
    }
}

