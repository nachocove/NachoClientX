//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    public interface ITimer : IDisposable
    {
        bool Change (Int32 due, Int32 period);

        bool Change (Int64 due, Int64 period);

        bool Change (TimeSpan due, TimeSpan period);

        bool Change (UInt32 due, UInt32 period);
    }
}

