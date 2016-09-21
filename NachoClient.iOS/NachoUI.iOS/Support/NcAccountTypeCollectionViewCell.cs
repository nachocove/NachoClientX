//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;

namespace NachoClient.iOS
{
    
    [Register ("NcAccountTypeCollectionViewCell")]
    public partial class NcAccountTypeCollectionViewCell : UICollectionViewCell, ThemeAdopter
    {
        [Outlet]
        public UIImageView iconView { get; set; }

        [Outlet]
        public UILabel label { get; set; }

        public NcAccountTypeCollectionViewCell (IntPtr handle) : base (handle)
        {
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            // Something changed in the storyboard and when this code runs, the image frame is 1000x1000,
            // so the radius calculation is wrong.  Unclear what the fix is other than hard-coding.
            //iconView.Layer.CornerRadius = iconView.Frame.Width / 2.0f;
            iconView.Layer.CornerRadius = 32.0f;
        }

        public void AdoptTheme (Theme theme)
        {
            label.TextColor = theme.AccountCreationFontColor;
            label.Font = theme.DefaultFont.WithSize (label.Font.PointSize);
        }

    }
}

