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
        CAPTURE,
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
                NcAssert.True (IsLogEvent());
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
                NcAssert.True (IsWbxmlEvent ());
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
                NcAssert.True (IsCounterEvent ());
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
                NcAssert.True (IsCounterEvent () || IsCaptureEvent ());
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
                NcAssert.True (IsCounterEvent ());
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
                NcAssert.True (IsCounterEvent ());
                _CounterEnd = value;
            }
        }


        // Capture Name
        private string _CaptureName;
        public string CaptureName {
            get {
                return _CaptureName;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _CaptureName = value;
            }
        }

        // Average
        private uint _Average;
        public uint Average {
            get {
                return _Average;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Average = value;
            }
        }

        // Capture Min
        private uint _Min;
        public uint Min {
            get {
                return _Min;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Min = value;
            }
        }

        // Capture Max
        private uint _Max;
        public uint Max {
            get {
                return _Max;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Max = value;
            }
        }

        // Capture Std. Dev
        private uint _StdDev;
        public uint StdDev {
            get {
                return _StdDev;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _StdDev = value;
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

        public static bool IsCaptureEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.CAPTURE == type);
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

        public bool IsCaptureEvent ()
        {
            return IsCaptureEvent (Type);
        }

        public TelemetryEvent (TelemetryEventType type)
        {
            Timestamp = DateTime.UtcNow;
            _Type = type;
            _Message = null;
            _Wbxml = null;
            _CounterName = null;
            _Count = 0;
            _CounterStart = new DateTime();
            _CounterEnd = new DateTime();
            _CaptureName = null;
            _Average = 0;
            _Min = 0;
            _Max = 0;
            _StdDev = 0;
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
                NcAssert.True (null != _SharedInstance);
                return _SharedInstance;
            }
        }

        private NcQueue<TelemetryEvent> EventQueue;

        private AutoResetEvent DbUpdated;

        private ITelemetryBE BackEnd;

        private NcTask TaskHandle;

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

            Type teleEvtType = typeof(TelemetryEventType);
            foreach (TelemetryEventType type in Enum.GetValues(teleEvtType)) {
                if ((TelemetryEventType.COUNTER == type) || 
                    (TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE == type) ||
                    (TelemetryEventType.UNKNOWN == type)) {
                    continue;
                }
                Counters [(int)type] = Counters [0].AddChild (Enum.GetName (teleEvtType, type));
            }
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
            SharedInstance.Counters [(int)tEvent.Type].Click ();
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

            NcAssert.True (TelemetryEvent.IsLogEvent (type));

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

            tEvent.CounterName = name;
            tEvent.Count = count;
            tEvent.CounterStart = start;
            tEvent.CounterEnd = end;

            RecordRawEvent (tEvent);
        }

        public static void RecordCapture (string name, uint count, uint average, uint min, uint max, uint stddev)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.CAPTURE);

            tEvent.CaptureName = name;
            tEvent.Count = count;
            tEvent.Average = average;
            tEvent.Min = min;
            tEvent.Max = max;
            tEvent.StdDev = stddev;

            RecordRawEvent (tEvent);
        }

        public void Start<T> () where T : ITelemetryBE, new()
        {
            if (!ENABLED) {
                return;
            }
            TaskHandle = NcTask.Run (() => {
                EventQueue.Token = TaskHandle.Token;
                Process<T> ();
            }, "Telemetry");
        }

        private void Process<T> () where T : ITelemetryBE, new()
        {
            BackEnd = new T ();
            Counters [0].ReportPeriod = 60 * 60; // report once per day

            // Capture the transaction time to telemetry server
            const string CAPTURE_NAME = "Telemetry.SendEvent";
            NcCapture.AddKind (CAPTURE_NAME);
            NcCapture transactionTime = NcCapture.Create(CAPTURE_NAME);

            while (!BackEnd.IsUseable ()) {
                NcTask.Delay (5000, TaskHandle.Token);
            }
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
                        // No pending event. Wait for one.
                        while (!DbUpdated.WaitOne (TaskHandle.PreferredCancellationTestInterval)) {
                            TaskHandle.Token.ThrowIfCancellationRequested ();
                        }
                        continue;
                    }
                    tEvent = dbEvent.GetTelemetryEvent ();
                }

                // Send it to the telemetry server
                transactionTime.Start ();
                BackEnd.SendEvent (tEvent);
                transactionTime.Stop ();
                transactionTime.Reset ();

                if (null != dbEvent) {
                    dbEvent.Delete ();
                }
            }
        }
    }

    public interface ITelemetryBE
    {
        bool IsUseable ();
        void SendEvent (TelemetryEvent tEvent);
    }
}
