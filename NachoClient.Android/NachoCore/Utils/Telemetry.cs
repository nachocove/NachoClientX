//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Threading;
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
        private static bool PERSISTED = true;

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
            tEvent.Wbxml = wbxml;

            if (!PERSISTED) {
                SharedInstance.EventQueue.Enqueue (tEvent);
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
                Telemetry.SharedInstance.DbUpdated.Set ();
            }
        }

        public void Start (ITelemetryBE backEnd)
        {
            BackEnd = backEnd;
            ProcessThread = new Thread (new ThreadStart (this.Process));
            ProcessThread.Start ();
        }

        private void Process ()
        {
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
