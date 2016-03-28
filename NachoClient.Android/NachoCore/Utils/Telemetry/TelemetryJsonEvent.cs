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
        public string user;
        public string timestamp;
        public string event_type;
        [JsonIgnore]
        public Action callback;

        public TelemetryJsonEvent ()
        {
            id = Guid.NewGuid ().ToString ().Replace ("-", "");
            client = NcApplication.Instance.ClientId;
            user = NcApplication.Instance.UserId;
            timestamp = AwsDateTime (DateTime.UtcNow);
        }

        public string ToJson ()
        {
            return JsonConvert.SerializeObject (this, Newtonsoft.Json.Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }

        public static string AwsDateTime (DateTime timestamp)
        {
            return String.Format ("{0:yyyy-MM-dd HH:mm:ss.fff}", timestamp);
        }

        public DateTime Timestamp ()
        {
            return Timestamp (timestamp);
        }

        public static DateTime Timestamp (string timestamp)
        {
            if (23 != timestamp.Length) {
                throw new FormatException (timestamp);
            }
            try {
                int year = int.Parse (timestamp.Substring (0, 4));
                int month = int.Parse (timestamp.Substring (5, 2));
                int day = int.Parse (timestamp.Substring (8, 2));
                int hour = int.Parse (timestamp.Substring (11, 2));
                int minute = int.Parse (timestamp.Substring (14, 2));
                int second = int.Parse (timestamp.Substring (17, 2));
                int millisecond = int.Parse (timestamp.Substring (20, 3));
                return new DateTime (year, month, day, hour, minute, second, millisecond, DateTimeKind.Utc);
            } catch {
                throw new FormatException (timestamp);
            }
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
        public string module;

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

    public class TelemetryProtocolEvent : TelemetryJsonEvent
    {
        public const string WBXML_REQUEST = "WBXML_REQUEST";
        public const string WBXML_RESPONSE = "WBXML_RESPONSE";
        public const string IMAP_REQUEST = "IMAP_REQUEST";
        public const string IMAP_RESPONSE = "IMAP_RESPONSE";

        public byte[] payload;

        public TelemetryProtocolEvent () : this (TelemetryEventType.WBXML_REQUEST)
        {
        }

        public TelemetryProtocolEvent (TelemetryEventType type)
        {
            switch (type) {
            case TelemetryEventType.WBXML_REQUEST:
                event_type = WBXML_REQUEST;
                break;
            case TelemetryEventType.WBXML_RESPONSE:
                event_type = WBXML_RESPONSE;
                break;
            case TelemetryEventType.IMAP_REQUEST:
                event_type = IMAP_REQUEST;
                break;
            case TelemetryEventType.IMAP_RESPONSE:
                event_type = IMAP_RESPONSE;
                break;
            default:
                var msg = String.Format ("TelemetryProtocolEvent: unexpected type {0}", type);
                throw new NcAssert.NachoDefaultCaseFailure (msg);
            }
        }
    }

    public class TelemetryCounterEvent : TelemetryJsonEvent
    {
        public const string COUNTER = "COUNTER";

        public string counter_name;
        public long count;
        public string counter_start;
        public string counter_end;

        public TelemetryCounterEvent ()
        {
            event_type = COUNTER;
        }
    }

    public class TelemetrySamplesEvent : TelemetryJsonEvent
    {
        public const string SAMPLES = "SAMPLES";

        public string sample_name;
        public int? sample_int;
        public double? sample_float;
        public string sample_string;

        public TelemetrySamplesEvent ()
        {
            event_type = SAMPLES;
        }
    }

    public class TelemetryTimeSeriesEvent : TelemetryJsonEvent
    {
        public const string TIME_SERIES = "TIME_SERIES";

        public string time_series_name;
        public string time_series_timestamp;
        public int? time_series_int;
        public double? time_series_float;
        public string time_series_string;

        public TelemetryTimeSeriesEvent ()
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

    public class TelemetrySupportRequestEvent : TelemetryJsonEvent
    {
        public const string SUPPORT_REQUEST = "SUPPORT_REQUEST";

        public string support;

        public TelemetrySupportRequestEvent ()
        {
            event_type = SUPPORT_REQUEST;
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

