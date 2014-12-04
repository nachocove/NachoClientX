//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IMessageTableViewSourceDelegate
    {
        void MessageThreadSelected (McEmailMessageThread thread);

        void PerformSegueForDelegate (string identifier, NSObject sender);

        void MultiSelectToggle (MessageTableViewSource source, bool enabled);

        void MultiSelectChange (MessageTableViewSource source, int count);
    }
}

