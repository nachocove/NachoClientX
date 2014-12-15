﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
//#define AWS_DEBUG
using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
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

        private static string ClientId;

        public TelemetryBEAWS ()
        {
            InitializeTables ();
        }

        private void InitializeTables ()
        {
            var config = new AmazonDynamoDBConfig ();
            config.UseHttp = false;
            config.AuthenticationRegion = "us-west-2";
            config.ServiceURL = "https://dynamodb.us-west-2.amazonaws.com";

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
            }

            Retry (() => {
                Client = new AmazonDynamoDBClient (credentials, config);

                DeviceInfoTable = Table.LoadTable (Client, TableName ("device_info"));
                LogTable = Table.LoadTable (Client, TableName ("log"));
                SupportTable = Table.LoadTable (Client, TableName ("support"));
                CounterTable = Table.LoadTable (Client, TableName ("counter"));
                CaptureTable = Table.LoadTable (Client, TableName ("capture"));
                UiTable = Table.LoadTable (Client, TableName ("ui"));
                WbxmlTable = Table.LoadTable (Client, TableName ("wbxml"));
            });
        }

        private void Retry (Action action)
        {
            bool isDone = false;
            while (!isDone) {
                try {
                    action ();
                    isDone = true;
                } catch (TaskCanceledException) {
                    if (NcTask.Cts.Token.IsCancellationRequested) {
                        throw;
                    }
                    // Otherwise, most likely HTTP client timeout
                    NcTask.CancelableSleep (5000);
                } catch (AmazonServiceException e) {
                    Console.WriteLine ("AWS service exception {0}", e);
                    NcTask.CancelableSleep (5000);
                } catch (AggregateException e) {
                    // Some code path wraps the exception with an AggregateException. Peel the onion
                    if (e.InnerException is TaskCanceledException) {
                        if (NcTask.Cts.Token.IsCancellationRequested) {
                            throw;
                        }
                        NcTask.CancelableSleep (5000);
                    } else if (e.InnerException is AmazonServiceException) {
                        Console.WriteLine ("AWS service inner exception {0}", e.InnerException);
                        NcTask.CancelableSleep (5000);
                    } else {
                        throw;
                    }
                }
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }

        public void ReinitializeTables ()
        {
            Client.Dispose ();
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
            anEvent ["id"] = Guid.NewGuid ().ToString ().Replace ("-", "");
            anEvent ["client"] = GetUserName ();
            anEvent ["timestamp"] = tEvent.Timestamp.Ticks;
            return anEvent;
        }

        private bool AwsSendOneEvent (Table eventTable, Document eventItem)
        {
            try {
                eventItem ["uploaded_at"] = DateTime.UtcNow.Ticks;
                var task = eventTable.PutItemAsync (eventItem);
                task.Wait (NcTask.Cts.Token);
            } catch (TaskCanceledException e) {
                if (NcTask.Cts.Token.IsCancellationRequested) {
                    throw;
                }
                // Otherwise, most likely HTTP client timeout
                Console.WriteLine ("Task canceled exception {0}", e);
                return false;
            } catch (OperationCanceledException) {
                // Since we are catching Exception below, we must catch and re-throw
                // or this exception will be swallowed and telemetry task will not exit.
                throw;
            } catch (AmazonDynamoDBException e) {
                Console.WriteLine ("AWS DynamoDB exception {0}", e);
                ReinitializeTables ();
                return false;
            } catch (AmazonServiceException e) {
                Console.WriteLine ("AWS exception {0}", e);
                return false;
            } catch (Exception e) {
                // FIXME - An exception is thrown but the exception is null.
                // This workaround simply catches everything and re-initializes
                // the connection and tables.
                Console.WriteLine ("Some exception {0}", e);
                ReinitializeTables ();
                return false;
            }
            return true;
        }

        private bool AwsSendBatchEvents (MultiTableDocumentBatchWrite multiBatchWrite)
        {
            try {
                var task = multiBatchWrite.ExecuteAsync (NcTask.Cts.Token);
                task.Wait (NcTask.Cts.Token);
            } catch (TaskCanceledException e) {
                if (NcTask.Cts.Token.IsCancellationRequested) {
                    throw;
                }
                // Otherwise, most likely HTTP client timeout
                Console.WriteLine ("Task canceled exception {0}", e);
                return false;
            } catch (OperationCanceledException) {
                // Since we are catching Exception below, we must catch and re-throw
                // or this exception will be swallowed and telemetry task will not exit.
                throw;
            } catch (AmazonDynamoDBException e) {
                Console.WriteLine ("AWS DynamoDB exception {0}", e);
                ReinitializeTables ();
                return false;
            } catch (AmazonServiceException e) {
                Console.WriteLine ("AWS exception {0}", e);
                return false;
            } catch (Exception e) {
                // FIXME - An exception is thrown but the exception is null.
                // This workaround simply catches everything and re-initializes
                // the connection and tables.
                Console.WriteLine ("Some exception {0}", e);
                ReinitializeTables ();
                return false;
            }
            return true;
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

