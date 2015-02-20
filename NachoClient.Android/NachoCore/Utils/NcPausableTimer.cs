//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Diagnostics;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcPausableTimer : IDisposable
    {
        public static Type StopWatchClass = typeof(PlatformStopwatch);
        private IStopwatch Stopwatch;
        private Int64 RemainingTimeMsec; // # of milliseconds left
        private NcTimer Timer;

        private string Who;
        private TimerCallback Callback;
        private object Object_;
        private Int64 PeriodMsec;
        private object LockObj;

        private IStopwatch CreateStopWatch ()
        {
            return (IStopwatch)Activator.CreateInstance (StopWatchClass);
        }

        private void SaveParameters (string who, TimerCallback c, Object o, Int64 p)
        {
            Who = who;
            Callback = c;
            Object_ = o;
            PeriodMsec = p;
            LockObj = new object ();
        }

        public TimerCallback CallbackProxy (TimerCallback cb)
        {
            LockObj = new object ();
            return obj => {
                lock (LockObj) {
                    Stopwatch.Stop ();
                    if (null != cb) {
                        cb (obj);
                    }
                    if (0 < PeriodMsec) {
                        RemainingTimeMsec = PeriodMsec;
                        Stopwatch.Reset ();
                        Stopwatch.Start ();
                    } else {
                        RemainingTimeMsec = 0;
                    }
                }
            };
        }

        public NcPausableTimer (string who, TimerCallback cb)
        {
            TimerCallback proxiedCallback = CallbackProxy (cb);
            SaveParameters (who, proxiedCallback, null, Timeout.Infinite);
            Timer = new NcTimer (who, proxiedCallback);
            Stopwatch = CreateStopWatch ();
        }

        public NcPausableTimer (string who, TimerCallback cb, Object obj, Int32 due, Int32 period)
        {
            TimerCallback proxiedCallback = CallbackProxy (cb);
            SaveParameters (who, proxiedCallback, obj, period);
            Timer = new NcTimer (who, proxiedCallback, obj, due, period);
            Stopwatch = CreateStopWatch ();
            UpdateStopwatch (due);
        }

        public NcPausableTimer (string who, TimerCallback cb, Object obj, Int64 due, Int64 period)
        {
            TimerCallback proxiedCallback = CallbackProxy (cb);
            SaveParameters (who, proxiedCallback, obj, period);
            Timer = new NcTimer (who, proxiedCallback, obj, due, period);
            Stopwatch = CreateStopWatch ();
            UpdateStopwatch (due);
        }

        public NcPausableTimer (string who, TimerCallback cb, Object obj, TimeSpan due, TimeSpan period)
        {
            TimerCallback proxiedCallback = CallbackProxy (cb);
            SaveParameters (who, proxiedCallback, obj, (Int64)period.TotalMilliseconds);
            Timer = new NcTimer (who, proxiedCallback, obj, due, period);
            Stopwatch = CreateStopWatch ();
            UpdateStopwatch (due);
        }

        public NcPausableTimer (string who, TimerCallback cb, Object obj, UInt32 due, UInt32 period)
        {
            TimerCallback proxiedCallback = CallbackProxy (cb);
            SaveParameters (who, proxiedCallback, obj, period);
            Timer = new NcTimer (who, proxiedCallback, obj, due, period);
            Stopwatch = CreateStopWatch ();
            UpdateStopwatch (due);
        }

        private void StopAndReset ()
        {
            Stopwatch.Stop ();
            Stopwatch.Reset ();
        }

        private void UpdateStopwatch (Int64 due)
        {
            StopAndReset ();
            RemainingTimeMsec = Timeout.Infinite == due ? 0 : due;
            if (0 < RemainingTimeMsec) {
                Stopwatch.Start ();
            }
        }

        private void UpdateStopwatch (TimeSpan due)
        {
            StopAndReset ();
            RemainingTimeMsec = Timeout.InfiniteTimeSpan == due ? 0 : (Int64)due.TotalMilliseconds;
            if (0 < RemainingTimeMsec) {
                Stopwatch.Start();
            }
        }

        public bool Change (Int32 i1, Int32 i2)
        {
            bool success = Timer.Timer.Change (i1, i2);
            if (success) {
                UpdateStopwatch (i1);
            }
            return success;
        }

        public bool Change (Int64 i1, Int64 i2)
        {
            bool success = Timer.Timer.Change (i1, i2);
            if (success) {
                UpdateStopwatch (i1);
            }
            return success;
        }

        public bool Change (TimeSpan t1, TimeSpan t2)
        {
            bool success = Timer.Timer.Change (t1, t2);
            if (success) {
                UpdateStopwatch (t1);
            }
            return success;
        }

        public bool Change (UInt32 i1, UInt32 i2)
        {
            bool success = Timer.Timer.Change (i1, i2);
            if (success) {
                UpdateStopwatch (i1);
            }
            return success;
        }

        public void Pause ()
        {
            lock (LockObj) {
                if (0 < RemainingTimeMsec) {
                    Stopwatch.Stop ();
                    RemainingTimeMsec = (RemainingTimeMsec < Stopwatch.ElapsedMilliseconds ? 
                    0 : RemainingTimeMsec - Stopwatch.ElapsedMilliseconds);
                    Stopwatch.Reset ();
                }
            }
            Timer.Dispose ();
            Timer = null;
        }

        public void Resume ()
        {
            lock (LockObj) {
                if (0 < RemainingTimeMsec) {
                    Timer = new NcTimer (Who, Callback, Object_, RemainingTimeMsec, PeriodMsec);
                    Stopwatch.Start ();
                }
            }
        }

        public void Dispose ()
        {
            if (null != Timer) {
                Timer.Dispose ();
                Timer = null;
            }
        }
    }
}

