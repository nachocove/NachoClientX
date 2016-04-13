//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;

namespace NachoClient.iOS
{
    public static class UIFont_Utils
    {
        public static nfloat RoundedLineHeight (this UIFont font, nfloat scale)
        {
            return (nfloat)Math.Ceiling (font.LineHeight * scale) / scale;
        }
    }
}

