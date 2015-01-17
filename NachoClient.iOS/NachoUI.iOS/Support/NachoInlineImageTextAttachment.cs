//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Drawing;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    public class NachoInlineImageTextAttachment : NSTextAttachment
    {
        public override RectangleF GetAttachmentBounds (NSTextContainer textContainer, RectangleF proposedLineFragment, PointF glyphPosition, uint characterIndex)
        {
            return new RectangleF (0, -6, 20, 20);
        }
    }
}

