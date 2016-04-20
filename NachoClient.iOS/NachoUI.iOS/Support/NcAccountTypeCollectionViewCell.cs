//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;

namespace NachoClient.iOS
{
    
    [Register ("NcAccountTypeCollectionViewCell")]
    public partial class NcAccountTypeCollectionViewCell : UICollectionViewCell
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
            iconView.Layer.CornerRadius = iconView.Frame.Width / 2.0f;
        }

    }
}

