//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
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
        private static List<NcTimer> ActiveTimers;
        private static Object StaticLockObj = new Object ();
        // Used to prevent Dispose in the middle of a callback.
        private Object InstanceLockObj;

        private TimerCallback PartialInit (TimerCallback c)
        {
            lock (StaticLockObj) {
                Id = ++nextId;
                if (null == ActiveTimers) {
                    ActiveTimers = new List<NcTimer> ();
                }
                ActiveTimers.Add (this);
            }
            InstanceLockObj = new object ();
            callback = c;

            Log.Info (Log.LOG_TIMER, "NcTimer {0} created", Id);

            return state => {
                lock (InstanceLockObj) {
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
            lock (StaticLockObj) {
                if (null != ActiveTimers.Find (nct => nct.Id == Id)) {
                    NachoAssert.True (ActiveTimers.Remove (this));
                }
            }
            lock (InstanceLockObj) {
                timer.Dispose ();
                callback = null;
            }
            Log.Info (Log.LOG_TIMER, "NcTimer {0} disposed", Id);
        }

        public static void Stop ()
        {
            lock (StaticLockObj) {
                if (0 < ActiveTimers.Count) {
                    Log.Warn (Log.LOG_TIMER, "NcTimer.Stop having to call Dispose(): ...");
                }
                while (0 < ActiveTimers.Count) {
                    var timer = ActiveTimers [0];
                    // Dispose will do the remove.
                    timer.Dispose ();
                }
            }
        }
    }
}

