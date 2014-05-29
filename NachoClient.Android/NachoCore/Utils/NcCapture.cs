//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NachoCore.Utils
{
    public class NcCapture
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

        private string Kind;
        private Stopwatch Watch;

        private static Dictionary<string,Summary> PerKind;

        private NcCapture (string kind)
        {
            Kind = kind;
            Watch = new Stopwatch ();
        }

        public static bool AddKind (string kind)
        {
            if (null == PerKind) {
                PerKind = new Dictionary<string, Summary> ();
            }
            if (PerKind.ContainsKey (kind)) {
                return false;
            } else {
                PerKind.Add (kind, new Summary (true));
                return true;
            }
        }

        public static string Summarize (string kind)
        {
            return string.Format ("Kind: {0}", kind) + PerKind [kind].ToString ();
        }

        public static string Summarize ()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var key in PerKind.Keys) {
                sb.Append (string.Format ("Kind: {0}", key));
                sb.Append (PerKind [key].ToString ());
                sb.Append (Environment.NewLine);
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
        }

        public void Stop ()
        {
            Stop (null);
        }

        public void Stop (Dictionary<string,int> xtra)
        {
            Watch.Stop ();
            var summary = PerKind [Kind];
            summary.Update ((uint)Watch.ElapsedMilliseconds);

        }

        public void Reset ()
        {
            Watch.Reset ();
        }

        public static void Report (string Kind)
        {
            if (!PerKind.ContainsKey(Kind)) {
                return;
            }
            Summary summary = PerKind [Kind];
            summary.Report (Kind);
        }

        public static void Report ()
        {
            foreach (string kind in PerKind.Keys) {
                Report (kind);
            }
        }
    }
}

