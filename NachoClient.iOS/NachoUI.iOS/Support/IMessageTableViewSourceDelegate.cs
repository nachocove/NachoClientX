//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoClient.iOS
{
    public interface IMessageTableViewSourceDelegate
    {
        void MultiSelectToggle (MessageTableViewSource source, bool enabled);
    }
}

