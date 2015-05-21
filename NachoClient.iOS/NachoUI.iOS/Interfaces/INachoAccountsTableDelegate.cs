//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public interface INachoAccountsTableDelegate
    {
        void AccountSelected(McAccount account);
    }
}
