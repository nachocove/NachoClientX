//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class NcSamplesInput
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

        public NcSamplesInput ()
        {
            LastReported = DateTime.UtcNow;
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
            ProcessSample (value);
            Count += 1;
            if ((0 < ReportThreshold) && (Count >= ReportThreshold)) {
                Report ();
                LastReported = DateTime.UtcNow;
            }
            if (0 < ReportIntervalSec) {
                var now = DateTime.UtcNow;
                if ((now - LastReported).TotalSeconds >= ReportIntervalSec) {
                    Report ();
                }
                LastReported = now;
            }
        }

        /// <summary>
        /// Clear all states derived from the samples.
        /// </summary>
        public void Clear ()
        {
            ClearSamples ();
            Count = 0;
        }

        /// <summary>
        /// Upload all states to telemetry.
        /// </summary>
        public void Report ()
        {
            var tEvent = GenerateTelemetryEvent ();
            var dbEvent = new McTelemetryEvent (tEvent);
            dbEvent.Insert ();
            Clear ();
        }

        /// <summary>
        /// The internal function for processing of a sample. Must be overridden.
        /// </summary>
        /// <param name="value">Value.</param>
        protected virtual void ProcessSample (int value)
        {
            throw new NotImplementedException ();
        }

        /// <summary>
        /// The internal function for clearing all states (back to a 0-sample state). Mst be overridden.
        /// </summary>
        protected virtual void ClearSamples ()
        {
            throw new NotImplementedException ();
        }

        /// <summary>
        /// The internal function for generating the telemetry event from the internal states. Must be overridden.
        /// </summary>
        /// <returns>The telemetry event.</returns>
        protected virtual TelemetryEvent GenerateTelemetryEvent ()
        {
            throw new NotImplementedException ();
        }
    }

    public class NcSamples : NcSamplesInput
    {
        protected List<int> Samples;

        public NcSamples ()
        {
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

        protected override TelemetryEvent GenerateTelemetryEvent ()
        {
            return new TelemetryEvent (TelemetryEventType.SAMPLES) {
                Samples = this.Samples
            };
        }
    }
}

