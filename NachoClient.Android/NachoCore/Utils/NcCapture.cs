//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    // An interface for both the real and the mock version
    public interface IStopwatch
    {
        long ElapsedMilliseconds { get; }

        void Start ();

        void Stop ();

        void Reset ();
    }

    // Since System.Diagnostics.Stopwatch does not inherit from IStopwatch,
    // we need to create a class that wraps Stopwatch and exports the 
    // interface required by IStopwatch.
    public class PlatformStopwatch : IStopwatch
    {
        private Stopwatch Watch;

        public long ElapsedMilliseconds {
            get {
                return Watch.ElapsedMilliseconds;
            }
        }

        public PlatformStopwatch ()
        {
            Watch = new Stopwatch ();
        }

        public void Start ()
        {
            Watch.Start ();
        }

        public void Stop ()
        {
            Watch.Stop ();
        }

        public void Reset ()
        {
            Watch.Reset ();
        }
    }

    public class NcCapture : IDisposable
    {
        private class Summary
        {
            bool IsTop;
            uint Min;
            uint Max;
            uint Average {
                // Only compute average when asked to save some divide
                get {
                    return (uint)(Total / Count);
                }
            }
            uint Count;
            UInt64 Total;
            Dictionary <string, Summary> Xtra;

            public Summary (bool isTop)
            {
                IsTop = isTop;
                Min = uint.MaxValue;
                Max = uint.MinValue;
                Count = 0;
                Total = 0;
            }

            public void Update (uint value, Dictionary<string,uint> xtra)
            {
                Count += 1;
                Total += value;
                if (value < Min) {
                    Min = value;
                }
                if (value > Max) {
                    Max = value;
                }
                if (null != xtra) {
                    if (null == Xtra) {
                        Xtra = new Dictionary<string, Summary> ();
                    }
                    foreach (var key in xtra.Keys) {
                        Summary summary;
                        if (! Xtra.TryGetValue (key, out summary)) {
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

            public override string ToString ()
            {
                if (IsTop) {
                    var top = string.Format ("Count = {0}, Min = {1}ms, Max = {2}ms, Average = {3}ms",
                        Count, Min, Max, Average);
                    if (null == Xtra) {
                        return top;
                    } else {
                        StringBuilder sb = new StringBuilder(top);
                        foreach (var key in Xtra.Keys) {
                            sb.Append (Environment.NewLine);
                            sb.Append (string.Format("    Key: {0}", key));
                            sb.Append (Xtra [key].ToString ());
                        }
                        return sb.ToString ();
                    }
                } else {
                    return string.Format ("Count = {1}, Min = {2}, Max = {3}, Average = {4}",
                        Count, Min, Max, Average);
                }
            }

            public void Report (string kind)
            {
                Telemetry.RecordCapture (kind, Count, Average, Min, Max);
                if (null != Xtra) {
                    foreach (var key in Xtra.Keys) {
                        Xtra [key].Report (key);
                    }
                }
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
                CaptureList.Add(capture);
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
        public static bool RemoveKind(string kind)
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
            StringBuilder sb = new StringBuilder();
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

        public static void Report (string Kind)
        {
            if (!PerKind.ContainsKey(Kind)) {
                return;
            }
            Summary summary = PerKind [Kind].Summary;
            summary.Report (Kind);
        }

        public static void Report ()
        {
            lock (ClassLockObj) {
                foreach (string kind in PerKind.Keys) {
                    Report (kind);
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

        public static void ResumeAll ()
        {
            lock (ClassLockObj) {
                foreach (string kind in PerKind.Keys) {
                    ResumeKind (kind);
                }
            }
        }

        public static void PauseAll ()
        {
            lock (ClassLockObj) {
                foreach (string kind in PerKind.Keys) {
                    PauseKind (kind);
                }
            }
        }
    }
}

