//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using CoreGraphics;
using System.Collections.Generic;
using System.Linq;

using UIKit;
using Foundation;

using NachoCore.Utils;

namespace NachoClient.iOS
{

    /// <summary>
    /// Cover view create a view that intended to cover
    /// an area (like the whole screen) but to create a
    /// hole to let events pass throught.
    /// </summary>
    public class CoverView : UIView
    {
        CGRect hole;

        public CoverView (UIView view, CGRect hole) : base (view.Frame)
        {
            this.hole = hole;
            this.UserInteractionEnabled = true;
            this.BackgroundColor = UIColor.Clear;
            // this.BackgroundColor = UIColor.FromWhiteAlpha (0.3f, 0.3f); // DEBUG
        }

        public override bool PointInside (CGPoint point, UIEvent uievent)
        {
            return !hole.Contains (point);
        }
    }
    
}
