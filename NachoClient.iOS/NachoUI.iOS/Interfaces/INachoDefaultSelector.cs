//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Brain;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public interface INachoDefaultSelector
    {
        void PerformSegueForDefaultSelector (string identifier, NSObject sender);
    }
}

