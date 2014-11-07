//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.Util;

using NachoClient.Build;

namespace NachoCore.Utils
{
    public class TelemetryBEAWS : ITelemetryBE
    {
        private static bool Initialized = false;

        private static AmazonDynamoDBClient Client;
        private static Table LogTable;
        private static Table SupportTable;
        private static Table CounterTable;
        private static Table CaptureTable;
        private static Table UiTable;
        private static Table WbxmlTable;

        public TelemetryBEAWS ()
        {
            BasicAWSCredentials cred = new BasicAWSCredentials ("dynamodb_local", "dynamodb_local");

            // FIXME
            var config = new AmazonDynamoDBConfig();
            config.ServiceURL = "http://localhost:8000";
            config.UseHttp = true;
            config.Timeout = new TimeSpan (0, 0, 5);
            config.AuthenticationRegion = "localhost";
            Client = new AmazonDynamoDBClient (cred, config);

            LogTable = Table.LoadTable (Client, TableName ("log"));
            SupportTable = Table.LoadTable (Client, TableName ("support"));
            CounterTable = Table.LoadTable (Client, TableName ("counter"));
            CaptureTable = Table.LoadTable (Client, TableName ("capture"));
            UiTable = Table.LoadTable (Client, TableName ("ui"));
            WbxmlTable = Table.LoadTable (Client, TableName ("wbxml"));
        }

        public bool IsUseable ()
        {
            return true;
        }

        public string GetUserName ()
        {
            return "HardCodedClientId";
        }

        public bool SendEvent (TelemetryEvent tEvent)
        {
            if (!Initialized) {
                SendDeviceInfo ();
                Initialized = true;
            }

            Table eventTable = null;
            Document eventItem = null;

            if (tEvent.IsLogEvent ()) {
                eventItem = LogEvent (tEvent);
                eventTable = LogTable;
            } else if (tEvent.IsSupportEvent ()) {
                eventItem = SupportEvent (tEvent);
                eventTable = SupportTable;
            } else if (tEvent.IsCounterEvent ()) {
                eventItem = CounterEvent (tEvent);
                eventTable = CounterTable;
            } else if (tEvent.IsCaptureEvent ()) {
                eventItem = CaptureEvent (tEvent);
                eventTable = CaptureTable;
            } else if (tEvent.IsUiEvent ()) {
                eventItem = UiEvent (tEvent);
                eventTable = UiTable;
            } else if (tEvent.IsWbxmlEvent()) {
                eventItem = WbxmlEvent (tEvent);
                eventTable = WbxmlTable;
            } else {
                NcAssert.True (false);
            }
            return AwsSendEvent (eventTable, eventItem);
        }

        private string TableName (string name)
        {
            return BuildInfo.ProjectPrefix + ".telemetry." + name;
        }

        private Document InitializeEvent (TelemetryEvent tEvent)
        {
            var anEvent = new Document ();
            // Client and timeestamp are the only common fields for all event tables.
            // They are also the primary keys.
            anEvent ["id"] = Guid.NewGuid ().ToString ().Replace ("-", "");
            anEvent ["client"] = GetUserName ();
            anEvent ["timestamp"] = tEvent.Timestamp.Ticks;
            return anEvent;
        }

        private bool AwsSendEvent (Table eventTable, Document eventItem)
        {
            try {
                // FIXME - Add cancellation token
                var task = eventTable.PutItemAsync (eventItem);
                task.Wait ();
            }
            catch (AmazonServiceException e) {
                Console.WriteLine ("AWS exception {0}", e);
                return false;
            }
            return true;
        }

        private void SendDeviceInfo ()
        {

        }

        private Document LogEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            switch (tEvent.Type) {
            case TelemetryEventType.ERROR:
                anEvent ["event_type"] = "ERROR";
                break;
            case TelemetryEventType.WARN:
                anEvent ["event_type"] = "WARN";
                break;
            case TelemetryEventType.INFO:
                anEvent ["event_type"] = "INFO";
                break;
            case TelemetryEventType.DEBUG:
                anEvent ["event_type"] = "DEBUG";
                break;
            default:
                var msg = String.Format ("unknown log event type {0}", (int)tEvent.Type);
                throw new NcAssert.NachoDefaultCaseFailure (msg);
            }
            anEvent ["message"] = tEvent.Message;
            // FIXME - Add thread id and subsystem
            return anEvent;
        }

        private Document SupportEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["client"] = GetUserName ();
            anEvent ["support"] = tEvent.Support;
            return anEvent;
        }

        private Document CounterEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["counter_name"] = tEvent.CounterName;
            anEvent ["count"] = tEvent.Count;
            anEvent ["counter_start"] = tEvent.CounterStart;
            anEvent ["counter_end"] = tEvent.CounterEnd;
            return anEvent;
        }

        private Document CaptureEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["capture_name"] = tEvent.CaptureName;
            anEvent ["count"] = tEvent.Count;
            anEvent ["average"] = tEvent.Average;
            anEvent ["min"] = tEvent.Min;
            anEvent ["max"] = tEvent.Max;
            anEvent ["stddev"] = tEvent.StdDev;
            return anEvent;
        }

        private Document UiEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["ui_type"] = tEvent.UiType;
            switch (tEvent.UiType) {
            case TelemetryEvent.UIDATEPICKER:
                anEvent ["ui_string"] = tEvent.UiString;
                break;
            case TelemetryEvent.UIPAGECONTROL:
                anEvent ["ui_integer"] = (int)tEvent.UiLong;
                break;
            case TelemetryEvent.UISEGMENTEDCONTROL:
                anEvent ["ui_integer"] = (int)tEvent.UiLong;
                break;
            case TelemetryEvent.UISWITCH:
                anEvent ["ui_string"] = tEvent.UiString;
                break;
            case TelemetryEvent.UIVIEWCONTROLER:
                anEvent["ui_string"] = tEvent.UiString;
                break;
            }
            return anEvent;
        }

        private Document WbxmlEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            switch (tEvent.Type) {
            case TelemetryEventType.WBXML_REQUEST:
                anEvent ["event_type"] = "WBXML_REQUST";
                break;
            case TelemetryEventType.WBXML_RESPONSE:
                anEvent ["event_type"] = "WBXML_RESPONSE";
                break;
            default:
                var msg = String.Format ("unknown wbxml event type {0}", (int)tEvent.Type);
                throw new NcAssert.NachoDefaultCaseFailure (msg);
            }
            return anEvent;
        }
    }
}

