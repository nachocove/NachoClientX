//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace Test.common
{
    public class MockStopwatch : IStopwatch
    {
        private static long _CurrentMillisecond;
        public static long CurrentMillisecond {
            get {
                return _CurrentMillisecond;
            }
        }

        private long StartMillisecond;
        private long _ElapsedMilliseconds;

        public long ElapsedMilliseconds {
            get {
                return _ElapsedMilliseconds;
            }
        }

        public MockStopwatch ()
        {
            StartMillisecond = -1;
            _ElapsedMilliseconds = 0;
        }

        public void Start ()
        {
            Console.WriteLine ("MockStopwatch: Start");
            StartMillisecond = CurrentMillisecond;
        }

        public void Stop ()
        {
            Console.WriteLine ("MockStopwatch: Stop");
            if (-1 == StartMillisecond) {
                return;
            }
            _ElapsedMilliseconds += CurrentMillisecond - StartMillisecond;
            StartMillisecond = -1;
        }

        public void Reset ()
        {
            _ElapsedMilliseconds = 0;
        }

        public static void AddTick (long msec)
        {
            _CurrentMillisecond += msec;
        }
    }
}

