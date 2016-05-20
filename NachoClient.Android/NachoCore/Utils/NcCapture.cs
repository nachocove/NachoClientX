//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Diagnostics;
using System.Text;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    /// <summary>
    /// First and second order statistics for a capture kind.
    /// </summary>
    public class Statistics2
    {
        private object LockObj;

        private int _Min;

        public int Min {
            get {
                return (0 == Count ? 0 : _Min);
            }
        }

        private int _Max;

        public int Max {
            get {
                return (0 == Count ? 0 : _Max);
            }
        }

        public int Average {
            // Only compute average when asked to save some divide
            get {
                return (0 == Count ? 0 : (int)(Total / Count));
            }
        }

        public int StdDev {
            get {
                if (0 == Count) {
                    return 0;
                }
                double variance = -((double)Total * (double)Total) / (double)Count;
                variance += (double)Total2;
                variance /= (double)Count;
                return (int)Math.Sqrt (variance);
            }
        }

        public int Count { get; protected set; }

        protected long Total;
        protected long Total2;

        public Statistics2 ()
        {
            LockObj = new object ();
            Reset ();
        }

        public void Update (int value)
        {
            lock (LockObj) {
                Count += 1;
                Total += value;
                Total2 += value * value;
                if (value < _Min) {
                    _Min = value;
                }
                if (value > _Max) {
                    _Max = value;
                }
            }
        }

        public void Reset ()
        {
            lock (LockObj) {
                _Min = int.MaxValue;
                _Max = int.MinValue;
                Count = 0;
                Total = 0;
                Total2 = 0;
            }
        }

        public override string ToString ()
        {
            var top = string.Format ("Count = {0}, Min = {1}, Max = {2}, Average = {3}, StdDev = {4}",
                          Count, Min, Max, Average, StdDev);
            return top;
        }

        public void Report (string kind)
        {
            NcApplication.Instance.TelemetryService.RecordStatistics2 (kind, Count, Min, Max, Total, Total2);
            Reset ();
        }
    }

    public class NcCapture : IDisposable
    {
        private class NcCaptureKind
        {
            public Statistics2 Statistics;
            public ConcurrentDictionary<int, NcCapture> CaptureList;

            public NcCaptureKind ()
            {
                Statistics = new Statistics2 ();
                CaptureList = new ConcurrentDictionary<int, NcCapture> ();
            }

            public void Add (NcCapture capture)
            {
                var added = CaptureList.TryAdd (Thread.CurrentThread.ManagedThreadId, capture);
                NcAssert.True (added);
            }

            public void Remove (NcCapture capture)
            {
                NcCapture dummy;
                bool result = CaptureList.TryRemove (Thread.CurrentThread.ManagedThreadId, out dummy);
                NcAssert.True (result);
            }

            public bool HasThread (int threadId)
            {
                return CaptureList.ContainsKey (threadId);
            }
        }

        private string Kind;
        private IStopwatch Watch;
        // Note that we cannot use Stopwatch.IsRunning property because when going
        // to background, we need to stop the capture but remember that it
        // needs to restart when waking up.
        public bool IsRunning { get; protected set; }

        // If true, this means the capture supports recursion and this is not
        // the first capture in the stack. In that case, no timer is actually
        // started.
        public bool IsRecursive { get; protected set; }

        public long ElapsedMilliseconds { get { return Watch.ElapsedMilliseconds; } }

        private static object ClassLockObj = new object ();
        private static Dictionary<string, NcCaptureKind> PerKind = new Dictionary<string, NcCaptureKind> ();
        public static Type StopwatchClass = typeof(PlatformStopwatch);
        // For periodically report to telemetry
        private static NcTimer ReportTimer;
        // For keep track of how long the sleep is
        private static IStopwatch SleepWatch;
        private bool IsDisposed;

        private NcCapture (string kind)
        {
            NcAssert.True (null != ClassLockObj);
            lock (ClassLockObj) {
                if (!PerKind.ContainsKey (kind)) {
                    AddKind (kind);
                }

                Kind = kind;
                IsRunning = false;
                Watch = (IStopwatch)Activator.CreateInstance (StopwatchClass);

                // Add to the global tracking list
                var captureKind = PerKind [kind];
                if (captureKind.HasThread (Thread.CurrentThread.ManagedThreadId)) {
                    IsRecursive = true;
                } else {
                    captureKind.Add (this);
                }
            }
        }

        public void Dispose ()
        {
            Dispose (true);
        }

        protected virtual void Dispose (bool disposing)
        {
            if (IsDisposed) {
                return;
            }
            if (disposing) {
                if (!IsRecursive) {
                    Stop ();
                    lock (ClassLockObj) {
                        // Remove self from the global tracking list
                        PerKind [Kind].Remove (this);
                    }
                }
                IsDisposed = true;
            }
        }

        public static bool AddKind (string kind)
        {
            lock (ClassLockObj) {
                if (PerKind.ContainsKey (kind)) {
                    return false;
                } else {
                    PerKind.Add (kind, new NcCaptureKind ());
                    return true;
                }
            }
        }

        // Remove a kind. This is only for testing purpose. Do not use this in
        // production code! The ramification of allowing removal of kind dynamically
        // is that we need to lock up critical sections when summarizing and
        // reporting a Summary since now it may disappear during its being read.
        public static bool RemoveKind (string kind)
        {
            lock (ClassLockObj) {
                if (!PerKind.ContainsKey (kind)) {
                    return false;
                }
                NcCaptureKind captureKind = PerKind [kind];
                if (0 < captureKind.CaptureList.Count) {
                    return false; // someone is still using it
                }
                PerKind.Remove (kind);
                return true;
            }
        }

        public static string Summarize (string kind)
        {
            // Note that we don't take ClassLockObj here because there is no
            // way to delete a kind.
            return string.Format ("[Kind: {0}] ", kind) + PerKind [kind].Statistics.ToString ();
        }

        public static string Summarize ()
        {
            StringBuilder sb = new StringBuilder ();
            lock (ClassLockObj) {
                foreach (var key in PerKind.Keys) {
                    sb.Append (Summarize (key));
                    sb.Append (Environment.NewLine);
                }
            }
            return sb.ToString ();
        }

        public static NcCapture Create (string kind)
        {
            return new NcCapture (kind);
        }

        public static NcCapture CreateAndStart (string kind)
        {
            var capture = Create (kind);
            capture.Start ();
            return capture;
        }

        public void Start ()
        {
            if (IsDisposed) {
                throw new ObjectDisposedException ("NcCapture already disposed");
            }
            if (IsRunning) {
                Watch.Reset ();
            }
            if (!IsRecursive) {
                Watch.Start ();
                IsRunning = true;
            }
        }

        public void Pause ()
        {
            if (!IsRecursive && IsRunning) {
                Watch.Stop ();
            }
        }

        public void Resume ()
        {
            if (!IsRecursive && IsRunning) {
                Watch.Start ();
            }
        }

        public void Stop ()
        {
            if (!IsRecursive && IsRunning) {
                Watch.Stop ();
                PerKind [Kind].Statistics.Update ((int)Watch.ElapsedMilliseconds);
                Reset ();
            }
        }

        public void Reset ()
        {
            if (!IsRecursive) {
                Watch.Reset ();
                IsRunning = false;
            }
        }

        public static void ReportKind (string Kind)
        {
            if (!PerKind.ContainsKey (Kind)) {
                return;
            }
            Statistics2 summary = PerKind [Kind].Statistics;
            summary.Report (Kind);
        }

        public static void Report ()
        {
            lock (ClassLockObj) {
                foreach (string kind in PerKind.Keys) {
                    ReportKind (kind);
                }
            }
        }

        delegate void CaptureListWalker (NcCapture capture);

        private static void WalkCaptureList (string Kind, CaptureListWalker walker)
        {
            lock (ClassLockObj) {
                if (!PerKind.ContainsKey (Kind)) {
                    return;
                }
                foreach (NcCapture capture in PerKind [Kind].CaptureList.Values) {
                    walker (capture);
                }
            }
        }

        public static void PauseKind (string Kind)
        {
            WalkCaptureList (Kind, cap => {
                cap.Pause ();
            });
        }

        public static void ResumeKind (string Kind)
        {
            WalkCaptureList (Kind, cap => {
                cap.Resume ();
            });
        }

        public static void Callback (object obj)
        {
            NcCapture.Report ();
        }

        public static void ResumeAll ()
        {
            lock (ClassLockObj) {
                const int reportPeriodMsec = 60 * 1000; // every 60 sec
                if (null == SleepWatch) {
                    SleepWatch = (IStopwatch)Activator.CreateInstance (StopwatchClass);
                }
                int dueTime = reportPeriodMsec;
                if (reportPeriodMsec < SleepWatch.ElapsedMilliseconds) {
                    dueTime = 0; // if it has slept for more than report period, report immediately
                }
                if (null == ReportTimer) {
                    // Report periodically
                    ReportTimer = new NcTimer ("NcCapture", NcCapture.Callback, null, dueTime, reportPeriodMsec);
                }
                foreach (string kind in PerKind.Keys) {
                    ResumeKind (kind);
                }
            }
        }

        public static void PauseAll ()
        {
            lock (ClassLockObj) {
                if (null != ReportTimer) {
                    ReportTimer.Dispose ();
                    ReportTimer = null;
                }
                foreach (string kind in PerKind.Keys) {
                    PauseKind (kind);
                }
                NcAssert.True (null != SleepWatch);
                SleepWatch.Start ();
            }
        }

        public static Statistics2 GetStatistics (string kind)
        {
            lock (ClassLockObj) {
                NcCaptureKind captureKind;
                if (!PerKind.TryGetValue (kind, out captureKind)) {
                    return null;
                }
                return captureKind.Statistics;
            }
        }
    }
}

