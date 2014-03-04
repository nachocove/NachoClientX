//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcTimer
    {
        Timer timer;
        TimerCallback callback;
        static int nextId = 0;
        static Object nextIdLockObj = new Object ();
        static int id;

        private TimerCallback PartialInit (TimerCallback c)
        {
            lock (nextIdLockObj) {
                id = ++nextId;
            }
            callback = c;

            Log.Info (Log.LOG_TIMER, "NcTimer {0} created", id);

            return state => {
                Log.Info (Log.LOG_TIMER, "NcTimer {0} fired", id);
                callback (state);
            };
        }

        public NcTimer (TimerCallback c)
        {
            timer = new Timer (PartialInit (c));
        }

        public NcTimer (TimerCallback c, Object o, Int32 i1, Int32 i2)
        {
            timer = new Timer (PartialInit (c), o, i1, i2);
        }

        public NcTimer (TimerCallback c, Object o, Int64 i1, Int64 i2)
        {
            timer = new Timer (PartialInit (c), o, i1, i2);
        }

        public NcTimer (TimerCallback c, Object o, TimeSpan t1, TimeSpan t2)
        {
            timer = new Timer (PartialInit (c), o, t1, t2);
        }

        public NcTimer (TimerCallback c, Object o, UInt32 i1, UInt32 i2)
        {
            timer = new Timer (PartialInit (c), o, i1, i2);
        }

        public void Dispose ()
        {
            Log.Info (Log.LOG_TIMER, "NcTimer {0} disposed", id);
            timer.Dispose ();
        }
    }
}

