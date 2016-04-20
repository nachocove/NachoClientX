//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using Foundation;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public interface IUcAttachmentBlockDelegate
    {
        void AttachmentBlockNeedsLayout (UcAttachmentBlock view);

        void DisplayAttachmentForAttachmentBlock (McAttachment attachment);

        void RemoveAttachmentForAttachmentBlock (McAttachment attachment);

        void ShowChooserForAttachmentBlock ();

        void ToggleCompactForAttachmentBlock ();
    }
}

