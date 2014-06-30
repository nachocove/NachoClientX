﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public interface IUcAttachmentBlockDelegate
    {
        void AttachmentBlockNeedsLayout (UcAttachmentBlock view);

        void DisplayAttachmentForAttachmentBlock (McAttachment attachment);

        void PerformSegueForAttachmentBlock (string identifier, SegueHolder segueHolder);

        void PresentViewControllerForAttachmentBlock (UIViewController viewControllerToPresent, bool animated, NSAction completionHandler);
    }
}

