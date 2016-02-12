//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcTimer : IDisposable
    {
        public delegate DateTime CurrentTimeFunction ();

        const long MSEC2TICKS = 10000L;
        const long MAX_DURATION = 40L * 86400L * 1000L;

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
        private static List<NcTimer> ActiveTimers = new List<NcTimer> ();
        private static Object StaticLockObj = new Object ();
        // Used to prevent Dispose in the middle of a callback.
        private Object InstanceLockObj;
        private bool HasFired = false;

        public static CurrentTimeFunction GetCurrentTime = DefaultGetCurrentTime;

        /// These are the original parameters from the caller. They are
        /// uninitialized (null) unless the due time or period is larger
        /// than 40 days.
        private DateTime DueTime;
        private Int64 Period;
        private object Object_;
        private TimerCallback WrappedCallback;

        public TimerCallback PartialInit (TimerCallback c)
        {
            lock (StaticLockObj) {
                Id = ++nextId;
                ActiveTimers.Add (this);
            }
            InstanceLockObj = new object ();
            callback = c;

            return state => {
                lock (InstanceLockObj) {
                    if (DateTime.MinValue < DueTime) {
                        DateTime now = GetCurrentTime ();
                        if (DueTime > now) {
                            // We are not done with the current due time. See how much time to go
                            Int64 due = (long)(DueTime - GetCurrentTime ()).TotalMilliseconds;
                            Int64 period = Period;
                            if (MAX_DURATION < due) {
                                // The remaining time is still too large. Fire another
                                // one-short MAX_DURATION timer.
                                due = MAX_DURATION;
                                period = Timeout.Infinite;
                            }
                            if (MAX_DURATION < period) {
                                period = Timeout.Infinite;
                            }

                            Timer.Dispose ();
                            Log.Debug (Log.LOG_TIMER, "callback set: due={0}, period={1}", due, period);
                            Timer = (ITimer)Activator.CreateInstance (TimerClass, WrappedCallback,
                                Object_, due, period);
                            return; // no callback yet
                        } else if (0 < Period) {
                            // We are past due time. See if the period is too large
                            Int64 due = Period;
                            Int64 period = Period;
                            if (MAX_DURATION < Period) {
                                DueTime = now + new TimeSpan (Period * MSEC2TICKS);
                                due = MAX_DURATION;
                                period = Timeout.Infinite;
                            } else {
                                // don't need these anymore
                                DueTime = DateTime.MinValue;
                                Period = 0;
                            }
                            Timer.Dispose ();
                            Log.Debug (Log.LOG_TIMER, "callback set2: due={0}, period={1}", due, period);
                            Timer = 
                                (ITimer)Activator.CreateInstance (TimerClass, WrappedCallback, 
                                Object_, due, period);
                        }
                    }
                    if (null == callback) {
                        Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} fired after Dispose.", Id, Who);
                    } else {
                        if (!Stfu) {
                            Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} fired.", Id, Who);
                        }
                        callback (state);
                        HasFired = true;
                        int dbCount = NachoCore.Model.NcModel.Instance.NumberDbConnections;
                        if (15 < dbCount) {
                            NachoCore.Model.NcModel.Instance.Db = null;
                            Log.Info(Log.LOG_SYS, "NcTimer {0}/{1} closing DB, connections: {2}", Id, Who, dbCount);
                        }
                    }
                }
            };
        }

        public static DateTime DefaultGetCurrentTime ()
        {
            return DateTime.Now;
        }

        private void Initialize (string who, TimerCallback c, object o)
        {
            Who = who;
            Object_ = o;
            WrappedCallback = PartialInit (c);
        }

        public NcTimer (string who, TimerCallback c)
        {
            Initialize (who, c, null);
            Change (Timeout.Infinite, Timeout.Infinite);
        }

        public NcTimer (string who, TimerCallback c, Object o, Int32 i1, Int32 i2)
        {
            Initialize (who, c, o);
            Change (i1, i2);
        }

        public NcTimer (string who, TimerCallback c, Object o, Int64 i1, Int64 i2)
        {
            Initialize (who, c, o);
            Change (i1, i2);
        }

        public NcTimer (string who, TimerCallback c, Object o, TimeSpan t1, TimeSpan t2)
        {
            Initialize (who, c, o);
            Change (t1, t2);
        }

        public NcTimer (string who, TimerCallback c, Object o, UInt32 i1, UInt32 i2)
        {
            Initialize (who, c, o);
            Change (i1, i2);
        }

        public bool Change (Int32 due, Int32 period)
        {
            return Change ((Int64)due, (Int64)period);
        }

        public bool Change (Int64 due, Int64 period)
        {
            lock (InstanceLockObj) {
                if (null != Timer) {
                    Timer.Dispose ();
                }
                DueTime = DateTime.MinValue;
                if ((MAX_DURATION < due) || (MAX_DURATION < period)) {
                    Log.Debug (Log.LOG_TIMER, "configured: due={0}, period={1}", due, period);
                    DueTime = GetCurrentTime () + new TimeSpan (due * MSEC2TICKS);
                    Period = period;

                    if (MAX_DURATION < due) {
                        // The due time is larger than the timer can handle. We save
                        // The real due time and period and create a one-shot timer
                        // fired in 40 days. If the remaining time is still larger
                        // than 40 days, the callback will create another one-shot
                        // timer until the remaining time is less than 40 days.
                        // The actual callback (to c) will not happen until then.
                        due = MAX_DURATION;
                        period = Timeout.Infinite;
                    }
                    if (MAX_DURATION < period) {
                        period = Timeout.Infinite;
                    }
                }
                Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} set: due={2}ms, period={3}", Id, Who, due, period);
                Timer = (ITimer)Activator.CreateInstance (TimerClass, WrappedCallback, Object_, due, period);

                return true;
            }
        }

        public bool Change (TimeSpan due, TimeSpan period)
        {
            return Change ((long)due.TotalMilliseconds, (long)period.TotalMilliseconds);
        }

        public bool Change (UInt32 due, UInt32 period)
        {
            return Change ((long)due, (long)period);
        }

        public bool IsExpired ()
        {
            return HasFired;
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
                if (null != Timer) {
                    Timer.Dispose ();
                }
                callback = null;
            }
            Log.Info (Log.LOG_TIMER, "NcTimer {0}/{1} disposed", Id, Who);
        }

        public static void StopService ()
        {
            Log.Info (Log.LOG_TIMER, "NcTimer: Stopping NCTimers...");
            lock (StaticLockObj) {
                Log.Info (Log.LOG_TIMER, "NcTimer: Active Timers Count {0}", ActiveTimers.Count);
                while (0 < ActiveTimers.Count) {
                    var timer = ActiveTimers [0];
                    Log.Warn (Log.LOG_TIMER, "NcTimer.Stop having to call Dispose() for {0}/{1}",
                        timer.Id, timer.Who);

                    // Dispose will do the remove.
                    timer.Dispose ();
                }
            }
            Log.Info (Log.LOG_TIMER, "NcTimer: Stopped all NCTimers.");
        }
    }
}

