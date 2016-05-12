//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface IAccountSwitching
    {
        void SwitchToAccount (McAccount account);
    }
}

