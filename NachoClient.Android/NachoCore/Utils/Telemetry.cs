//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Threading;
using System.Diagnostics;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public enum TelemetryEventType {
        UNKNOWN = 0,
        ERROR,
        WARN,
        INFO,
        DEBUG,
        WBXML_REQUEST,
        WBXML_RESPONSE,
        STATE_MACHINE,
        COUNTER,
        MAX_TELEMETRY_EVENT_TYPE
    };

    [Serializable]
    public class TelemetryEvent : NcQueueElement
    {
        public DateTime Timestamp { set; get; }

        private TelemetryEventType _Type;
        public TelemetryEventType Type {
            get {
                return _Type;
            }
        }

        // The format string of a log message.
        private string _Message;
        public string Message { 
            get {
                return _Message;
            }
            set {
                NachoAssert.True (IsLogEvent());
                _Message = value;
            }
        }

        // WBXML bytes
        private byte[] _Wbxml;
        public byte[] Wbxml {
            get {
                return _Wbxml;
            }
            set {
                NachoAssert.True (IsWbxmlEvent ());
                _Wbxml = value;
            }
        }

        // Counter Name
        private string _CounterName;
        public string CounterName {
            get {
                return _CounterName;
            }
            set {
                NachoAssert.True (IsCounterEvent ());
                _CounterName = value;
            }
        }

        // Counter Count
        private Int64 _Count;
        public Int64 Count {
            get {
                return _Count;
            }
            set {
                NachoAssert.True (IsCounterEvent ());
                _Count = value;
            }
        }

        // Counter start time
        private DateTime _CounterStart;
        public DateTime CounterStart {
            get {
                return _CounterStart;
            }
            set {
                NachoAssert.True (IsCounterEvent ());
                _CounterStart = value;
            }
        }

        // Counter end time
        private DateTime _CounterEnd;
        public DateTime CounterEnd {
            get {
                return _CounterEnd;
            }
            set {
                NachoAssert.True (IsCounterEvent ());
                _CounterEnd = value;
            }
        }

        public static bool IsLogEvent (TelemetryEventType type)
        {
            return ((TelemetryEventType.ERROR == type) ||
            (TelemetryEventType.WARN == type) ||
            (TelemetryEventType.INFO == type) ||
            (TelemetryEventType.DEBUG == type));
        }

        public static bool IsWbxmlEvent (TelemetryEventType type)
        {
            return ((TelemetryEventType.WBXML_REQUEST == type) ||
            (TelemetryEventType.WBXML_RESPONSE == type));
        }

        public static bool IsCounterEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.COUNTER == type);
        }

        public bool IsLogEvent ()
        {
            return IsLogEvent (Type);
        }

        public bool IsWbxmlEvent ()
        {
            return IsWbxmlEvent (Type);
        }

        public bool IsCounterEvent ()
        {
            return IsCounterEvent (Type);
        }

        public TelemetryEvent (TelemetryEventType type)
        {
            Timestamp = DateTime.UtcNow;
            _Type = type;
            _Message = null;
            _Wbxml = null;
        }

        public uint GetSize ()
        {
            return 0;
        }
    }

    public class Telemetry
    {
        private static bool ENABLED = true;
        private static bool PERSISTED = true;
        // Parse has a maximum data size of 128K for PFObject. But the 
        // exact definition of data size of an object with multiple
        // fields is not clear. So, we just limit the log messages and 
        // redacted WBXML to 120 KB to leave some headroom for other fields.
        private const int MAX_PARSE_LEN = 120 * 1024;

        private static Telemetry _SharedInstance;
        public static Telemetry SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new Telemetry ();
                }
                NachoAssert.True (null != _SharedInstance);
                return _SharedInstance;
            }
        }

        private NcQueue<TelemetryEvent> EventQueue;

        private AutoResetEvent DbUpdated;

        private ITelemetryBE BackEnd;

        private Thread ProcessThread;

        NcCounter[] Counters;
  
        public Telemetry ()
        {
            EventQueue = new NcQueue<TelemetryEvent> ();
            BackEnd = null;
            DbUpdated = new AutoResetEvent (false);
            Counters = new NcCounter[(int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE];
            Counters[0] = new NcCounter ("Telemetry", true);
            Counters[0].AutoReset = true;
            Counters[0].ReportPeriod = 0;
            Counters [0].PreReportCallback = PreReportAdjustment;

            Counters [(int)TelemetryEventType.DEBUG] = Counters [0].AddChild ("DEBUG");
            Counters[(int)TelemetryEventType.INFO] = Counters[0].AddChild ("INFO");
            Counters[(int)TelemetryEventType.WARN] = Counters[0].AddChild ("WARN");
            Counters[(int)TelemetryEventType.ERROR] = Counters[0].AddChild ("ERROR");
            Counters[(int)TelemetryEventType.WBXML_REQUEST] = Counters[0].AddChild ("WBXML_REQUEST");
            Counters[(int)TelemetryEventType.WBXML_RESPONSE] = Counters[0].AddChild ("WBXML_RESPONSE");
            Counters [(int)TelemetryEventType.STATE_MACHINE] = Counters [0].AddChild ("STATE_MACHINE");
            // Counter must be the last counter created!
            Counters[(int)TelemetryEventType.COUNTER] = Counters[0].AddChild ("COUNTER");
        }

        // This is kind of a hack. When Telemetry is reporting the counter values,
        // they are being updated. For example, root counter "Telemetry" is updated
        // 8 more times (for 8 event types) after it is reported. So, its count is 
        // wrong. The fix is pre-adjust the increment that will happen during the
        // reporting. All counts are reset after reporting so the adjustment has
        // no longer effect at all.
        private static void PreReportAdjustment ()
        {
            SharedInstance.Counters [(int)TelemetryEventType.COUNTER].Click (1);
            SharedInstance.Counters [0].Click ((int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE - 1);
        }

        private static void RecordRawEvent (TelemetryEvent tEvent)
        {
            if (!PERSISTED) {
                SharedInstance.EventQueue.Enqueue (tEvent);
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
                Telemetry.SharedInstance.DbUpdated.Set ();
            }
        }

        public static void RecordLogEvent (TelemetryEventType type, string fmt, params object[] list)
        {
            if (!ENABLED) {
                return;
            }

            NachoAssert.True (TelemetryEvent.IsLogEvent (type));
            SharedInstance.Counters [(int)type].Click ();

            TelemetryEvent tEvent = new TelemetryEvent (type);
            tEvent.Message = String.Format(fmt, list);

            if (MAX_PARSE_LEN < tEvent.Message.Length) {
                // Truncate the message
                tEvent.Message = tEvent.Message.Substring (0, MAX_PARSE_LEN - 4);
                tEvent.Message += " ...";
            }

            RecordRawEvent (tEvent);
        }

        public static void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEventType type;
            if (isRequest) {
                type = TelemetryEventType.WBXML_REQUEST;
            } else {
                type = TelemetryEventType.WBXML_RESPONSE;
            }
            TelemetryEvent tEvent = new TelemetryEvent(type);
            SharedInstance.Counters [(int)type].Click ();

            if (MAX_PARSE_LEN < wbxml.Length) {
                Console.WriteLine ("Redacted WBXML too long (length={0})", wbxml.Length);
                StackTrace st = new StackTrace ();
                Console.WriteLine ("{0}", st.ToString ());
                // Can't truncate the WBXML and still have it remain valid.
                // TODO - Need to think of a better solution
            }

            tEvent.Wbxml = wbxml;

            RecordRawEvent (tEvent);
        }

        public static void RecordCounter (string name, Int64 count, DateTime start, DateTime end)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.COUNTER);
            SharedInstance.Counters [(int)TelemetryEventType.COUNTER].Click ();

            tEvent.CounterName = name;
            tEvent.Count = count;
            tEvent.CounterStart = start;
            tEvent.CounterEnd = end;

            RecordRawEvent (tEvent);
        }

        public void Start<T> () where T : ITelemetryBE, new()
        {
            if (!ENABLED) {
                return;
            }
            ProcessThread = new Thread (new ThreadStart (this.Process<T>));
            ProcessThread.Start ();
        }

        private void Process<T> () where T : ITelemetryBE, new()
        {
            BackEnd = new T ();
            Counters [0].ReportPeriod = 60 * 60 * 24; // report once per day
            while (true) {
                // TODO - We need to be smart about when we run. 
                // For example, if we don't have WiFi, it may not be a good
                // idea to upload a lot of data. The exact algorithm is TBD.
                TelemetryEvent tEvent = null;
                McTelemetryEvent dbEvent = null;
                if (!PERSISTED) {
                    tEvent = EventQueue.Dequeue ();
                } else {
                    dbEvent = McTelemetryEvent.QueryOne ();
                    if (null == dbEvent) {
                        DbUpdated.WaitOne ();
                        continue;
                    }
                    tEvent = dbEvent.GetTelemetryEvent ();
                }
                BackEnd.SendEvent (tEvent);
                if (null != dbEvent) {
                    dbEvent.Delete ();
                }
            }
        }
    }

    public interface ITelemetryBE
    {
        void SendEvent (TelemetryEvent tEvent);
    }
}
