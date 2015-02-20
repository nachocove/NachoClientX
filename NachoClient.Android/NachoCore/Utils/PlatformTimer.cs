//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public class PlatformTimer : ITimer
    {
        private Timer Timer_;

        public PlatformTimer (TimerCallback cb)
        {
            Timer_ = new Timer (cb);
        }

        public PlatformTimer (TimerCallback cb, Object obj, Int64 due, Int64 period)
        {
            Timer_ = new Timer (cb, obj, due, period);
        }

        public PlatformTimer (TimerCallback cb, Object obj, TimeSpan due, TimeSpan period)
        {
            Timer_ = new Timer (cb, obj, due, period);
        }

        public PlatformTimer (TimerCallback cb, Object obj, UInt32 due, UInt32 period)
        {
            Timer_ = new Timer (cb, obj, due, period);
        }

        public bool Change (Int32 due, Int32 period)
        {
            return Timer_.Change (due, period);
        }

        public bool Change (Int64 due, Int64 period)
        {
            return Timer_.Change (due, period);
        }

        public bool Change (TimeSpan due, TimeSpan period)
        {
            return Timer_.Change (due, period);
        }

        public bool Change (UInt32 due, UInt32 period)
        {
            return Timer_.Change (due, period);
        }

        public void Dispose ()
        {
            Timer_.Dispose ();
        }
    }
}
