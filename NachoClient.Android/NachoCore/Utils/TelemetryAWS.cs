//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
//#define AWS_DEBUG
using System;
using System.IO;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.Util;
using Amazon.CognitoIdentity;
using Amazon;

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

        private static string ClientId;

        public TelemetryBEAWS ()
        {
            var config = new AmazonDynamoDBConfig();
            config.UseHttp = false;
            config.AuthenticationRegion = "us-west-2";
            config.ServiceURL = "https://dynamodb.us-west-2.amazonaws.com";

            #if AWS_DEBUG
            BasicAWSCredentials credentials =
                new BasicAWSCredentials("ACCESS KEY", "SECRET ACCESS KEY");
            #else
            CognitoAWSCredentials credentials = new CognitoAWSCredentials(
                "610813048224",
                "us-east-1:0d40f2cf-bf6c-4875-a917-38f8867b59ef",
                "arn:aws:iam::610813048224:role/Cognito_dev_telemetryUnauth_DefaultRole",
                "NO PUBLIC AUTHENTICATION",
                RegionEndpoint.USEast1
            );
            Console.WriteLine (">>>>>>>> Cognito Id = {0}", credentials.GetIdentityId ());

            // We get a different Cognito id each time it runs because unauthenticated
            // identities (that we use) are anonymous. But doing so would mean it is
            // really hard to track a client's activity. So, we look for Documents/client_id.
            // If it does not exist, we save the current Cognito id into the file. After
            // the 1st time, we use the id in the file as the client id. Note that we 
            // still need to talk to Cognito in order to get the session token.
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            var clientIdFile = Path.Combine (documents, "client_id");
            if (File.Exists(clientIdFile)) {
                // Get the client id from the file
                using (var stream = new FileStream (clientIdFile, FileMode.Open, FileAccess.Read)) {
                    using (var reader = new StreamReader (stream)) {
                        ClientId = reader.ReadLine ();
                    }
                }
            } else {
                // Save the current Cognito id as client id
                ClientId = credentials.GetIdentityId ();
                using (var stream = new FileStream (clientIdFile, FileMode.Create, FileAccess.Write)) {
                    using (var writer = new StreamWriter (stream)) {
                        writer.WriteLine(ClientId);
                    }
                }
            }
            Console.WriteLine(">>>>>>>>> ClientId = {0}", ClientId);
            #endif

            Client = new AmazonDynamoDBClient (credentials, config);

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
            return ClientId;
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
            anEvent ["id"] = ClientId;
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

