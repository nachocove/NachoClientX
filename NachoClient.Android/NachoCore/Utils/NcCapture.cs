//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NachoCore.Utils;

namespace NachoCore.Utils
{


    public class NcCapture : IDisposable
    {
        private class Summary
        {
            bool IsTop;
            uint _Min;

            uint Min {
                get {
                    return (0 == Count ? 0 : _Min);
                }
            }

            uint _Max;

            uint Max {
                get {
                    return (0 == Count ? 0 : _Max);
                }
            }

            uint Average {
                // Only compute average when asked to save some divide
                get {
                    return (0 == Count ? 0 : (uint)(Total / Count));
                }
            }

            uint StdDev {
                get {
                    if (0 == Count) {
                        return 0;
                    }
                    double variance = -((double)Total * (double)Total) / (double)Count;
                    variance += (double)Total2;
                    variance /= (double)Count;
                    return (uint)Math.Sqrt (variance);
                }
            }

            uint Count;
            UInt64 Total;
            UInt64 Total2;
            Dictionary <string, Summary> Xtra;

            public Summary (bool isTop)
            {
                IsTop = isTop;
                _Min = uint.MaxValue;
                _Max = uint.MinValue;
                Count = 0;
                Total = 0;
                Total2 = 0;
            }

            public void Update (uint value, Dictionary<string,uint> xtra)
            {
                Count += 1;
                Total += value;
                Total2 += value * value;
                if (value < _Min) {
                    _Min = value;
                }
                if (value > _Max) {
                    _Max = value;
                }
                if (null != xtra) {
                    if (null == Xtra) {
                        Xtra = new Dictionary<string, Summary> ();
                    }
                    foreach (var key in xtra.Keys) {
                        Summary summary;
                        if (!Xtra.TryGetValue (key, out summary)) {
                            summary = new Summary (false);
                            Xtra.Add (key, summary);
                        }
                        summary.Update (xtra [key], null);
                    }
                }
            }

            public void Update (uint value)
            {
                Update (value, null);
            }

            public void Reset ()
            {
                _Min = uint.MaxValue;
                _Max = uint.MinValue;
                Count = 0;
                Total = 0;
                Total2 = 0;
                if (null != Xtra) {
                    foreach (var kind in Xtra.Keys) {
                        Xtra [kind].Reset ();
                    }
                }
            }

            public override string ToString ()
            {
                if (IsTop) {
                    var top = string.Format ("Count = {0}, Min = {1}ms, Max = {2}ms, Average = {3}ms, StdDev = {4}ms",
                                  Count, Min, Max, Average, StdDev);
                    if (null == Xtra) {
                        return top;
                    } else {
                        StringBuilder sb = new StringBuilder (top);
                        foreach (var key in Xtra.Keys) {
                            sb.Append (Environment.NewLine);
                            sb.Append (string.Format ("    Key: {0}", key));
                            sb.Append (Xtra [key].ToString ());
                        }
                        return sb.ToString ();
                    }
                } else {
                    return string.Format ("Count = {0}, Min = {1}, Max = {2}, Average = {3}, StdDev = {4}",
                        Count, Min, Max, Average, StdDev);
                }
            }

            public void Report (string kind)
            {
                Telemetry.RecordCapture (kind, Count, Min, Max, Total, Total2);
                if (kind.StartsWith ("SSAOCE")) {
                    Console.WriteLine ("SLUGGO! {0},count={1},min={2},max={3},total={4},total2={5}",
                        kind, Count, Min, Max, Total, Total2);
                }
                if (null != Xtra) {
                    foreach (var key in Xtra.Keys) {
                        Xtra [key].Report (key);
                    }
                }
                Reset ();
            }
        }

        private class NcCaptureKind
        {
            public Summary Summary;
            public List<NcCapture> CaptureList;

            public NcCaptureKind ()
            {
                Summary = new Summary (true);
                CaptureList = new List<NcCapture> ();
            }

            public void Add (NcCapture capture)
            {
                CaptureList.Add (capture);
            }

            public void Remove (NcCapture capture)
            {
                bool result = CaptureList.Remove (capture);
                NcAssert.True (result);
            }
        }

        private string Kind;
        private IStopwatch Watch;
        // Note that we cannot use Stopwatch.IsRunning property because when going
        // to background, we need to stop the capture but remember that it
        // needs to restart when waking up.
        private bool _IsRunning;

        public bool IsRunning {
            get {
                return _IsRunning;
            }
        }

        private static object ClassLockObj;
        private static Dictionary<string, NcCaptureKind> PerKind;
        public static Type StopwatchClass = typeof(PlatformStopwatch);
        // For periodically report to telemetry
        private static NcTimer ReportTimer;
        // For keep track of how long the sleep is
        private static IStopwatch SleepWatch;

        private NcCapture (string kind)
        {
            NcAssert.True (null != ClassLockObj);
            lock (ClassLockObj) {
                NcAssert.True (PerKind.ContainsKey (kind));

                Kind = kind;
                _IsRunning = false;
                Watch = (IStopwatch)Activator.CreateInstance (StopwatchClass);

                // Add to the global tracking list
                NcAssert.True (PerKind.ContainsKey (kind));
                PerKind [kind].Add (this);
            }
        }

        public void Dispose ()
        {
            lock (ClassLockObj) {
                // Remove self from the global tracking list
                PerKind [Kind].Remove (this);
            }
        }

        public static bool AddKind (string kind)
        {
            if (null == ClassLockObj) {
                ClassLockObj = new object ();
            }
            lock (ClassLockObj) {
                if (null == PerKind) {
                    PerKind = new Dictionary<string, NcCaptureKind> ();
                }
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
                    return false;
                }
                PerKind.Remove (kind);
                return true;
            }
        }

        public static string Summarize (string kind)
        {
            // Note that we don't take ClassLockObj here because there is no
            // way to delete a kind.
            return string.Format ("[Kind: {0}] ", kind) + PerKind [kind].Summary.ToString ();
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
            Watch.Start ();
            _IsRunning = true;
        }

        public void Pause ()
        {
            if (IsRunning) {
                Watch.Stop ();
            }
        }

        public void Resume ()
        {
            if (IsRunning) {
                Watch.Start ();
            }
        }

        public void Stop ()
        {
            Stop (null);
        }

        public long ElapsedMilliseconds { get { return Watch.ElapsedMilliseconds; } }

        public void Stop (Dictionary<string,int> xtra)
        {
            Watch.Stop ();
            var summary = PerKind [Kind].Summary;
            summary.Update ((uint)Watch.ElapsedMilliseconds);
            _IsRunning = false;
        }

        public void Reset ()
        {
            Watch.Reset ();
            _IsRunning = false;
        }

        public static void ReportKind (string Kind)
        {
            if (!PerKind.ContainsKey (Kind)) {
                return;
            }
            Summary summary = PerKind [Kind].Summary;
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
                foreach (NcCapture capture in PerKind [Kind].CaptureList) {
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
    }
}

