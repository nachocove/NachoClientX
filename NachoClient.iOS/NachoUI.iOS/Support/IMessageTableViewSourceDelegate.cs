//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;

namespace NachoClient.iOS
{
    public interface IMessageTableViewSourceDelegate
    {
        void PerformSegueForDelegate (string identifier, NSObject sender);

        void MultiSelectToggle (MessageTableViewSource source, bool enabled);
    }
}

