//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoEmailMessages
    {
        int Count ();

        void Refresh ();

        McEmailMessageThread GetEmailThread (int i);
    }
}
