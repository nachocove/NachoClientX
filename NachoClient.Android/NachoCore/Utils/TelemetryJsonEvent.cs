//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace NachoCore.Utils
{
    /// <summary>
    /// Telemetry JSON events are used for converting to JSON via Json.Net library.
    /// TODO - replace the teledb mechanism by these JSON files directly.
    /// </summary>
    public class TelemetryJsonEvent
    {
        public string ServerId;
        public DateTime TimeStamp;
        public string EventType;

        public TelemetryJsonEvent ()
        {
        }

        public string ToJson ()
        {
            return JsonConvert.SerializeObject (this);
        }
    }

    public class TelemetryLogJsonEvent : TelemetryJsonEvent
    {
        public int ThreadId;
        public string Message;
    }

    public class TelemetryWbxmlJsonEvent : TelemetryJsonEvent
    {
        public byte[] Wbxml;
    }

    public class TelemetryCounterJsonEvent : TelemetryJsonEvent
    {
        public string CounterName;
        public Int64 Count;
        public DateTime CounterStart;
        public DateTime CounterEnd;
    }

    public class TelemetrySamplesJsonEvent : TelemetryJsonEvent
    {
        public string SamplesName;
        public List<int> Samples;
    }

    public class TelemetryTimeSeriesSamplesJsonEvent : TelemetryJsonEvent
    {
        public string TimeSeriesName;
        public List<KeyValuePair<DateTime, int>> TimeSeriesSamples;
    }

    public class TelemetryDistributionJsonEvent : TelemetryJsonEvent
    {
        public string DistributionName;
        public List<KeyValuePair<int, int>> Cdf;
    }

    public class TelemetryUiJsonEvent : TelemetryJsonEvent
    {
        public string UiType;
        public string UiObject;
        public string UiString;
        public long UiLong;
    }

    public class TelemetrySupportJsonEvent : TelemetryJsonEvent
    {
        public string Support;
    }

    public class TelemetryJsonFile
    {
        protected string FilePath;
        protected FileStream JsonFile;

        public int NumberOfEntries { get; protected set; }

        public TelemetryJsonFile (string path)
        {
            FilePath = path;
            if (File.Exists (FilePath)) {
                JsonFile = File.Open (FilePath, FileMode.Create, FileAccess.Write);
                Append ("[");
            } else {
                JsonFile = File.Open (FilePath, FileMode.Open, FileAccess.Write);
                // Count how many lines;
                using (var reader = new StreamReader (JsonFile)) {
                    while (!String.IsNullOrEmpty (reader.ReadLine ())) {
                        NumberOfEntries += 1;
                    }
                }
                JsonFile.Seek (0, SeekOrigin.End);
            }
        }

        protected void Append (string data)
        {
            byte[] bytes = Encoding.ASCII.GetBytes (data);
            JsonFile.Write (bytes, 0, bytes.Length);
        }

        public bool Add (TelemetryJsonEvent tEvent)
        {
            bool succeeded;
            try {
                if (0 == NumberOfEntries) {
                    Append (tEvent.ToJson ());
                } else {
                    Append (",\n" + tEvent.ToJson ());
                }
                NumberOfEntries += 1;
                succeeded = true;
                JsonFile.Flush ();
            } catch (IOException e) {
                Log.Warn (Log.LOG_UTILS, "fail to write a telemetry JSON event ({0})", e);
                succeeded = false;
            }
            return succeeded;
        }

        public void Close ()
        {
            Append ("]");
            JsonFile.Close ();
        }
    }
}

