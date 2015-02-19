//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using UIKit;

namespace NachoClient.iOS
{
    public interface IBodyRender
    {
        UIView uiView ();

        CGSize ContentSize { get; }

        void ScrollingAdjustment (CGRect frame, CGPoint contentOffset);
    }
}
