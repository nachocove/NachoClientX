//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcTimer
    {
        public Timer timer;
        public TimerCallback callback;
        public bool Stfu { get; set; }
        public int Id;

        private static int nextId = 0;
        // Used for increment critical section.
        private static Object nextIdLockObj = new Object ();
        // Used to prevent Dispose in the middle of a callback.
        private Object lockObj;

        private TimerCallback PartialInit (TimerCallback c)
        {
            lock (nextIdLockObj) {
                Id = ++nextId;
            }
            lockObj = new object ();
            callback = c;

            Log.Info (Log.LOG_TIMER, "NcTimer {0} created", Id);

            return state => {
                lock (lockObj) {
                    if (null == callback) {
                        Log.Info (Log.LOG_TIMER, "NcTimer {0} fired after Dispose.", Id);
                    } else {
                        if (!Stfu) {
                            Log.Info (Log.LOG_TIMER, "NcTimer {0} fired.", Id);
                        }
                        callback (state);
                    }
                }
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
            lock (lockObj) {
                timer.Dispose ();
                callback = null;
            }
            Log.Info (Log.LOG_TIMER, "NcTimer {0} disposed", Id);
        }
    }
}

