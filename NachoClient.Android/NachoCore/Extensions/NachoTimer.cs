//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public class NachoTimer
    {
        Timer timer; 

        static int nextId = 0;
        static int id;

        public NachoTimer(TimerCallback c, Object o, Int32 i1, Int32 i2)
        {
            id = ++nextId;
            Log.Info(Log.LOG_TIMER, "NachoTimer {0} created", id);
            timer = new Timer(c, o, i1, i2);
        }

        public NachoTimer(TimerCallback c, Object o, Int64 i1, Int64 i2)
        {
            id = ++nextId;
            Log.Info(Log.LOG_TIMER, "NachoTimer {0} created", id);
            timer = new Timer(c, o, i1, i2);
        }

        public NachoTimer(TimerCallback c, Object o, TimeSpan t1, TimeSpan t2)
        {
            id = ++nextId;
            Log.Info(Log.LOG_TIMER, "NachoTimer {0} created", id);
            timer = new Timer(c, o, t1, t2);
        }

        public NachoTimer(TimerCallback c, Object o, UInt32 i1, UInt32 i2)
        {
            id = ++nextId;
            Log.Info(Log.LOG_TIMER, "NachoTimer {0} created", id);
            timer = new Timer(c, o, i1, i2);
        }

        public void Dispose()
        {
            Log.Info(Log.LOG_TIMER, "NachoTimer {0} disposed", id);
            timer.Dispose ();
        }
    }
}

