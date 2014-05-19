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
        COUNTERS,
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

        public bool IsLogEvent ()
        {
            return IsLogEvent (Type);
        }

        public bool IsWbxmlEvent ()
        {
            return IsWbxmlEvent (Type);
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
        private static bool PERSISTED = false;
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
  
        public Telemetry ()
        {
            EventQueue = new NcQueue<TelemetryEvent> ();
            BackEnd = null;
            DbUpdated = new AutoResetEvent (false);
        }

        public static void RecordLogEvent (TelemetryEventType type, string fmt, params object[] list)
        {
            if (!ENABLED) {
                return;
            }

            NachoAssert.True (TelemetryEvent.IsLogEvent (type));
            TelemetryEvent tEvent = new TelemetryEvent (type);
            tEvent.Message = String.Format(fmt, list);

            if (MAX_PARSE_LEN < tEvent.Message.Length) {
                // Truncate the message
                tEvent.Message = tEvent.Message.Substring (0, MAX_PARSE_LEN - 4);
                tEvent.Message += " ...";
            }

            if (!PERSISTED) {
                SharedInstance.EventQueue.Enqueue (tEvent);
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
                Telemetry.SharedInstance.DbUpdated.Set ();
            }
        }

        public static void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent;
            if (isRequest) {
                tEvent = new TelemetryEvent (TelemetryEventType.WBXML_REQUEST);
            } else {
                tEvent = new TelemetryEvent (TelemetryEventType.WBXML_RESPONSE);
            }
            if (MAX_PARSE_LEN < wbxml.Length) {
                Console.WriteLine ("Redacted WBXML too long (length={0})", wbxml.Length);
                StackTrace st = new StackTrace ();
                Console.WriteLine ("{0}", st.ToString ());
                // Can't truncate the WBXML and still have it remain valid.
                // TODO - Need to think of a better solution
            }

            tEvent.Wbxml = wbxml;

            if (!PERSISTED) {
                SharedInstance.EventQueue.Enqueue (tEvent);
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
                Telemetry.SharedInstance.DbUpdated.Set ();
            }
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
