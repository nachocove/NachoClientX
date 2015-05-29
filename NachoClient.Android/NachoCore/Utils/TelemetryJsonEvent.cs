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
        public long timestamp;
        public string event_type;

        public TelemetryJsonEvent ()
        {
            id = Guid.NewGuid ().ToString ().Replace ("-", "");
            client = NcApplication.Instance.ClientId;
            timestamp = DateTime.UtcNow.Ticks;
        }

        public string ToJson ()
        {
            return JsonConvert.SerializeObject (this);
        }
    }

    public class TelemetryLogEvent : TelemetryJsonEvent
    {
        public const string ERROR = "ERROR";
        public const string WARN = "WARN";
        public const string INFO = "INFO";
        public const string DEBUG = "DEBUG";

        public int thread_id;
        public string message;

        public TelemetryLogEvent () : this (TelemetryEventType.ERROR)
        {
        }

        public TelemetryLogEvent (TelemetryEventType type)
        {
            switch (type) {
            case TelemetryEventType.ERROR:
                event_type = ERROR;
                break;
            case TelemetryEventType.WARN:
                event_type = WARN;
                break;
            case TelemetryEventType.INFO:
                event_type = INFO;
                break;
            case TelemetryEventType.DEBUG:
                event_type = DEBUG;
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("RecordLogEvent: unexpected type {0}", type));
            }
        }
    }

    public class TelemetryWbxmlEvent : TelemetryJsonEvent
    {
        public const string REQUEST = "WBXML_REQUEST";
        public const string RESPONSE = "WBXML_RESPONSE";

        public byte[] wbxml;

        public TelemetryWbxmlEvent () : this (TelemetryEventType.WBXML_REQUEST)
        {
        }

        public TelemetryWbxmlEvent (TelemetryEventType type)
        {
            switch (type) {
            case TelemetryEventType.WBXML_REQUEST:
                event_type = REQUEST;
                break;
            case TelemetryEventType.WBXML_RESPONSE:
                event_type = RESPONSE;
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("RecordWbxmlEvent: unexpected type {0}", type));
            }
        }
    }

    public class TelemetryCounterEvent : TelemetryJsonEvent
    {
        public const string COUNTER = "COUNTER";

        public string counter_name;
        public long count;
        public long counter_start;
        public long counter_end;

        public TelemetryCounterEvent ()
        {
            event_type = COUNTER;
        }
    }

    public class TelemetrySamplesEvent : TelemetryJsonEvent
    {
        public const string SAMPLES = "SAMPLES";

        public string samples_name;
        public List<int> samples;

        public TelemetrySamplesEvent ()
        {
            event_type = SAMPLES;
        }
    }

    public class TelemetryTimeSeriesSamplesEvent : TelemetryJsonEvent
    {
        public const string TIME_SERIES = "TIME_SERIES";

        public string time_series_name;
        public List<long> time_series_timestamp;
        public List<int> time_series_samples;

        public TelemetryTimeSeriesSamplesEvent ()
        {
            event_type = TIME_SERIES;
        }
    }

    public class TelemetryDistributionEvent : TelemetryJsonEvent
    {
        public const string DISTRIBUTION = "DISTRIBUTION";

        public string distribution_name;
        public List<KeyValuePair<int, int>> cdf;

        public TelemetryDistributionEvent ()
        {
            event_type = DISTRIBUTION;
        }
    }

    public class TelemetryStatistics2Event : TelemetryJsonEvent
    {
        public const string STATISTICS2 = "STATISTICS2";

        public string stat2_name;
        public int count;
        public int min;
        public int max;
        public long sum;
        public long sum2;

        public TelemetryStatistics2Event ()
        {
            event_type = STATISTICS2;
        }
    }

    public class TelemetryUiEvent : TelemetryJsonEvent
    {
        public const string UI = "UI";

        public string ui_type;
        public string ui_object;
        public string ui_string;
        public long ui_long;

        public TelemetryUiEvent ()
        {
            event_type = UI;
        }
    }

    public class TelemetrySupportEvent : TelemetryJsonEvent
    {
        public const string SUPPORT = "SUPPORT";

        public string support;

        public TelemetrySupportEvent ()
        {
            event_type = SUPPORT;
        }
    }

    public class TelemetryDeviceInfoEvent : TelemetryJsonEvent
    {
        public string os_type;
        public string os_version;
        public string device_model;
        public string build_version;
        public string build_number;
        public string device_id;
        public bool fresh_install;
        public string user_id;
    }
}

