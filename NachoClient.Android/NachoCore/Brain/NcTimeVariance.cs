//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Linq;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    /// Time variance type. Note that each enum is assigned an integer value.
    /// The value must never replaced or reused as it will create migration
    /// issue.
    public enum NcTimeVarianceType
    {
        NONE = 0,
        DONE = 1,
        DEADLINE = 2,
        DEFERENCE = 3,
        AGING = 4,
        MEETING = 5,
    }

    /// Time variance state machines keep track of temporal variatiional
    /// aspect of the score of all scorable objects.
    ///
    /// These state machines have simple state transitions. Each has a 
    /// single linearly path. The state is represented by an integer.
    /// (Actual implementation can use enum for state and convert them
    /// to and from integers.)
    /// 
    /// These state machines are implemented as objects of a different
    /// class instead of being inside the objects to which they belong 
    /// because not all objects in the model are brought into memory.
    /// We may have 100 email messages with different deadlines.
    /// These McEmailMessage objects will not be kept in memory all the
    /// time but their state machine must run.
    public class NcTimeVariance : IDisposable
    {
        public const int STATE_TERMINATED = 0;
        public const int STATE_NONE = -1;

        public delegate DateTime CurrentDateTimeFunction ();

        public delegate void TimeVarianceCallBack (int state, Int64 objId);

        public static CurrentDateTimeFunction GetCurrentDateTime = PlatformGetCurrentDateTime;

        public NcTimeVarianceType Type { get; protected set; }

        protected DateTime StartTime;

        protected virtual List<TimeSpan> TimeOffsets {
            get {
                throw new NotImplementedException ();
            }
        }

        protected virtual List<double> Factors {
            get {
                throw new NotImplementedException ();
            }
        }

        /// A list of all active Time variance state machine
        protected static TimeVarianceTable _ActiveList;

        public static TimeVarianceTable ActiveList {
            get {
                if (null == _ActiveList) {
                    _ActiveList = new TimeVarianceTable ();
                }
                return _ActiveList;
            }
        }

        protected static NcTimerPool _TimerPool;

        public static NcTimerPool TimerPool {
            get {
                if (null == _TimerPool) {
                    _TimerPool = new NcTimerPool ("NcTimeVariance");
                }
                return _TimerPool;
            }
        }

        private object LockObj { set; get; }

        /// State is just an integer that increments. Note that state 0 is
        /// reserved for terminated state.
        private int _State { get; set; }

        public int State {
            get {
                return _State;
            }
        }

        // The largest non-zero state. Advancing from this state goes to 0.
        public int MaxState {
            get {
                return TimeOffsets.Count - 1;
            }
        }

        // The timer for keeping track of when the next event occurs.
        // In order to conserve memory, we only create the timer if
        // needed.
        private NcTimerPoolTimer EventTimer;

        public bool IsRunning {
            get {
                return (null != EventTimer);
            }
        }

        public string Description { get; protected set; }

        public TimeVarianceCallBack CallBackFunction;

        public Int64 CallBackId;

        /// <summary>
        /// This method must be overridden for each derived class. It provides the amount
        /// of adjustment (from 0.0 to 1.0) for each state.
        /// </summary>
        /// <param name="state">State.</param>
        public double Adjustment (int state)
        {
            if (state > MaxState) {
                throw new IndexOutOfRangeException (GetType ().Name);
            }
            return Factors [state];
        }

        /// <summary>
        /// This method must be overridden for each derived class. It provides the
        /// time when this current state ends.
        /// </summary>
        /// <returns>The event time.</returns>
        /// <param name="state">State.</param>
        protected virtual DateTime NextEventTime (int state)
        {
            if (MaxState < state) {
                throw new IndexOutOfRangeException (GetType ().Name);
            }
            if (0 == state) {
                return DateTime.MinValue;
            }
            return StartTime + TimeOffsets [state];
        }

        public static DateTime PlatformGetCurrentDateTime ()
        {
            return DateTime.UtcNow;
        }

        public override string ToString ()
        {
            return String.Format ("[{0}: {1}]", GetType ().Name, Description);
        }

        private void Initialize ()
        {
            _State = -1;
        }

        public NcTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId, NcTimeVarianceType type, DateTime startTime)
        {
            Description = description;
            LockObj = new object ();
            CallBackFunction = callback;
            CallBackId = objId;
            Type = type;
            StartTime = startTime;
            Initialize ();
        }

        private string TimerDescription ()
        {
            return ToString ();
        }

        public void Pause ()
        {
            lock (LockObj) {
                Log.Debug (Log.LOG_BRAIN, "{0}: pausing", ToString ());
                StopTimer ();
            }
        }

        /// Normally, a time variance object is internally cleaned up when it
        /// goes to state 0. However, one should call this method to
        /// abnormally terminates a time variance object. One example of this
        /// when a NcTimeVarianceTest case fails and it needs to clean up
        /// before the next test case.
        public void Dispose ()
        {
            lock (LockObj) {
                StopTimer ();
                ActiveList.Remove (this);
            }
        }

        private void StartTimer (DateTime now)
        {
            DateTime eventTime = NextEventTime ();
            long dueTime = (long)(eventTime - now).TotalMilliseconds;
            Log.Debug (Log.LOG_BRAIN, "{0}: start timer {1} {2}", ToString (), now, eventTime);
            EventTimer = new NcTimerPoolTimer (TimerPool, TimerDescription (), AdvanceCallback, this,
                dueTime, Timeout.Infinite);
        }

        private void StopTimer ()
        {
            if (null != EventTimer) {
                EventTimer.Dispose ();
                EventTimer = null;
            }
        }

        private void Run ()
        {
            lock (LockObj) {
                DateTime now = GetCurrentDateTime ();

                bool advanced = FindNextState (now);
                if (!advanced) {
                    StopTimer ();
                    StartTimer (now);
                    return;
                }

                /// Make callback if provided
                if (null != CallBackFunction) {
                    CallBackFunction (State, CallBackId);
                }

                /// Get rid of the old timer if exists
                StopTimer ();

                /// Start a new timer unless it is terminated
                if (STATE_TERMINATED != State) {
                    StartTimer (now);
                } else {
                    Log.Debug (Log.LOG_BRAIN, "{0}: terminated", ToString ());
                    ActiveList.Remove (this);
                }
            }
        }

        public void Resume ()
        {
            Log.Debug (Log.LOG_BRAIN, "{0}: resuming", ToString ());
            Run ();
        }

        public virtual void Start ()
        {
            Log.Debug (Log.LOG_BRAIN, "{0}: starting", ToString ());
            ActiveList.Add (this);
            Run ();
        }

        public static void StopList (string description)
        {
            NcTimeVarianceList tvList;
            if (ActiveList.RemoveList (description, out tvList)) {
                foreach (NcTimeVariance tv in tvList) {
                    tv.StopTimer ();
                }
            }
        }

        public static void AdvanceCallback (object obj)
        {
            NcTimeVariance tv = obj as NcTimeVariance;
            tv.Run ();
        }

        private int AdvanceState (int state)
        {
            NcAssert.True (state <= MaxState);
            if (STATE_NONE == state) {
                state = 1;
            } else if (MaxState == state) {
                state = STATE_TERMINATED;
            } else {
                state++;
            }
            return state;
        }

        /// <summary>
        /// Find the next state given the current time
        /// </summary>
        /// <returns><c>true</c>, if state is advanced <c>false</c> otherwise.</returns>
        public int FindNextState (DateTime now, int state)
        {
            if (STATE_TERMINATED == state) {
                return state;
            }
            if (STATE_NONE == state) {
                state = 1;
            }
            while (now >= NextEventTime (state)) {
                state = AdvanceState (state);
                if (STATE_TERMINATED == state) {
                    break;
                }
            }
            return state;
        }

        private bool FindNextState (DateTime now)
        {
            int origState = _State;
            _State = FindNextState (now, _State);
            Log.Debug (Log.LOG_BRAIN, "{0}: state {1} -> state {2}", ToString (), origState, _State);
            return (origState != _State);
        }

        protected DateTime NextEventTime ()
        {
            return NextEventTime (State);
        }

        protected DateTime LimitEventTime (DateTime time)
        {
            /// Limit the event time to 9999/1/1 12:00:00AM. The reason for this
            /// is to avoid a potential overflow exception. 'now' was timestamped
            /// some time ago. So, the duration from 'now' to eventTime starting
            /// at the current time may actually beyond DateTime.MaxValue.
            ///
            /// The solution is to cap event time to 9999/1/1 12:00:00AM. This gives
            /// one year of slack between 'now' and the real current time. It is
            /// okay to give a false event time since the timer will not fire
            /// with or without adjustment for 7,000+ years. And we don't care
            /// what happens 7 millennium later.
            DateTime maxDueTime = new DateTime (9999, 1, 1, 0, 0, 0);
            if (time > maxDueTime) {
                return maxDueTime;
            }
            return time;
        }

        public double Adjustment (DateTime now)
        {
            int state = FindNextState (now, -1);
            return Adjustment (state);
        }

        public double Adjustment ()
        {
            return Adjustment (State);
        }

        public DateTime LastEventTime ()
        {
            return NextEventTime (MaxState);
        }

        public double LastAdjustment ()
        {
            return Adjustment (MaxState);
        }

        protected DateTime SafeAddDateTime (DateTime time, TimeSpan duration)
        {
            DateTime retval;
            try {
                retval = time + duration;
            } catch (ArgumentOutOfRangeException) {
                retval = time;
            }
            return retval;
        }

        public bool ShouldRun (DateTime now)
        {
            return (LastEventTime () > now);
        }

        public static void PauseAll ()
        {
            TimerPool.Pause ();
        }

        public static void ResumeAll ()
        {
            TimerPool.Resume ();
        }
    }

    public class NcTimeVarianceComparer : IEqualityComparer<NcTimeVariance>
    {
        public bool Equals (NcTimeVariance tv1, NcTimeVariance tv2)
        {
            return tv1.Type == tv2.Type;
        }

        public int GetHashCode (NcTimeVariance tv)
        {
            return (int)tv.Type;
        }
    }

    public class NcDeadlineTimeVariance : NcTimeVariance
    {
        protected static List<double> _Factors = new List<double> () {
            0.1, // 0
            1.0, // 1
            0.7, // 2
            0.4, // 3
        };

        protected static List<TimeSpan> _TimeOffsets = new List<TimeSpan> () {
            new TimeSpan (-1, 0, 0, 0), // 0
            new TimeSpan (0, 0, 0, 0), // 1
            new TimeSpan (1, 0, 0, 0), // 2
            new TimeSpan (2, 0, 0, 0), // 3
        };

        protected override List<double> Factors {
            get {
                return _Factors;
            }
        }

        protected override List<TimeSpan> TimeOffsets {
            get {
                return _TimeOffsets;
            }
        }

        public NcDeadlineTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId, DateTime deadline)
            : base (description, callback, objId, NcTimeVarianceType.DEADLINE, deadline)
        {
        }
    }

    public class NcDeferenceTimeVariance : NcTimeVariance
    {
        protected static List<double> _Factors = new List<double> {
            1.0, // 0
            0.1, // 1
        };

        protected static List<TimeSpan> _TimeOffsets = new List<TimeSpan> {
            new TimeSpan (-1, 0, 0, 0), // 0
            new TimeSpan (0, 0, 0, 0), // 1
        };

        protected override List<double> Factors {
            get {
                return _Factors;
            }
        }

        protected override List<TimeSpan> TimeOffsets {
            get {
                return _TimeOffsets;
            }
        }

        public NcDeferenceTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId, DateTime deferUntil)
            : base (description, callback, objId, NcTimeVarianceType.DEFERENCE, deferUntil)
        {
        }
    }

    public class NcAgingTimeVariance : NcTimeVariance
    {
        protected static List<double> _Factors = new List<double> {
            0.1, // 0
            1.0, // 1
            0.8, // 2
            0.7, // 3
            0.6, // 4
            0.5, // 5
            0.4, // 6
            0.3, // 7
            0.2, // 8
        };

        protected static List<TimeSpan> _TimeOffsets = new List<TimeSpan> {
            new TimeSpan (-1, 0, 0, 0), // 0
            new TimeSpan (7, 0, 0, 0), // 1
            new TimeSpan (8, 0, 0, 0), // 2
            new TimeSpan (9, 0, 0, 0), // 3
            new TimeSpan (10, 0, 0, 0), // 4
            new TimeSpan (11, 0, 0, 0), // 5
            new TimeSpan (12, 0, 0, 0), // 6
            new TimeSpan (13, 0, 0, 0), // 7
            new TimeSpan (14, 0, 0, 0), // 8
        };

        protected override List<double> Factors {
            get {
                return _Factors;
            }
        }

        protected override List<TimeSpan> TimeOffsets {
            get {
                return _TimeOffsets;
            }
        }

        public NcAgingTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId, DateTime startTime)
            : base (description, callback, objId, NcTimeVarianceType.AGING, startTime)
        {
        }
    }

    public class NcMeetingTimeVariance : NcTimeVariance
    {
        protected static List<double> _Factors = new List<double> {
            0.05, // 0
            1.0, // 1
        };

        protected static List<TimeSpan> _TimeOffsets = new List<TimeSpan> {
            new TimeSpan (-1, 0, 0, 0), // 0
            new TimeSpan (0, 0, 0, 0), // 1
        };

        protected override List<double> Factors {
            get {
                return _Factors;
            }
        }

        protected override List<TimeSpan> TimeOffsets {
            get {
                return _TimeOffsets;
            }
        }

        public NcMeetingTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId, DateTime endTime)
            : base (description, callback, objId, NcTimeVarianceType.MEETING, endTime)
        {
        }
    }

    /// A NcTimeVarianceSet holds a list of NcTimeVariance derived objects for
    /// one particular object (e.g one McEmailMessage)
    public class NcTimeVarianceList : HashSet<NcTimeVariance>
    {
        public NcTimeVarianceList () : base (new NcTimeVarianceComparer ())
        {
        }

        public double Adjustment (DateTime now)
        {
            double adjustment = 1.0;
            foreach (var tv in this) {
                adjustment *= tv.Adjustment (now);
            }
            return adjustment;
        }

        public bool Start (DateTime now)
        {
            bool started = false;
            foreach (var tv in this) {
                if (tv.ShouldRun (now)) {
                    tv.Start ();
                    started = true;
                }
            }
            return started;
        }

        public NcTimeVarianceType LastTimeVarianceType (DateTime now)
        {
            NcTimeVarianceType type = NcTimeVarianceType.DONE;
            foreach (var tv in this) {
                if (tv.ShouldRun (now)) {
                    type = tv.Type;
                }
            }
            return type;
        }
    }

    /// TimeVarianceTable holds a dictionary of NcTimeVarianceSet
    public class TimeVarianceTable : IEnumerable
    {
        private object LockObj = new object ();

        private ConcurrentDictionary<string, NcTimeVarianceList> TvLists = new ConcurrentDictionary<string, NcTimeVarianceList> ();

        public int Count {
            get {
                int count = 0;
                lock (LockObj) {
                    foreach (NcTimeVarianceList tvList in TvLists.Values) {
                        count += tvList.Count;
                    }
                }
                return count;
            }
        }

        private NcTimeVarianceList AddList (string description)
        {
            lock (LockObj) {
                NcTimeVarianceList tvList = new NcTimeVarianceList ();
                if (TvLists.TryAdd (description, tvList)) {
                    return tvList;
                }
                return GetList (description);
            }
        }

        public NcTimeVarianceList GetList (string description)
        {
            NcTimeVarianceList tvList;
            TvLists.TryGetValue (description, out tvList);
            return tvList;
        }

        public bool RemoveList (string description, out NcTimeVarianceList tvList)
        {
            lock (LockObj) {
                return TvLists.TryRemove (description, out tvList);
            }
        }

        public bool Add (NcTimeVariance tv)
        {
            lock (LockObj) {
                NcTimeVarianceList tvList = GetList (tv.Description);
                if (null == tvList) {
                    tvList = AddList (tv.Description);
                }
                return tvList.Add (tv);
            }
        }

        public bool Remove (NcTimeVariance tv)
        {
            lock (LockObj) {
                NcTimeVarianceList tvList = GetList (tv.Description);
                if (null == tvList) {
                    return false;
                }
                bool removed = tvList.Remove (tv);
                if (0 == tvList.Count) {
                    /// Remove the last element in the set. Get rid of the set itself
                    NcTimeVarianceList dummy;
                    RemoveList (tv.Description, out dummy);
                }
                return removed;
            }
        }

        public IEnumerator GetEnumerator ()
        {
            foreach (NcTimeVarianceList tvList in TvLists.Values) {
                foreach (NcTimeVariance tv in tvList) {
                    yield return tv;
                }
            }
        }

        public void Clear ()
        {
            TvLists.Clear ();
        }
    }
}
