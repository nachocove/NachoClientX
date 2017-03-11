//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public abstract class NcSamplesInput
    {
        public bool LimitInput { get; set; }

        public int MaxInput { get; set ; }

        public int MinInput { get; set; }

        // If greater than 0, the # of samples added before it automatically reports.
        public int ReportThreshold { get; set; }

        // If greater than 0, the max duration (in sec) before it automatically reports.
        private int _ReportIntervalSec;

        public int ReportIntervalSec {
            get {
                return _ReportIntervalSec;
            }
            set {
                if (0 == _ReportIntervalSec) {
                    LastReported = DateTime.UtcNow;
                }
                _ReportIntervalSec = value;
            }
        }

        protected int Count;

        protected DateTime LastReported;

        protected object LockObj;

        public NcSamplesInput ()
        {
            LastReported = DateTime.UtcNow;
            LockObj = new object ();
        }

        /// <summary>
        /// This is the public interface for adding a new sample for all derived classes.
        /// It provides the functionality of range checking of input samples.
        /// </summary>
        /// <param name="value">Value.</param>
        public void AddSample (int value)
        {
            if (LimitInput) {
                if (value < MinInput) {
                    throw new ArgumentOutOfRangeException (
                        String.Format ("{0} is less than lower limit {1}", value, MinInput));
                }
                if (value > MaxInput) {
                    throw new ArgumentOutOfRangeException (
                        String.Format ("{0} is greater than upper limit {1}", value, MaxInput));
                }
            }
            lock (LockObj) {
                ProcessSample (value);
                Count += 1;
            }
            if ((0 < ReportThreshold) && (Count >= ReportThreshold)) {
                Report ();
                LastReported = DateTime.UtcNow;
            }
            if (0 < ReportIntervalSec) {
                var now = DateTime.UtcNow;
                if ((now - LastReported).TotalSeconds >= ReportIntervalSec) {
                    Report ();
                    LastReported = now;
                }
            }
        }

        /// <summary>
        /// Clear all states derived from the samples.
        /// </summary>
        public void Clear ()
        {
            lock (LockObj) {
                ClearSamples ();
                Count = 0;
            }
        }

        /// <summary>
        /// Upload all states to telemetry.
        /// </summary>
        public void Report ()
        {
            lock (LockObj) {
                RecordSamples ();
                Clear ();
            }
        }

        /// <summary>
        /// The internal function for processing of a sample. Must be overridden.
        /// </summary>
        /// <param name="value">Value.</param>
        protected abstract void ProcessSample (int value);

        /// <summary>
        /// The internal function for clearing all states (back to a 0-sample state). Mst be overridden.
        /// </summary>
        protected abstract void ClearSamples ();

        /// <summary>
        /// The internal function for recording the telemetry event from the internal states. Must be overridden.
        /// </summary>
        /// <returns>The telemetry event.</returns>
        protected abstract void RecordSamples ();
    }

    public class NcSamples : NcSamplesInput
    {
        public string Name { get; protected set; }

        protected List<int> Samples;

        public NcSamples (string name)
        {
            Name = name;
            Samples = new List<int> ();
        }

        protected override void ProcessSample (int value)
        {
            Samples.Add (value);
        }

        protected override void ClearSamples ()
        {
            Samples.Clear ();
        }

        protected override void RecordSamples ()
        {
            NcApplication.Instance.TelemetryService.RecordIntSamples (Name, Samples);
        }
    }
}

