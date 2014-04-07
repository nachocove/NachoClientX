//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;

namespace NachoCore
{
    public interface INachoFolders
    {
        int Count();
        void Refresh();
        McFolder GetFolder (int i);
    }
}
