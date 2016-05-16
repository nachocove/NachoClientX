//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    /// <summary>
    /// NcTimerPoolTimerId holds the fields that deteremines the order of
    /// timer pool timers. The primary key is the (absolute) due time.
    /// In case there are more than one timer due at a particular time,
    /// the tie is broken by the globally unique timer pool timer id.
    /// </summary>
    public class NcTimerPoolTimerId : IComparable
    {
        private static uint NextId = 1;

        public DateTime DueTime;

        public uint Id { get; protected set; }

        public NcTimerPoolTimerId ()
        {
            Id = NextId;
            NextId++;
        }

        public int CompareTo (object obj)
        {
            NcTimerPoolTimerId other = obj as NcTimerPoolTimerId;
            if (DueTime < other.DueTime) {
                return -1;
            }
            if (DueTime > other.DueTime) {
                return +1;
            }
            return Id.CompareTo (other.Id);
        }
    }

    public class NcTimerPoolTimer : NcTimerPoolTimerId, ITimer
    {
        const long MSEC2TICKS = 10000L;

        private NcTimerPool Pool;

        public string Name { get; private set; }

        public TimeSpan Period { get; private set; }

        private TimerCallback Callback;
        object Object_;

        private void Initialize (NcTimerPool pool, string name, TimerCallback callback, object obj)
        {
            Pool = pool;
            Name = name;
            Callback = callback;
            Object_ = obj;
        }

        public NcTimerPoolTimer (NcTimerPool pool, string name,
                                 TimerCallback callback, object obj, Int32 due, Int32 period) : base ()
        {
            Initialize (pool, name, callback, obj);
            Change (due, period);
        }

        public NcTimerPoolTimer (NcTimerPool pool, string name,
                                 TimerCallback callback, object obj, Int64 due, Int64 period) : base ()
        {
            Initialize (pool, name, callback, obj);
            Change (due, period);
        }

        public NcTimerPoolTimer (NcTimerPool pool, string name,
                                 TimerCallback callback, object obj, TimeSpan due, TimeSpan period) : base ()
        {
            Initialize (pool, name, callback, obj);
            Change (due, period);
        }

        public NcTimerPoolTimer (NcTimerPool pool, string name,
                                 TimerCallback callback, object obj, UInt32 due, UInt32 period) : base ()
        {
            Initialize (pool, name, callback, obj);
            Change (due, period);
        }

        public void Dispose ()
        {
            Pool.TryRemove (this);
            Period = new TimeSpan (0);
            DueTime = DateTime.MinValue;
        }

        public static DateTime SafeDateTimeAdd (DateTime time, TimeSpan duration)
        {
            try {
                return time + duration;
            } catch (ArgumentOutOfRangeException) {
                /// Instead of throw exceptions all over, if we ever exceed
                /// the range of DateTime, just clip the returned value to
                /// DateTime.MaxValue.
                return DateTime.MaxValue;
            }
        }

        public bool Change (Int32 due, Int32 period)
        {
            return Change ((long)due, (long)period);
        }

        public bool Change (Int64 due, Int64 period)
        {
            return Change (new TimeSpan (due * MSEC2TICKS), new TimeSpan (period * MSEC2TICKS));
        }

        public bool Change (TimeSpan due, TimeSpan period)
        {
            DueTime = SafeDateTimeAdd (NcTimer.GetCurrentTime (), due);
            Period = period;
            Pool.Add (this);
            return true;
        }

        public bool Change (UInt32 due, UInt32 period)
        {
            return Change ((long)due, (long)period);
        }

        public void MakeCallback ()
        {
            NcAssert.True (null != Callback);
            Callback (Object_);
        }

        public override string ToString ()
        {
            return string.Format ("[NcTimerPoolTimer: Name={0}, Id={1}, DueTime={2}]", Name, Id, DueTime);
        }
    }

    public class NcTimerPool : IDisposable
    {
        public delegate DateTime CurrentTimeFunction ();

        public string Description { get; private set; }

        public CancellationToken Token;

        public DateTime NextDueTime { get; private set; }

        private NcTimer RealTimer;
        private SortedDictionary<NcTimerPoolTimerId, NcTimerPoolTimer> ChildrenTimers;
        private object LockObj;

        public int ActiveCount {
            get {
                lock (LockObj) {
                    return ChildrenTimers.Count;
                }
            }
        }

        public NcTimerPool (string description)
        {
            Description = description;
            LockObj = new object ();
            ChildrenTimers = new SortedDictionary<NcTimerPoolTimerId, NcTimerPoolTimer> ();
        }

        private void StopRealTimer ()
        {
            if (null != RealTimer) {
                RealTimer.Dispose ();
                RealTimer = null;
            }
        }

        private void StartTimer ()
        {
            StopRealTimer ();
            if (0 == ChildrenTimers.Count) {
                return; // no more active timer. just stop
            }
            NcTimerPoolTimerId minId = ChildrenTimers.Keys.Min ();
            DateTime now = NcTimer.GetCurrentTime ();
            TimeSpan duration = new TimeSpan ();
            if (minId.DueTime >= now) {
                duration = minId.DueTime - now;
            }
            RealTimer = new NcTimer (Description, Callback, Token, duration, Timeout.InfiniteTimeSpan);
        }

        public override string ToString ()
        {
            string result = "";
            foreach (NcTimerPoolTimer t in ChildrenTimers.Values) {
                result += String.Format ("{0}: {1}\n", t.DueTime, t.Id);
            }
            return result;
        }

        public void Dispose ()
        {
            lock (LockObj) {
                StopRealTimer ();
                ChildrenTimers.Clear ();
            }

        }

        public void Add (NcTimerPoolTimer timer)
        {
            lock (LockObj) {
                bool minTimeChanged = true;
                if (0 < ChildrenTimers.Count) {
                    /// If there is at least one active timer and the new timer is 
                    /// fired sooner than the current minimum, do not reset timer
                    NcTimerPoolTimerId minId = ChildrenTimers.Keys.Min ();
                    minTimeChanged = timer.DueTime < minId.DueTime;
                }
                ChildrenTimers.Add (timer, timer);
                if (minTimeChanged) {
                    NextDueTime = timer.DueTime;
                    StartTimer ();
                }
            }
        }

        public NcTimerPoolTimer Find (NcTimerPoolTimerId timerId)
        {
            NcTimerPoolTimer timer;
            if (!ChildrenTimers.TryGetValue (timerId, out timer)) {
                return null;
            }
            return timer;
        }

        public void Remove (NcTimerPoolTimer timer)
        {
            lock (LockObj) {
                if (0 == ChildrenTimers.Count) {
                    return; // nothing to remove
                }

                if (!ChildrenTimers.Remove (timer)) {
                    Log.Warn (Log.LOG_UTILS, "Cannot remove {0}", timer.ToString ());
                }

                bool minTimeChanged = true;
                if (0 < ChildrenTimers.Count) {
                    NcTimerPoolTimerId minId = ChildrenTimers.Keys.Min ();
                    minTimeChanged = timer.DueTime < minId.DueTime;
                }
                if (minTimeChanged) {
                    StartTimer ();
                }
            }
        }

        public void TryRemove (NcTimerPoolTimer timer)
        {
            lock (LockObj) {
                if (null != Find (timer)) {
                    Remove (timer);
                }
            }
        }

        public void Pause ()
        {
            lock (LockObj) {
                StopRealTimer ();
            }
        }

        public void Resume ()
        {
            lock (LockObj) {
                if (0 < ChildrenTimers.Count) {
                    StartTimer ();
                }
            }
        }

        public void Callback (object obj)
        {
            CancellationToken c = (CancellationToken)obj;
            lock (LockObj) {
                DateTime now = NcTimer.GetCurrentTime ();
                NcTimerPoolTimerId minId = ChildrenTimers.Keys.Min ();
                while ((null != minId) && (minId.DueTime <= now)) {
                    if (c.IsCancellationRequested) {
                        return;
                    }

                    // Remove the timer from active list
                    NcTimerPoolTimer timer;
                    bool found = ChildrenTimers.TryGetValue (minId, out timer);
                    NcAssert.True (found);
                    bool removed = ChildrenTimers.Remove (timer);
                    NcAssert.True (removed);

                    // Make callback
                    timer.MakeCallback ();

                    // Schedule the timer if it is periodic
                    if (new TimeSpan (0) < timer.Period) {
                        timer.DueTime = NcTimerPoolTimer.SafeDateTimeAdd (NcTimer.GetCurrentTime (), timer.Period);
                        ChildrenTimers.Add (timer, timer);
                    }

                    // Get the id of the next timer to expire
                    minId = ChildrenTimers.Keys.Min ();
                }
                StartTimer ();
            }
        }
    }
}

