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
        public string id;
        public string client;
        public DateTime timestamp;
        public string event_type;

        public TelemetryJsonEvent ()
        {
        }

        public string ToJson ()
        {
            return JsonConvert.SerializeObject (this);
        }
    }

    public class TelemetryLogEvent : TelemetryJsonEvent
    {
        public int thread_id;
        public string message;
    }

    public class TelemetryWbxmlEvent : TelemetryJsonEvent
    {
        public byte[] wbxml;
    }

    public class TelemetryCounterEvent : TelemetryJsonEvent
    {
        public string counter_name;
        public Int64 count;
        public DateTime counter_start;
        public DateTime counter_end;
    }

    public class TelemetrySamplesEvent : TelemetryJsonEvent
    {
        public string samples_name;
        public List<int> samples;
    }

    public class TelemetryTimeSeriesSamplesEvent : TelemetryJsonEvent
    {
        public string time_series_name;
        public List<DateTime> time_series_timestamp;
        public List<int> time_series_samples;
    }

    public class TelemetryDistributionEvent : TelemetryJsonEvent
    {
        public string distribution_name;
        public List<KeyValuePair<int, int>> cdf;
    }

    public class TelemetryStatistics2Event : TelemetryJsonEvent
    {
        public string stat2_name;
        public int count;
        public int min;
        public int max;
        public long sum;
        public long sum2;
    }

    public class TelemetryUiEvent : TelemetryJsonEvent
    {
        public string ui_type;
        public string ui_object;
        public string ui_string;
        public long ui_long;
    }

    public class TelemetrySupportEvent : TelemetryJsonEvent
    {
        public string support;
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

