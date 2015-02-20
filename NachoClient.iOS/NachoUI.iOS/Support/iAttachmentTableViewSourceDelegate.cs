//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IAttachmentTableViewSourceDelegate
    {
        void PerformSegueForDelegate (string identifier, NSObject sender);
        void RemoveAttachment(McAttachment attachment);
    }
}

