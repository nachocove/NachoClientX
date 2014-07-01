//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
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
    public class NcTimeVariance
    {
        public delegate void TimeVarianceCallBack (int state);

        /// A list of all active Time variance state machine
        protected static ConcurrentDictionary<string, NcTimeVariance> _ActiveList;
        public static ConcurrentDictionary<string, NcTimeVariance> ActiveList {
            get {
                if (null == _ActiveList) {
                    _ActiveList = new ConcurrentDictionary<string, NcTimeVariance> ();
                }
                return _ActiveList;
            }
        }

        /// State is just an integer that increments. Note that state 0 is
        /// reserved for terminated state.
        private int State_ { get; set; }
        public int State {
            get {
                return State_;
            }
        }

        // The largest non-zero state. Advancing from this state goes to 0.
        public int MaxState;

        // The timer for keeping track of when the next event occurs.
        // In order to conserve memory, we only create the timer if
        // needed. Otherwise, we'll need a timer for every 
        private NcTimer EventTimer;

        protected string _Description { get; set; }
        public string Description {
            get {
                return _Description;
            }
        }

        public TimeVarianceCallBack CallBack;

        public virtual double AdjustScore (double score)
        {
            throw new NotImplementedException ("AdjustScore");
        }

        protected virtual DateTime NextEventTime ()
        {
            throw new NotImplementedException ("NextEventTime");
        }

        private void Initialize ()
        {
            State_ = 1;
            MaxState = 1;
            NcAssert.True (!ActiveList.ContainsKey (Description));
            ActiveList[Description] = this;
            CallBack = null;
        }

        public NcTimeVariance (string description)
        {
            _Description = description;
            Initialize ();
        }

        private string TimerDescription ()
        {
            return String.Format ("time variance: {0}", Description);
        }

        public void Pause ()
        {
            if (null != EventTimer) {
                EventTimer.Dispose ();
                EventTimer = null;
            }
        }

        public void Resume ()
        {
            if (0 != State_) {
                DateTime eventTime = NextEventTime ();
                DateTime now = DateTime.Now;
                if (eventTime.Ticks > now.Ticks) {
                    int dueTime = (int)(eventTime - now).TotalMilliseconds;
                    EventTimer = new NcTimer (TimerDescription (), AdvanceCallback, this,
                            dueTime, Timeout.Infinite);
                } else {
                    Advance ();
                }
            } else {
                Cleanup ();
            }
        }

        public virtual void Start ()
        {
            Resume ();
        }

        private void Cleanup ()
        {
            Pause ();
            NcTimeVariance dummy;
            bool didRemoved = ActiveList.TryRemove (Description, out dummy);
            NcAssert.True (didRemoved);
        }

        public static void AdvanceCallback (object obj)
        {
            NcTimeVariance tv = obj as NcTimeVariance;
            tv.Advance ();
        }

        private void FindNextState ()
        {
            NcAssert.True (State_ < MaxState);
            State_++;
            while (DateTime.Now > NextEventTime ()) {
                if (MaxState > State_) {
                    State_++;
                } else {
                    State_ = 0;
                    break;
                }
            }
        }

        public virtual void Advance ()
        {
            // Update state
            if (MaxState == State_) {
                State_ = 0;
            } else {
                FindNextState ();
            }

            // Throw away the fired timer. 
            Pause ();
            if (null != CallBack) {
                CallBack (State);
            }

            // Set up for next event if there is one
            if (0 == State) {
                Cleanup ();
            } else {
                Resume ();
            }
        }

        public void Dispose ()
        {
            if (null != EventTimer) {
                EventTimer.Dispose ();
                EventTimer = null;
            }
        }

        public static void PauseAll ()
        {
            foreach (NcTimeVariance tv in ActiveList.Values) {
                tv.Pause ();
            }
        }

        public static void ResumeAll ()
        {
            foreach (NcTimeVariance tv in ActiveList.Values) {
                tv.Resume ();
            }
        }
    }

    public class NcDeadlineTimeVariance : NcTimeVariance
    {
        public DateTime Deadline;

        public NcDeadlineTimeVariance (string description, DateTime deadline) : base (description)
        {
            Deadline = deadline;
            MaxState = 3;
        }

        public override double AdjustScore (double score)
        {
            double factor = 1.0;
            switch (State) {
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
            return score * factor;
        }

        protected override DateTime NextEventTime ()
        {
            DateTime retval;
            switch (State) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = Deadline;
                break;
            case 2:
                retval = Deadline + new TimeSpan (1, 0, 0, 0);
                break;
            case 3:
                retval = Deadline + new TimeSpan (2, 0, 0, 0);
                break;
            default: 
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown deadline state {0}", State));
            }
            return retval;
        }

        public static DateTime LastEventTime (DateTime deadline)
        {
            return deadline + new TimeSpan (2, 0, 0, 0);
        }
    }

    public class NcDeferenceTimeVariance : NcTimeVariance
    {
        public DateTime DeferUntil { get; set; }

        public NcDeferenceTimeVariance (string description, DateTime deferUntil) : base (description)
        {
            DeferUntil = deferUntil;
            MaxState = 1;
        }

        public override double AdjustScore (double score)
        {
            double factor;
            switch (State) {
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
            return score * factor;
        }

        protected override DateTime NextEventTime ()
        {
            DateTime retval;
            switch (State) {
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
            return retval;
        }  
    }

    public class NcAgingTimeVariance : NcTimeVariance
    {
        public DateTime StartTime { get; set; }

        public NcAgingTimeVariance (string description, DateTime startTime) : base (description)
        {
            StartTime = startTime;
            MaxState = 8;
        }

        public override double AdjustScore (double score)
        {
            double factor;
            switch (State) {
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
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown aging state {0}", State));
            }

            return score * factor;
        }

        protected override DateTime NextEventTime ()
        {
            DateTime retval;
            switch (State) {
            case 0:
                retval = new DateTime (0, 0, 0, 0, 0, 0);
                break;
            case 1:
                retval = StartTime + new TimeSpan (7, 0, 0, 0);
                break;
            case 2:
                retval = StartTime + new TimeSpan (8, 0, 0, 0);
                break;
            case 3:
                retval = StartTime + new TimeSpan (9, 0, 0, 0);
                break;
            case 4:
                retval = StartTime + new TimeSpan (10, 0, 0, 0);
                break;
            case 5:
                retval = StartTime + new TimeSpan (11, 0, 0, 0);
                break;
            case 6:
                retval = StartTime + new TimeSpan (12, 0, 0, 0);
                break;
            case 7:
                retval = StartTime + new TimeSpan (13, 0, 0, 0);
                break;
            case 8:
                retval = StartTime + new TimeSpan (14, 0, 0, 0);
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("unknown aging state {0}", State));
            }
            return retval;
        }

        public static DateTime LastEventTime (DateTime dateReceived)
        {
            return dateReceived + new TimeSpan (14, 0, 0, 0);
        }
    }
}
