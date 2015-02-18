//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;
using CoreGraphics;

namespace NachoClient.iOS
{
    public class NachoInlineImageTextAttachment : NSTextAttachment
    {
        public override CGRect GetAttachmentBounds (NSTextContainer textContainer, CGRect proposedLineFragment, CGPoint glyphPosition, nuint characterIndex)
        {
            return new CGRect (0, -6, 20, 20);
        }
    }
}

