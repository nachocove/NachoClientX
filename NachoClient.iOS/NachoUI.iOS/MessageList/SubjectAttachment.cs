//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using CoreGraphics;
using UIKit;

namespace NachoClient.iOS
{

    public class SubjectAttachment : NSTextAttachment
    {
        
        UIFont Font;

        public SubjectAttachment (UIFont font) : base ()
        {
            Font = font;
        }

        public override CGRect GetAttachmentBounds (NSTextContainer textContainer, CGRect proposedLineFragment, CGPoint glyphPosition, nuint characterIndex)
        {
            nfloat height = Font.CapHeight;
            return new CGRect (0.0f, 0.0f, Image.Size.Width * height / Image.Size.Height, height);
        }
    }

    public class HotAttachment : SubjectAttachment
    {
        public HotAttachment (UIFont font) : base (font)
        {
            Image = UIImage.FromBundle("subject-hot");
        }
    }

    public class AttachAttachment : SubjectAttachment
    {
        public AttachAttachment (UIFont font) : base (font)
        {
            Image = UIImage.FromBundle("subject-attach");
        }
    }
}

