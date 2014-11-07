//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;

namespace NachoClient.iOS
{
    public interface IBodyRender
    {
        UIView uiView ();

        SizeF ContentSize { get; }

        void ScrollingAdjustment (RectangleF frame, PointF contentOffset);
    }
}
