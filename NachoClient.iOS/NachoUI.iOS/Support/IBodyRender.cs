//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

namespace NachoClient.iOS
{
    // A BodyView consists of a list of render views arranged vertically.
    // Each type of body render view must implement this interface
    public interface IBodyRender
    {
        SizeF ContentSize { get; }

        void ScrollTo (PointF upperLeftCorner);

        string LayoutInfo ();
    }
}

