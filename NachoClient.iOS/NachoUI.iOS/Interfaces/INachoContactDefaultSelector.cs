//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using NachoCore.Model;
using NachoCore.Brain;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public interface INachoContactDefaultSelector
    {
        void ContactDefaultSelectorComposeMessage (string address);
    }
}

