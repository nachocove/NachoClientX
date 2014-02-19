//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoContacts
    {
        int Count ();
        void Refresh ();
        McContact GetContact (int i);
    }
}