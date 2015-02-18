//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

namespace NachoCore.Utils
{
    // An interface for both the real and the mock version. This interface does not 
    // define all properties and methods for StopWatch. Just the ones we use. This
    // interface is used for MockStopWatch and PlatformStopwatch
    public interface IStopwatch
    {
        long ElapsedMilliseconds { get; }

        void Start ();

        void Stop ();

        void Reset ();
    }
}

