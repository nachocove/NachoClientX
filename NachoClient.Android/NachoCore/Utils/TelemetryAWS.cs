//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
//#define AWS_DEBUG
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.Util;
using Amazon.CognitoIdentity;
using Amazon;

using NachoPlatform;
using NachoClient.Build;

namespace NachoCore.Utils
{
    public class TelemetryBEAWS : ITelemetryBE
    {
        private static bool Initialized = false;

        private static AmazonDynamoDBClient Client;
        private static Table DeviceInfoTable;
        private static Table LogTable;
        private static Table SupportTable;
        private static Table CounterTable;
        private static Table CaptureTable;
        private static Table UiTable;
        private static Table WbxmlTable;

        private static string _ClientId;

        private static string ClientId {
            get {
                return _ClientId;
            }
            set {
                _ClientId = value;
                // Currently, the telemetry client id is the application's client id.
                NcApplication.Instance.ClientId = _ClientId;
            }
        }

        private bool FreshInstall;

        public TelemetryBEAWS ()
        {
        }

        public void Initialize ()
        {
            InitializeTables ();
        }

        private void InitializeTables ()
        {
            var config = new AmazonDynamoDBConfig ();
            config.UseHttp = false;
            config.AuthenticationRegion = "us-west-2";
            config.ServiceURL = "https://dynamodb.us-west-2.amazonaws.com";
            // Disable exponential backoff to implement our own linear backoff scheme.
            config.MaxErrorRetry = 0;

            CognitoAWSCredentials credentials = new CognitoAWSCredentials (
                                                    BuildInfo.AwsAccountId,
                                                    BuildInfo.AwsIdentityPoolId,
                                                    BuildInfo.AwsUnauthRoleArn,
                                                    BuildInfo.AwsAuthRoleArn,
                                                    RegionEndpoint.USEast1
                                                );

            // We get a different Cognito id each time it runs because unauthenticated
            // identities (that we use) are anonymous. But doing so would mean it is
            // really hard to track a client's activity. So, we look for Documents/client_id.
            // If it does not exist, we save the current Cognito id into the file. After
            // the 1st time, we use the id in the file as the client id. Note that we 
            // still need to talk to Cognito in order to get the session token.
            var documents = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            var clientIdFile = Path.Combine (documents, "client_id");
            if (File.Exists (clientIdFile)) {
                // Get the client id from the file
                using (var stream = new FileStream (clientIdFile, FileMode.Open, FileAccess.Read)) {
                    using (var reader = new StreamReader (stream)) {
                        ClientId = reader.ReadLine ();
                    }
                }
                FreshInstall = false;
            } else {
                // Save the current Cognito id as client id
                Retry (() => {
                    ClientId = credentials.GetIdentityId ();
                });
                using (var stream = new FileStream (clientIdFile, FileMode.Create, FileAccess.Write)) {
                    using (var writer = new StreamWriter (stream)) {
                        writer.WriteLine (ClientId);
                    }
                }
                FreshInstall = true;
            }

            // Notify others (e.g. push assist SM) about the client id
            var result = NcResult.Info (NcResult.SubKindEnum.Info_PushAssistClientToken);
            result.Value = ClientId;
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Status = result,
            });

            Retry (() => {
                Client = new AmazonDynamoDBClient (credentials, config);

                DeviceInfoTable = Table.LoadTableAsync (Client, TableName ("device_info"), NcTask.Cts.Token);
                LogTable = Table.LoadTableAsync (Client, TableName ("log"), NcTask.Cts.Token);
                SupportTable = Table.LoadTableAsync (Client, TableName ("support"), NcTask.Cts.Token);
                CounterTable = Table.LoadTableAsync (Client, TableName ("counter"), NcTask.Cts.Token);
                CaptureTable = Table.LoadTableAsync (Client, TableName ("capture"), NcTask.Cts.Token);
                UiTable = Table.LoadTableAsync (Client, TableName ("ui"), NcTask.Cts.Token);
                WbxmlTable = Table.LoadTableAsync (Client, TableName ("wbxml"), NcTask.Cts.Token);
            });
        }

        private void Retry (Action action)
        {
            bool isDone = false;
            while (!isDone) {
                try {
                    action ();
                    isDone = true;
                } catch (Exception e) {
                    if (!HandleAWSException (e, false)) {
                        if (NcTask.Cts.Token.IsCancellationRequested) {
                            if (null != Client) {
                                Client.Dispose ();
                                Client = null;
                            }
                            NcTask.Cts.Token.ThrowIfCancellationRequested ();
                        }
                        throw;
                    }
                    NcTask.CancelableSleep (5000);
                }
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }

        public void ReinitializeTables ()
        {
            if (null != Client) {
                Client.Dispose ();
            }
            InitializeTables ();
        }

        public bool IsUseable ()
        {
            return true;
        }

        public string GetUserName ()
        {
            return ClientId;
        }

        public bool SendEvents (List<TelemetryEvent> tEvents)
        {
            if (!Initialized) {
                if (!SendDeviceInfo ()) {
                    return false;
                }
                Initialized = true;
            }

            var writeDict = new Dictionary<Table, DocumentBatchWrite> ();
            foreach (var tEvent in tEvents) {
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
                } else if (tEvent.IsWbxmlEvent ()) {
                    eventItem = WbxmlEvent (tEvent);
                    eventTable = WbxmlTable;
                } else {
                    NcAssert.True (false);
                }

                // Get the table batch write. Create one if it doesn't exist
                DocumentBatchWrite batchWrite;
                if (!writeDict.TryGetValue (eventTable, out batchWrite)) {
                    batchWrite = new DocumentBatchWrite (eventTable);
                    writeDict.Add (eventTable, batchWrite);
                }
                eventItem ["uploaded_at"] = DateTime.UtcNow.Ticks;
                batchWrite.AddDocumentToPut (eventItem);
            }

            var multiBatchWrite = new MultiTableDocumentBatchWrite ();
            foreach (var batchWrite in writeDict.Values) {
                multiBatchWrite.AddBatch (batchWrite);
            }
            return AwsSendBatchEvents (multiBatchWrite);
        }

        private string TableName (string name)
        {
            return BuildInfo.AwsPrefix + ".telemetry." + name;
        }

        private Document InitializeEvent (TelemetryEvent tEvent)
        {
            var anEvent = new Document ();
            // Client and timeestamp are the only common fields for all event tables.
            // They are also the primary keys.
            if (null == tEvent.ServerId) {
                // These are old events that do not have the ServerId field yet.
                // In that case, create an id for the event.
                anEvent ["id"] = Guid.NewGuid ().ToString ().Replace ("-", "");
            } else {
                anEvent ["id"] = tEvent.ServerId;
            }
            anEvent ["client"] = GetUserName ();
            anEvent ["timestamp"] = tEvent.Timestamp.Ticks;
            return anEvent;
        }

        /// <summary>
        /// Handles the AWS exception.
        /// </summary>
        /// <returns><c>true</c>, if AWS exception was handled, <c>false</c> otherwise.
        /// In that case, the caller must re-throw.</returns>
        /// <param name="e">E.</param>
        private bool HandleAWSException (Exception e, bool doReinitialize = true)
        {
            if (null != e) {
                if (e is AggregateException) {
                    return HandleAWSException (e.InnerException);
                }
                if (e is ProvisionedThroughputExceededException) {
                    return true;
                }
                if (e is TaskCanceledException) {
                    if (NcTask.Cts.Token.IsCancellationRequested) {
                        return false;
                    }
                    // Otherwise, most likely HTTP client timeout
                    Console.WriteLine ("Task canceled exception caught in AWS send event\n{0}", e);
                    return true;
                }
                if (e is OperationCanceledException) {
                    // Since we are catching Exception below, we must catch and re-throw
                    // or this exception will be swallowed and telemetry task will not exit.
                    return false;
                }
                if (e is AmazonDynamoDBException) {
                    Console.WriteLine ("AWS DynamoDB exception caught in AWS send event\n{0}", e);
                    ReinitializeTables ();
                    return true;
                }
                if (e is AmazonServiceException) {
                    Console.WriteLine ("AWS exception caught in AWS send event\n{0}", e);
                    return true;
                }
            }
            // FIXME - An exception is thrown but the exception is null.
            // This workaround simply catches everything and re-initializes
            // the connection and tables.
            Console.WriteLine ("Some exception caught in AWS send event\n{0}", e);
            if (doReinitialize) {
                ReinitializeTables ();
            }
            return true;
        }

        private bool AwsSendEvent (Action action)
        {
            try {
                action ();
            } catch (Exception e) {
                if (!HandleAWSException (e)) {
                    if (NcTask.Cts.Token.IsCancellationRequested) {
                        Client.Dispose ();
                        Client = null;
                        NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    }
                    throw;
                }
                // Linear backoff
                if (!NcTask.CancelableSleep (1000)) {
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                }
                return false;
            }
            return true;
        }

        private bool AwsSendOneEvent (Table eventTable, Document eventItem)
        {
            return AwsSendEvent (() => {
                eventItem ["uploaded_at"] = DateTime.UtcNow.Ticks;
                var task = eventTable.PutItemAsync (eventItem);
                task.Wait (NcTask.Cts.Token);
            });
        }

        private bool AwsSendBatchEvents (MultiTableDocumentBatchWrite multiBatchWrite)
        {
            return AwsSendEvent (() => {
                var task = multiBatchWrite.ExecuteAsync (NcTask.Cts.Token);
                task.Wait (NcTask.Cts.Token);
            });
        }

        private bool SendDeviceInfo ()
        {
            var anEvent = new Document ();
            anEvent ["id"] = Guid.NewGuid ().ToString ().Replace ("-", "");
            anEvent ["client"] = GetUserName ();
            anEvent ["timestamp"] = DateTime.UtcNow.Ticks;
            anEvent ["os_type"] = Device.Instance.OsType ();
            anEvent ["os_version"] = Device.Instance.OsVersion ();
            anEvent ["device_model"] = Device.Instance.Model ();
            anEvent ["build_version"] = BuildInfo.Version;
            anEvent ["build_number"] = BuildInfo.BuildNumber;
            anEvent ["device_id"] = Device.Instance.Identity ();
            anEvent ["fresh_install"] = FreshInstall;
            FreshInstall = false;

            return AwsSendOneEvent (DeviceInfoTable, anEvent);
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
            anEvent ["thread_id"] = tEvent.ThreadId;
            return anEvent;
        }

        private Document SupportEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["event_type"] = "SUPPORT";
            anEvent ["client"] = GetUserName ();
            anEvent ["support"] = tEvent.Support;
            return anEvent;
        }

        private Document CounterEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["event_type"] = "COUNTER";
            anEvent ["counter_name"] = tEvent.CounterName;
            anEvent ["count"] = tEvent.Count;
            anEvent ["counter_start"] = tEvent.CounterStart.Ticks;
            anEvent ["counter_end"] = tEvent.CounterEnd.Ticks;
            return anEvent;
        }

        private Document CaptureEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["event_type"] = "CAPTURE";
            anEvent ["capture_name"] = tEvent.CaptureName;
            anEvent ["count"] = tEvent.Count;
            anEvent ["min"] = tEvent.Min;
            anEvent ["max"] = tEvent.Max;
            anEvent ["sum"] = tEvent.Sum;
            anEvent ["sum2"] = tEvent.Sum2;
            return anEvent;
        }

        private Document UiEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            anEvent ["event_type"] = "UI";
            anEvent ["ui_type"] = tEvent.UiType;
            if (null == tEvent.UiObject) {
                anEvent ["ui_object"] = "(unknown)";
            } else {
                anEvent ["ui_object"] = tEvent.UiObject;
            }
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
                anEvent ["ui_string"] = tEvent.UiString;
                break;
            }
            return anEvent;
        }

        private Document WbxmlEvent (TelemetryEvent tEvent)
        {
            var anEvent = InitializeEvent (tEvent);
            switch (tEvent.Type) {
            case TelemetryEventType.WBXML_REQUEST:
                anEvent ["event_type"] = "WBXML_REQUEST";
                break;
            case TelemetryEventType.WBXML_RESPONSE:
                anEvent ["event_type"] = "WBXML_RESPONSE";
                break;
            default:
                var msg = String.Format ("unknown wbxml event type {0}", (int)tEvent.Type);
                throw new NcAssert.NachoDefaultCaseFailure (msg);
            }
            anEvent ["wbxml"] = tEvent.Wbxml;
            return anEvent;
        }
    }
}

