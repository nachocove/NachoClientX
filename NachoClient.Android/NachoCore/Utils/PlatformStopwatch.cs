//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Diagnostics;

namespace NachoCore.Utils
{
    // Since System.Diagnostics.Stopwatch does not inherit from IStopwatch,
    // we need to create a class that wraps Stopwatch and exports the 
    // interface required by IStopwatch.
    public class PlatformStopwatch : IStopwatch
    {
        private Stopwatch Watch;

        public long ElapsedMilliseconds {
            get {
                return Watch.ElapsedMilliseconds;
            }
        }

        public PlatformStopwatch ()
        {
            Watch = new Stopwatch ();
        }

        public void Start ()
        {
            Watch.Start ();
        }

        public void Stop ()
        {
            Watch.Stop ();
        }

        public void Reset ()
        {
            Watch.Reset ();
        }
    }
}

