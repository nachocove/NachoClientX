//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoFiles
    {
        int Count ();
        McFile GetFile (int i);
    }
}
