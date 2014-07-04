//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcTimer
    {
        /// The class we use to instantiate a system timer. The default is System.Threading.Timer.
        /// In unit test, it is replaced with MockTimer which allows us to simulate firing
        /// of timers.
        public static Type TimerClass = typeof(PlatformTimer);

        public ITimer Timer;
        public TimerCallback callback;
        public bool Stfu { get; set; }
        public string Who { get; set; }
        public int Id;

        private static int nextId = 0;
        private static List<NcTimer> ActiveTimers;
        private static Object StaticLockObj = new Object ();
        // Used to prevent Dispose in the middle of a callback.
        private Object InstanceLockObj;
        private bool HasFired = false;

        protected TimerCallback PartialInit (TimerCallback c)
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

            Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} created", Id, Who);

            return state => {
                lock (InstanceLockObj) {
                    if (null == callback) {
                        Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} fired after Dispose.", Id, Who);
                    } else {
                        if (!Stfu) {
                            Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} fired.", Id, Who);
                        }
                        callback (state);
                        HasFired = true;
                    }
                }
            };
        }

        public NcTimer (string who, TimerCallback c)
        {
            Who = who;
            Timer = (ITimer)Activator.CreateInstance (TimerClass, PartialInit (c));
        }

        public NcTimer (string who, TimerCallback c, Object o, Int32 i1, Int32 i2)
        {
            Who = who;
            Timer = (ITimer)Activator.CreateInstance (TimerClass, PartialInit (c), o, (Int32)i1, (Int32)i2);
        }

        public NcTimer (string who, TimerCallback c, Object o, Int64 i1, Int64 i2)
        {
            Who = who;
            Timer = (ITimer)Activator.CreateInstance (TimerClass, PartialInit (c), o, i1, i2);
        }

        public NcTimer (string who, TimerCallback c, Object o, TimeSpan t1, TimeSpan t2)
        {
            Who = who;
            Timer = (ITimer)Activator.CreateInstance (TimerClass, PartialInit (c), o, t1, t2);
        }

        public NcTimer (string who, TimerCallback c, Object o, UInt32 i1, UInt32 i2)
        {
            Who = who;
            Timer = (ITimer)Activator.CreateInstance (TimerClass, PartialInit (c), o, i1, i2);
        }

        public bool DisposeAndCheckHasFired ()
        {
            Dispose ();
            return HasFired;
        }

        public void Dispose ()
        {
            lock (StaticLockObj) {
                if (null != ActiveTimers.Find (nct => nct.Id == Id)) {
                    NcAssert.True (ActiveTimers.Remove (this));
                }
            }
            lock (InstanceLockObj) {
                Timer.Dispose ();
                callback = null;
            }
            Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} disposed", Id, Who);
        }

        public static void StopService ()
        {
            lock (StaticLockObj) {
                while (0 < ActiveTimers.Count) {
                    var timer = ActiveTimers [0];
                    Log.Warn (Log.LOG_TIMER, "NcTimer.Stop having to call Dispose() for {0}/{1}",
                        timer.Id, timer.Who);

                    // Dispose will do the remove.
                    timer.Dispose ();
                }
            }
        }
    }
}

