//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;

namespace Test.common
{
    public class MockStopwatch : IStopwatch
    {
        public static long Tick;

        private long StartTick;
        private long _ElapsedMilliseconds;

        public long ElapsedMilliseconds {
            get {
                return _ElapsedMilliseconds;
            }
        }

        public MockStopwatch ()
        {
            StartTick = -1;
            _ElapsedMilliseconds = 0;
        }

        public void Start ()
        {
            Console.WriteLine ("MockStopwatch: Start");
            StartTick = Tick;
        }

        public void Stop ()
        {
            Console.WriteLine ("MockStopwatch: Stop");
            if (-1 == StartTick) {
                return;
            }
            _ElapsedMilliseconds += Tick - StartTick;
            StartTick = -1;
        }

        public void Reset ()
        {
            _ElapsedMilliseconds = 0;
        }

        public static void AddTick (long msec)
        {
            Tick += msec;
        }
    }
}

