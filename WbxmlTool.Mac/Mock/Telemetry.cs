//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

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

    public class Telemetry
    {
        public Telemetry ()
        {
        }

        public static void RecordLogEvent (TelemetryEventType type, string fmt, params object[] list)
        {
        }

        public static void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
        }
    }
}

