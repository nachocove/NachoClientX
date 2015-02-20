//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using NachoCore.Utils;

namespace Test.Common
{
    public class MockTimer : ITimer, IComparable
    {
        public class TimerList
        {
            private SortedList<Int64, HashSet<MockTimer>> TimeSlots;

            public TimerList ()
            {
                TimeSlots = new SortedList<long, HashSet<MockTimer>>();
            }

            public HashSet<MockTimer> FindTimeSlot (Int64 time)
            {
                HashSet<MockTimer> timerList;
                bool found = TimeSlots.TryGetValue (time, out timerList);
                if (!found) {
                    timerList = new HashSet<MockTimer> ();
                    TimeSlots.Add (time, timerList);
                }
                return timerList;
            }

            public HashSet<MockTimer> RemoveTimeSlot (Int64 time)
            {
                HashSet<MockTimer> timerList = FindTimeSlot (time);
                if (null != timerList) {
                    bool removed = TimeSlots.Remove (time);
                    NcAssert.True (removed);
                }
                return timerList;
            }

            public bool Add (MockTimer timer)
            {
                HashSet<MockTimer> timerList = FindTimeSlot (timer.DueTime);
                return timerList.Add (timer);
            }

            public bool Remove (MockTimer timer)
            {
                HashSet<MockTimer> timerList = FindTimeSlot (timer.DueTime);
                bool removed = timerList.Remove (timer);
                if (0 == timerList.Count) {
                    TimeSlots.Remove (timer.DueTime);
                }
                return removed;
            }

            public Int64 NextTimeSlot ()
            {
                if (0 == TimeSlots.Count) {
                    return -1;
                }
                return TimeSlots.Keys [0];
            }

            public void Clear ()
            {
                while (0 < TimeSlots.Count) {
                    HashSet<MockTimer> timeSlot;
                    TimeSlots.TryGetValue (TimeSlots.Keys[0], out timeSlot);
                    TimeSlots.RemoveAt (0);
                    Log.Info (Log.LOG_TEST, "Removing {0} timers", timeSlot.Count);
                    timeSlot.Clear ();
                }
            }
        }

        // Keep track of all timers that will fire in the future
        private static TimerList _ActiveList;
        public static TimerList ActiveList {
            get {
                if (null == _ActiveList) {
                    _ActiveList = new TimerList ();
                }
                return _ActiveList;
            }
        }

        // Current time (in milliseconds)
        private static object _CurrentTimeLock;
        private static object CurrentTimeLock {
            get {
                if (null == _CurrentTimeLock) {
                    _CurrentTimeLock = new object ();
                }
                return _CurrentTimeLock;
            }
        }

        private static Int64 _CurrentTime;
        public static Int64 CurrentTime {
            get {
                return _CurrentTime;
            }
            set {
                NcAssert.True (value > _CurrentTime);
                // This is a hack to simulate the right behavior. The unit test
                // thread sets the mock system time asynchronously to the callback
                // thread. This is actually how the real system works as well. However,
                // it can advance the time so fast that the pausable timer stopwatch
                // reads a time far more ahead than expected. In real situation, this
                // does not cause any problem. It just means the expiry may be a bit
                // delayed (by a small amount where "small" ~ 10s of msec). But in our
                // control environment, it will lead to the timer not firing when expected
                // and the test will be stuck. So, we lock current time when callbacks
                // are made.
                lock (CurrentTimeLock) {
                    Int64 nextSlot = ActiveList.NextTimeSlot ();
                    if ((0 > nextSlot) || (nextSlot > value)) {
                        _CurrentTime = value;
                        MockStopwatch.CurrentMillisecond = value;
                    } else {
                        // Fire all timer in between
                        _CurrentTime = nextSlot;
                        MockStopwatch.CurrentMillisecond = nextSlot;
                        Signal.Set ();
                    }
                }
            }
        }

        public static DateTime CurrentDateTime;

        static Thread CallbackThread;
        static CancellationTokenSource Cancellation;
        static AutoResetEvent Signal;

        Int64 _DueTime;
        public Int64 DueTime {
            get {
                return _DueTime;
            }
            set {
                if (Timeout.Infinite == value) {
                    _DueTime = value; // no adjust if it is infinite
                } else {
                    _DueTime = _CurrentTime + value; // otherwise add current time
                }
            }
        }
        public Int64 PeriodTime;
        public TimerCallback Callback;
        Object Object_;

        private void Initialize (TimerCallback cb, Object obj)
        {
            Callback = cb;
            Object_ = obj;
            DueTime = Timeout.Infinite;
            PeriodTime = Timeout.Infinite;
        }

        public MockTimer (TimerCallback cb)
        {
            Initialize (cb, null);
        }

        public MockTimer (TimerCallback cb, Object obj, Int64 due, Int64 period)
        {
            Initialize (cb, obj);
            Change (due, period);
        }

        public MockTimer (TimerCallback cb, Object obj, TimeSpan due, TimeSpan period)
        {
            Initialize (cb, obj);
            Change (due, period);
        }

        private bool IsDifferent (Int64 due, Int64 period)
        {
            if (DueTime != due) {
                return true;
            }
            if (PeriodTime != period) {
                return true;
            }
            return false;
        }

        private bool ChangeInternal (Int64 due, Int64 period)
        {
            /// Mimick real System.Threading.Timer's limited range
            if ((-1 > due) || ((long)UInt32.MaxValue <= due) ||
                (-1 > period) || ((long)UInt32.MaxValue <= period)) {
                throw new ArgumentOutOfRangeException ();
            }

            if (!IsDifferent (due, period)) {
                return false;
            }
            if (0 <= DueTime) {
                bool removed = ActiveList.Remove (this);
                NcAssert.True (removed);
            }
            DueTime = due;
            PeriodTime = period;
            if (0 <= DueTime) {
                bool added = ActiveList.Add (this);
                NcAssert.True (added);
                if (0 == due) {
                    // Due time of 0 means immediately fire the timer.
                    Signal.Set ();
                }
            }
            return true;
        }

        private void ChangePeriodic ()
        {
            if (0 >= PeriodTime) {
                return; // not periodic
            }
            DueTime = PeriodTime;
            bool added = ActiveList.Add (this);
            NcAssert.True (added);
        }

        public bool Change (Int32 due, Int32 period)
        {
            return ChangeInternal (due, period);
        }

        public bool Change (Int64 due, Int64 period)
        {
            return ChangeInternal (due, period);
        }

        public bool Change (TimeSpan due, TimeSpan period)
        {
            return ChangeInternal ((Int64)due.TotalMilliseconds,
                (Int64)period.TotalMilliseconds);
        }

        public bool Change (UInt32 due, UInt32 period)
        {
            return ChangeInternal (due, period);
        }

        public int CompareTo (Object obj)
        {
            MockTimer other = obj as MockTimer;
            return DueTime.CompareTo (other.DueTime);
        }

        public void Dispose ()
        {
            if (0 <= DueTime) {
                ActiveList.Remove (this);
            }
        }

        public static void Start ()
        {
            CurrentDateTime = new DateTime (1, 1, 1, 0, 0, 0);
            NcTimer.TimerClass = typeof(MockTimer);
            NcTimer.GetCurrentTime = GetCurrentDateTime;

            CallbackThread = new Thread (CallbackLoop);
            Cancellation = new CancellationTokenSource ();
            Signal = new AutoResetEvent (false);
            CallbackThread.Start (Cancellation.Token);
            _CurrentTime = 0;
            MockStopwatch.CurrentMillisecond = 0;
        }

        public static void Stop ()
        {
            Cancellation.Cancel ();
            Signal.Set ();
            ActiveList.Clear ();

            NcTimer.TimerClass = typeof(PlatformTimer);
            NcTimer.GetCurrentTime = NcTimer.DefaultGetCurrentTime;
        }

        public static void CallbackLoop (object obj)
        {
            CancellationToken c = (CancellationToken)obj;
            while (!c.IsCancellationRequested) {
                // Wait for a signal or a cancellation
                Signal.WaitOne ();
                if (c.IsCancellationRequested) {
                    break;
                }

                // Find the set of timer at a particular time slot
                lock (CurrentTimeLock) {
                    HashSet<MockTimer> timerList = ActiveList.RemoveTimeSlot (CurrentTime);
                    if ((null == timerList) || (0 == timerList.Count)) {
                        continue;
                    }

                    // Make callbacks for all timers in the time slot and reschedule periodic timers
                    foreach (MockTimer timer in timerList) {
                        NcAssert.True (null != timer.Callback);
                        timer.ChangePeriodic ();
                        timer.Callback (timer.Object_);
                    }
                }
            }
        }

        /// This function guarantees that any ongoing timer callbacks are 
        /// finished upon returning from this call. Note that you need to
        /// make sure that there is an ongoing callback. This can be done
        /// by signalling 
        public static int WaitForCallBack ()
        {
            lock (CurrentTimeLock) {
                /// Do some useless work to make sure the optimizer cannot 
                /// get rid of this code.
                return new Random ().Next ();
            }
        }

        public static DateTime GetCurrentDateTime ()
        {
            return CurrentDateTime;
        }
    }
}

