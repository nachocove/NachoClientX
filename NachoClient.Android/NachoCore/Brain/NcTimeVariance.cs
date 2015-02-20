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

        /// A TimeVarianceList holds a list of NcTimeVariance derived objects for
        /// one particular object (e.g one McEmailMessage)
        public class TimeVarianceList : HashSet<NcTimeVariance>
        {
            public TimeVarianceList () : base (new NcTimeVarianceComparer ())
            {
            }

            public NcTimeVariance TryGetType<T> ()
            {
                foreach (NcTimeVariance tv in this) {
                    if (tv is T) {
                        return tv;
                    }
                }
                return null;
            }

            public TimeVarianceList FilterStillRunning (DateTime now)
            {
                TimeVarianceList tvList = new TimeVarianceList ();
                foreach (NcTimeVariance tv in this) {
                    DateTime lastEvent = tv.LastEventTime ();
                    if (lastEvent > now) {
                        tvList.Add (tv);
                    }
                }
                return tvList;
            }
        }

        /// TimeVarianceTable holds a dictionary of TimeVarianceList
        public class TimeVarianceTable : IEnumerable
        {
            private object _LockObj;

            private object LockObj {
                get {
                    if (null == _LockObj) {
                        _LockObj = new object ();
                    }
                    return _LockObj;
                }
                set {
                    _LockObj = value;
                }
            }

            private ConcurrentDictionary<string, TimeVarianceList> _TvLists;

            private ConcurrentDictionary<string, TimeVarianceList> TvLists {
                get {
                    if (null == _TvLists) {
                        _TvLists = new ConcurrentDictionary<string, TimeVarianceList> ();
                    }
                    return _TvLists;
                }
            }

            public int Count {
                get {
                    int count = 0;
                    lock (LockObj) {
                        foreach (TimeVarianceList tvList in TvLists.Values) {
                            count += tvList.Count;
                        }
                    }
                    return count;
                }
            }

            private TimeVarianceList AddList (string description)
            {
                lock (LockObj) {
                    TimeVarianceList tvList = new TimeVarianceList ();
                    if (TvLists.TryAdd (description, tvList)) {
                        return tvList;
                    }
                    return GetList (description);
                }
            }

            public TimeVarianceList GetList (string description)
            {
                TimeVarianceList tvList;
                TvLists.TryGetValue (description, out tvList);
                return tvList;
            }

            public bool RemoveList (string description, out TimeVarianceList tvList)
            {
                lock (LockObj) {
                    return TvLists.TryRemove (description, out tvList);
                }
            }

            public bool Add (NcTimeVariance tv)
            {
                lock (LockObj) {
                    TimeVarianceList tvList = GetList (tv.Description);
                    if (null == tvList) {
                        tvList = AddList (tv.Description);
                    }
                    return tvList.Add (tv);
                }
            }

            public bool Remove (NcTimeVariance tv)
            {
                lock (LockObj) {
                    TimeVarianceList tvList = GetList (tv.Description);
                    if (null == tvList) {
                        return false;
                    }
                    bool removed = tvList.Remove (tv);
                    if (0 == tvList.Count) {
                        /// Remove the last element in the set. Get rid of the set itself
                        TimeVarianceList dummy;
                        RemoveList (tv.Description, out dummy);
                    }
                    return removed;
                }
            }

            public IEnumerator GetEnumerator ()
            {
                foreach (TimeVarianceList tvList in TvLists.Values) {
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
        public int MaxState;

        // The timer for keeping track of when the next event occurs.
        // In order to conserve memory, we only create the timer if
        // needed.
        private NcTimerPoolTimer EventTimer;

        public bool IsRunning {
            get {
                return (null != EventTimer);
            }
        }

        protected string _Description { get; set; }

        public string Description {
            get {
                return _Description;
            }
        }

        public TimeVarianceCallBack CallBackFunction;

        public Int64 CallBackId;

        /// <summary>
        /// This method must be overridden for each derived class. It provides the amount
        /// of adjustment (from 0.0 to 1.0) for each state.
        /// </summary>
        /// <param name="state">State.</param>
        public virtual double Adjustment (int state)
        {
            throw new NotImplementedException ("Adjustment");
        }

        /// <summary>
        /// This method must be overridden for each derived class. It provides the
        /// time when this current state ends.
        /// </summary>
        /// <returns>The event time.</returns>
        /// <param name="state">State.</param>
        protected virtual DateTime NextEventTime (int state)
        {
            throw new NotImplementedException ("NextEventTime");
        }

        public virtual NcTimeVarianceType TimeVarianceType ()
        {
            throw new NotImplementedException ("TimeVarianceType");
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
            MaxState = 1;
        }

        public NcTimeVariance (string description, TimeVarianceCallBack callback, Int64 objId)
        {
            _Description = description;
            LockObj = new object ();
            CallBackFunction = callback;
            CallBackId = objId;
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
            TimeVarianceList tvList;
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
            return tv1.TimeVarianceType () == tv2.TimeVarianceType ();
        }

        public int GetHashCode (NcTimeVariance tv)
        {
            return (int)tv.TimeVarianceType ();
        }
    }

    public class NcDeadlineTimeVariance : NcTimeVariance
    {
        public DateTime Deadline;

        public NcDeadlineTimeVariance (string description, TimeVarianceCallBack callback,
                                       Int64 objId, DateTime deadline) : base (description, callback, objId)
        {
            Deadline = deadline;
            MaxState = 3;
        }

        public override NcTimeVarianceType TimeVarianceType ()
        {
            return NcTimeVarianceType.DEADLINE;
        }

        public override double Adjustment (int state)
        {
            double factor;
            switch (state) {
            case 0:
                factor = 0.1;
                break;
            case 1:
                factor = 1.0;
                break;
            case 2:
                factor = 0.7;
                break;
            case 3:
                factor = 0.4;
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown deadline state {0}", State));
            }
            return factor;
        }

        protected override DateTime NextEventTime (int state)
        {
            DateTime retval;
            switch (state) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = Deadline;
                break;
            case 2:
                retval = SafeAddDateTime (Deadline, new TimeSpan (1, 0, 0, 0));
                break;
            case 3:
                retval = SafeAddDateTime (Deadline, new TimeSpan (2, 0, 0, 0));
                break;
            default: 
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown deadline state {0}", State));
            }
            return LimitEventTime (retval);
        }
    }

    public class NcDeferenceTimeVariance : NcTimeVariance
    {
        public DateTime DeferUntil { get; set; }

        public NcDeferenceTimeVariance (string description, TimeVarianceCallBack callback,
                                        Int64 objId, DateTime deferUntil) : base (description, callback, objId)
        {
            DeferUntil = deferUntil;
            MaxState = 1;
        }

        public override NcTimeVarianceType TimeVarianceType ()
        {
            return NcTimeVarianceType.DEFERENCE;
        }

        public override double Adjustment (int state)
        {
            double factor;
            switch (state) {
            case 0:
                factor = 1.0;
                break;
            case 1:
                factor = 0.1;
                break;
            default:
                string mesg = String.Format ("unknown deference state {0}", State);
                throw new NcAssert.NachoDefaultCaseFailure (mesg);
            }
            return factor;
        }

        protected override DateTime NextEventTime (int state)
        {
            DateTime retval;
            switch (state) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = DeferUntil;
                break;
            default:
                string mesg = String.Format ("unknown deference state {0}", State);
                throw new NcAssert.NachoDefaultCaseFailure (mesg);
            }
            return LimitEventTime (retval);
        }
    }

    public class NcAgingTimeVariance : NcTimeVariance
    {
        public DateTime StartTime { get; set; }

        public NcAgingTimeVariance (string description, TimeVarianceCallBack callback,
                                    Int64 objId, DateTime startTime) : base (description, callback, objId)
        {
            StartTime = startTime;
            MaxState = 8;
        }

        public override NcTimeVarianceType TimeVarianceType ()
        {
            return NcTimeVarianceType.AGING;
        }

        public override double Adjustment (int state)
        {
            double factor;
            switch (state) {
            case 0:
                factor = 0.1;
                break;
            case 1:
                factor = 1.0;
                break;
            case 2:
                factor = 0.8;
                break;
            case 3:
                factor = 0.7;
                break;
            case 4:
                factor = 0.6;
                break;
            case 5:
                factor = 0.5;
                break;
            case 6:
                factor = 0.4;
                break;
            case 7:
                factor = 0.3;
                break;
            case 8:
                factor = 0.2;
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown aging state {0}", state));
            }

            return factor;
        }

        protected override DateTime NextEventTime (int state)
        {
            DateTime retval;
            switch (state) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = SafeAddDateTime (StartTime, new TimeSpan (7, 0, 0, 0));
                break;
            case 2:
                retval = SafeAddDateTime (StartTime, new TimeSpan (8, 0, 0, 0));
                break;
            case 3:
                retval = SafeAddDateTime (StartTime, new TimeSpan (9, 0, 0, 0));
                break;
            case 4:
                retval = SafeAddDateTime (StartTime, new TimeSpan (10, 0, 0, 0));
                break;
            case 5:
                retval = SafeAddDateTime (StartTime, new TimeSpan (11, 0, 0, 0));
                break;
            case 6:
                retval = SafeAddDateTime (StartTime, new TimeSpan (12, 0, 0, 0));
                break;
            case 7:
                retval = SafeAddDateTime (StartTime, new TimeSpan (13, 0, 0, 0));
                break;
            case 8:
                retval = SafeAddDateTime (StartTime, new TimeSpan (14, 0, 0, 0));
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown aging state {0}", State));
            }
            return retval;
        }
    }

    public class NcMeetingTimeVariance : NcTimeVariance
    {
        public DateTime EndTime { get; set; }

        public NcMeetingTimeVariance (string description, TimeVarianceCallBack callback,
                                      Int64 objId, DateTime endTime) : base (description, callback, objId)
        {
            EndTime = endTime;
            MaxState = 1;
        }

        public override NcTimeVarianceType TimeVarianceType ()
        {
            return NcTimeVarianceType.MEETING;
        }

        public override double Adjustment (int state)
        {
            double factor;
            switch (state) {
            case 0:
                factor = 0.05;
                break;
            case 1:
                factor = 1.0;
                break;
            default:
                string mesg = String.Format ("unknown meeting state {0}", State);
                throw new NcAssert.NachoDefaultCaseFailure (mesg);
            }
            return factor;
        }

        protected override DateTime NextEventTime (int state)
        {
            DateTime retval;
            switch (state) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = EndTime;
                break;
            default:
                string mesg = String.Format ("unknown meeting state {0}", State);
                throw new NcAssert.NachoDefaultCaseFailure (mesg);
            }
            return LimitEventTime (retval);
        }
    }
}
