//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;

namespace NachoCore.Utils
{
    public class NcCounter : IDisposable
    {
        // Keep track of all instantiated NcCounter
        private static ConcurrentDictionary<string, NcCounter> ActiveList_;

        public static ConcurrentDictionary<string, NcCounter> ActiveList {
            get {
                if (null == ActiveList_) {
                    ActiveList_ = new ConcurrentDictionary<string, NcCounter> ();
                }
                return ActiveList_;
            }
        }

        // String used for reporting to telemetry
        public string Name;

        // Configuration of the counter
        // If true, clicking a counter automatically increment the parent (and all
        // ancestor) counters by the same amount.
        private bool _UpdateParent;

        public bool UpdateParent {
            get {
                return _UpdateParent;
            }
        }

        // If true, it automatically resets the count when timer fires
        private bool _AutoReset;

        public bool AutoReset {
            get {
                if (!IsRoot ()) {
                    Log.Warn (Log.LOG_UTILS, "Getting AutoReset for non-root counter {0}", Name);
                    NcAssert.True (false == _AutoReset);
                }
                return _AutoReset;
            }
            set {
                if (!IsRoot ()) {
                    Log.Error (Log.LOG_UTILS, "Setting AutoReset for non-root counter {0} is ignored", Name);
                    return;
                }
                _Lock.WaitOne ();
                _AutoReset = value;
                _Lock.ReleaseMutex ();
            }
        }

        // If greater than 0, auto-reporting happens every N seconds as specified
        // by this propperty. If 0, auto-reporting is diabled.
        // Time interval in seconds between auto-reporting
        private int _ReportPeriod;

        public int ReportPeriod {
            get {
                if (!IsRoot ()) {
                    Log.Warn (Log.LOG_UTILS, "Getting ReportPeriod for non-root counter {0}", Name);
                    NcAssert.True (0 == _ReportPeriod);
                }
                return _ReportPeriod;
            }
            set {
                if (!IsRoot ()) {
                    Log.Error (Log.LOG_UTILS, "Setting ReportPeriod for non-root counter {0} is ignored", Name);
                    return;
                }
                if (0 > value) {
                    Log.Error (Log.LOG_UTILS, "Invalid second ({0})", value);
                    return;
                }
                if (value == _ReportPeriod) {
                    return; // no change
                }

                LockDownward ();

                // Cancel the old timer
                if (null != Timer) {
                    Timer.Dispose ();
                    Timer = null;
                }

                // Calculate the new duration
                DateTime now = DateTime.UtcNow;
                Int64 deltaTick;
                if (0 == _ReportPeriod) {
                    deltaTick = value * TimeSpan.TicksPerSecond;
                } else {
                    deltaTick = UtcStart.Ticks + (value * TimeSpan.TicksPerSecond) - now.Ticks;
                }
                if (0 > deltaTick) {
                    deltaTick = 0;
                }

                // Update the period
                _ReportPeriod = value;

                // Create a new timer if necessary
                if (0 < _ReportPeriod) {
                    Timer = new NcPausableTimer ("NcCounter", NcCounter.Callback, this,
                        new TimeSpan (deltaTick), new TimeSpan (0, 0, _ReportPeriod));
                }

                UnlockDownward ();
            }
        }

        public delegate void NcCounterCallback ();

        private NcCounterCallback PreReportCallback_;

        public NcCounterCallback PreReportCallback {
            get {
                return PreReportCallback_;
            }
            set {
                _Lock.WaitOne ();
                PreReportCallback_ = value;
                _Lock.ReleaseMutex ();
            }
        }

        // Counters can be arranged as a tree hiearchy. There are two use cases.
        // First, all related counters can fire off the timer of the root counter.
        // Second, incrementing a child counter can automatically increment parent
        // counters.
        private NcCounter Parent;
        private List<NcCounter> Children;
        private NcPausableTimer Timer;
        private Int64 _Count;

        public Int64 Count {
            get {
                return _Count;
            }
        }

        Mutex _Lock;

        // Time when the counter is initialized or last reset
        public DateTime UtcStart;

        private bool IsRoot ()
        {
            return (null == Parent);
        }


        private static void Callback (object obj)
        {
            NcCounter counter = (NcCounter)obj;
            counter.Report ();
            if (counter.AutoReset) {
                counter.Reset ();
            }
        }

        public NcCounter (string name, bool updateParent = false)
        {
            NcAssert.True (!ActiveList.ContainsKey (name));
            Name = name;
            _Lock = new Mutex ();
            _AutoReset = false;
            _ReportPeriod = 0;
            _UpdateParent = updateParent;
            Children = new List<NcCounter> ();
            UtcStart = DateTime.UtcNow;

            bool added = ActiveList.TryAdd (name, this);
            NcAssert.True (added);
        }

        public NcCounter AddChild (string name)
        {
            _Lock.WaitOne ();
            NcCounter counter = new NcCounter (Name + "." + name, UpdateParent);
            counter.Parent = this;
            Children.Add (counter);
            _Lock.ReleaseMutex ();
            return counter;
        }

        private void LockUpward ()
        {
            _Lock.WaitOne ();
            if (!UpdateParent) {
                NcCounter current = this;
                while (null != current.Parent) {
                    current = current.Parent;
                    current._Lock.WaitOne ();
                }
            }
        }

        private void UnlockUpward ()
        {
            _Lock.ReleaseMutex ();
            if (!UpdateParent) {
                NcCounter current = this;
                while (null != current.Parent) {
                    current = current.Parent;
                    current._Lock.ReleaseMutex ();
                }
            }
        }

        private void LockDownward ()
        {
            _Lock.WaitOne ();
            foreach (NcCounter child in Children) {
                child.LockDownward ();
            }
        }

        private void UnlockDownward ()
        {
            _Lock.ReleaseMutex ();
            foreach (NcCounter child in Children) {
                child.UnlockDownward ();
            }
        }

        // Increase the count by an increment (default to 1 if omitted)
        // One can use a non-1 increment to count bytes for example.
        public void Click (int increment = 1)
        {
            LockUpward ();

            if (UpdateParent) {
                NcCounter current = this;
                current._Count += increment;
                while (null != current.Parent) {
                    current = current.Parent;
                    current._Count += increment;
                }
            }

            UnlockUpward ();
        }
         
        // Reset the counter to 0.
        public void Reset ()
        {
            LockDownward ();
            ResetInternal (DateTime.UtcNow);
            UnlockDownward ();
        }

        // Internal rountine to reset a counter. This function is used to
        // allow all child counters to be reset using the same start time.
        private void ResetInternal (DateTime utcStart)
        {
            _Count = 0;
            UtcStart = utcStart;
            foreach (NcCounter child in Children) {
                child.ResetInternal (utcStart);
            }
        }

        private void ReportInternal (DateTime utcNow)
        {
            Console.WriteLine ("Counter: {0} = {1} [{2}-{3}]", Name, Count, UtcStart, utcNow);
            Telemetry.RecordCounter (Name, Count, UtcStart, utcNow);
            foreach (NcCounter child in Children) {
                child.ReportInternal (utcNow);
            }
        }

        public void Report ()
        {
            LockDownward ();
            if (null != PreReportCallback) {
                PreReportCallback ();
            }
            ReportInternal (DateTime.UtcNow);
            UnlockDownward ();
        }

        public void Dispose ()
        {
            if (null != Timer) {
                Timer.Dispose ();
            }
            NcCounter counter;
            bool retval = ActiveList.TryRemove (Name, out counter);
            NcAssert.True (retval && (this == counter));
            if (null != Children) {
                foreach (var child in Children) {
                    child.Dispose ();
                }
            }
        }

        public static void StopService ()
        {
            foreach (NcCounter c in ActiveList.Values) {
                c.Timer.Pause ();
            }
        }

        public static void StartService ()
        {
            foreach (NcCounter c in ActiveList.Values) {
                c.Timer.Resume ();
            }
        }
    }
}

