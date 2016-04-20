//  Copyright (C) 2014-2015 Nacho Cove, Inc. All rights reserved.
//
//#define AWS_DEBUG
using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Threading;
using System.Reflection;

using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.DocumentModel;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Runtime;
using Amazon.SecurityToken;
using Amazon.Util;
using Amazon.CognitoIdentity;
using Amazon;
using Newtonsoft.Json;

using NachoPlatform;
using NachoClient.Build;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public class TelemetryBEAWS : ITelemetryBE
    {
        private static bool Initialized = false;

        private static AmazonDynamoDBClient Client;
        private static AmazonS3Client S3Client;
        private static Table LogTable;
        private static Table SupportTable;
        private static Table CounterTable;
        private static Table CaptureTable;
        private static Table UiTable;
        private static Table WbxmlTable;

        private static int t2EventCount = -1;
        private static int t2SupportEventCount = -1;

        private static string ClientId {
            get {
                return Device.Instance.Identity ();
            }
        }

        public static string _HashUserId;

        private static string HashUserId {
            get {
                if (null == _HashUserId) {
                    if (null == NcApplication.Instance.UserId) {
                        return null;
                    }
                    _HashUserId = HashHelper.Sha256 (NcApplication.Instance.UserId).Substring (0, 8);
                }
                return _HashUserId;
            }
        }

        private bool FreshInstall;

        public TelemetryBEAWS ()
        {
        }

        public void Initialize ()
        {
            // Clean up all leftover gzipped JSON files
            foreach (var gzFilePath in Directory.EnumerateFiles (NcApplication.GetDataDirPath ())) {
                if (gzFilePath.EndsWith (".gz")) {
                    SafeFileDelete (gzFilePath);
                }
            }

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

            var credentials = new TelemetryAWSCredentials (
                                  BuildInfo.AwsAccountId,
                                  BuildInfo.AwsIdentityPoolId,
                                  BuildInfo.AwsUnauthRoleArn,
                                  BuildInfo.AwsAuthRoleArn,
                                  RegionEndpoint.USEast1
                              );

            // We get a different Cognito id each time it runs because unauthenticated
            // identities (that we use) are anonymous. But doing so would mean it is
            // really hard to track a user's activity.
            if (null != NcApplication.Instance.UserId) {
                FreshInstall = false;
            } else {
                // Save the current Cognito id as client id
                Retry (() => {
                    NcApplication.Instance.UserId = credentials.GetIdentityId ();
                });
                FreshInstall = true;
            }
                
            Retry (() => {
                if (-1 == t2EventCount) {
                    t2EventCount = McTelemetryEvent.QueryCount ();
                }
                if (-1 == t2SupportEventCount) {
                    t2SupportEventCount = McTelemetrySupportEvent.QueryCount ();
                }
                if ((0 == t2EventCount) && (0 == t2SupportEventCount)) {
                    // Do not iniitalize DynamoDB client and tables if teledb is empty. This allows
                    // us to remove DynamoDB tables in AWS for dev and alpha projects.
                    return;
                }
                Client = new AmazonDynamoDBClient (credentials, config);

                LogTable = Table.LoadTableAsync (Client, TableName ("log"), NcTask.Cts.Token);
                SupportTable = Table.LoadTableAsync (Client, TableName ("support"), NcTask.Cts.Token);
                CounterTable = Table.LoadTableAsync (Client, TableName ("counter"), NcTask.Cts.Token);
                CaptureTable = Table.LoadTableAsync (Client, TableName ("capture"), NcTask.Cts.Token);
                UiTable = Table.LoadTableAsync (Client, TableName ("ui"), NcTask.Cts.Token);
                WbxmlTable = Table.LoadTableAsync (Client, TableName ("wbxml"), NcTask.Cts.Token);
            });

            Retry (() => {
                S3Client = new AmazonS3Client (credentials, RegionEndpoint.USWest2);
            });
        }

        private void DisposeClient ()
        {
            if (null != Client) {
                Client.Dispose ();
                Client = null;
            }
        }

        private void DisposeS3Client ()
        {
            if (null != S3Client) {
                S3Client.Dispose ();
                S3Client = null;
            }
        }

        private void Retry (Action action)
        {
            bool isDone = false;
            while (!isDone) {
                try {
                    action ();
                    isDone = true;
                } catch (Exception e) {
                    if (!HandleAWSException (e, "AWS init", false)) {
                        if (NcTask.Cts.Token.IsCancellationRequested) {
                            DisposeClient ();
                            DisposeS3Client ();
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
            DisposeClient ();
            InitializeTables ();
        }

        public string GetUserName ()
        {
            return ClientId;
        }

        public bool SendEvents (List<TelemetryEvent> tEvents)
        {
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

                if (null != eventItem) {
                    NcAssert.True (null != eventTable);
                    // To DynamoDB
                    // Get the table batch write. Create one if it doesn't exist
                    DocumentBatchWrite batchWrite;
                    if (!writeDict.TryGetValue (eventTable, out batchWrite)) {
                        batchWrite = new DocumentBatchWrite (eventTable);
                        writeDict.Add (eventTable, batchWrite);
                    }
                    eventItem ["uploaded_at"] = DateTime.UtcNow.Ticks;
                    batchWrite.AddDocumentToPut (eventItem);
                }
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
            anEvent ["client"] = ClientId;
            anEvent ["timestamp"] = tEvent.Timestamp.Ticks;
            return anEvent;
        }

        /// <summary>
        /// Handles the AWS exception.
        /// </summary>
        /// <returns><c>true</c>, if AWS exception was handled, <c>false</c> otherwise.
        /// In that case, the caller must re-throw.</returns>
        /// <param name="e">E.</param>
        private bool HandleAWSException (Exception e, string description, bool doReinitialize = true)
        {
            if (null != e) {
                if (e is AggregateException) {
                    return HandleAWSException (e.InnerException, description);
                }
                if (e is ProvisionedThroughputExceededException) {
                    return true;
                }
                if (e is TaskCanceledException) {
                    if (NcTask.Cts.Token.IsCancellationRequested) {
                        return false;
                    }
                    // Otherwise, most likely HTTP client timeout
                    Console.WriteLine ("Task canceled exception caught in {1}\n{0}", e, description);
                    return true;
                }
                if (e is OperationCanceledException) {
                    // Since we are catching Exception below, we must catch and re-throw
                    // or this exception will be swallowed and telemetry task will not exit.
                    return false;
                }
                if (e is AmazonDynamoDBException) {
                    Console.WriteLine ("AWS DynamoDB exception caught in AWS send event\n{0}", e.Message);
                    ReinitializeTables ();
                    return true;
                }
                if (e is AmazonServiceException) {
                    Console.WriteLine ("AWS exception caught in AWS send event\n{0}", e.Message);
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

        private bool AwsSendEvent (Action action, string description, Action cleanup = null)
        {
            try {
                action ();
            } catch (Exception e) {
                if (!HandleAWSException (e, description)) {
                    if (null != cleanup) {
                        cleanup ();
                    }
                    if (NcTask.Cts.Token.IsCancellationRequested) {
                        DisposeClient ();
                        DisposeS3Client ();
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
            }, "AWS send one event");
        }

        private bool AwsSendBatchEvents (MultiTableDocumentBatchWrite multiBatchWrite)
        {
            return AwsSendEvent (() => {
                var task = multiBatchWrite.ExecuteAsync (NcTask.Cts.Token);
                task.Wait (NcTask.Cts.Token);
            }, "AWS send batch events");
        }

        protected void SafeFileDelete (string path)
        {
            try {
                File.Delete (path);
            } catch (IOException) {
            }
        }

        protected string GetS3Path (string filePath, out string s3Bucket)
        {
            var fileName = Path.GetFileName (filePath);
            var startTimeStamp = fileName.Substring (0, 17);
            var jsonType = fileName.Substring (36);
            var date = startTimeStamp.Substring (0, 8);

            string s3Path;
            if (jsonType == TelemetrySupportRequestEvent.SUPPORT_REQUEST.ToLowerInvariant ()) {
                s3Path = NcApplication.Instance.UserId + '-' + NcApplication.Instance.ClientId + '-' + startTimeStamp + ".gz";
                s3Bucket = BuildInfo.SupportS3Bucket;
            } else {
                var s3FileName = jsonType + '-' + startTimeStamp + ".gz";
                s3Path = Path.Combine (
                    date,
                    HashUserId,
                    NcApplication.Instance.UserId,
                    NcApplication.Instance.ClientId,
                    "NachoMail",
                    s3FileName);
                var eventClass = s3FileName.Substring (0, s3FileName.LastIndexOf ('-')).Replace ('_', '-');
                s3Bucket = BuildInfo.S3Bucket + eventClass;
            }
            return s3Path;
        }

        protected bool UploadFileToS3 (string filePath, string s3Key, string s3Bucket)
        {
            NcAssert.True (filePath.EndsWith (".gz"));
            var uploadRequest = new PutObjectRequest () {
                BucketName = s3Bucket,
                Key = s3Key,
                FilePath = filePath,
            };
            var succeeded = AwsSendEvent (() => {
                var task = S3Client.PutObjectAsync (uploadRequest, NcTask.Cts.Token);
                task.Wait (NcTask.Cts.Token);
            }, "AWS upload events", () => {
                SafeFileDelete (filePath);
            });

            SafeFileDelete (filePath);

            return succeeded;
        }

        public bool UploadEvents (string jsonFilePath)
        {
            if (!Initialized) {
                if (!SendDeviceInfo ()) {
                    return false;
                }
                Initialized = true;
            }

            var gzJsonFilePath = jsonFilePath + ".gz";
            if (File.Exists (gzJsonFilePath)) {
                File.Delete (gzJsonFilePath); // last upload could be aborted.
            }
            using (var jsonStream = File.Open (jsonFilePath, FileMode.Open, FileAccess.Read))
            using (var gzJsonStream = File.Open (gzJsonFilePath, FileMode.CreateNew, FileAccess.Write))
            using (var gzipStream = new GZipStream (gzJsonStream, CompressionMode.Compress)) {
                jsonStream.CopyTo (gzipStream);
            }

            // Extract timestamps from the file path
            string s3Bucket;
            var s3Path = GetS3Path (jsonFilePath, out s3Bucket);
            return UploadFileToS3 (gzJsonFilePath, s3Path, s3Bucket);
        }

        private bool SendDeviceInfo ()
        {
            if (null == NcApplication.Instance.UserId) {
                // During 1st launch after a fresh install, there is a small window when we don't have a user id.
                return false;
            }
            // Create the JSON
            var jsonEvent = new TelemetryDeviceInfoEvent () {
                os_type = Device.Instance.OsType (),
                os_version = Device.Instance.OsVersion (),
                device_model = Device.Instance.Model (),
                build_version = BuildInfo.Version,
                build_number = BuildInfo.BuildNumber,
                device_id = Device.Instance.Identity (),
                fresh_install = FreshInstall,
                user_id = NcApplication.Instance.UserId,
            };
            var json = JsonConvert.SerializeObject (
                           jsonEvent, Newtonsoft.Json.Formatting.None,
                           new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            // Create the temporary JSON .gz file
            var timestamp = jsonEvent.Timestamp ();
            var readFilePath = Telemetry.TelemetryJsonFileTable.GetReadFilePath (
                                   Path.Combine (NcApplication.GetDataDirPath (), "device_info"),
                                   timestamp, timestamp);
            string s3Bucket;
            var s3Path = GetS3Path (readFilePath, out s3Bucket);
            var gzJsonFilePath = readFilePath + ".gz";
            using (var gzJsonStream = File.Open (gzJsonFilePath, FileMode.CreateNew, FileAccess.Write))
            using (var gzipStream = new GZipStream (gzJsonStream, CompressionMode.Compress))
            using (var streamWriter = new StreamWriter (gzipStream)) {
                streamWriter.Write (json);
            }

            return UploadFileToS3 (gzJsonFilePath, s3Path, s3Bucket);
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
            anEvent ["client"] = ClientId;
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

    class TelemetryAWSCredentials : CognitoAWSCredentials
    {
        public TelemetryAWSCredentials (string accountId, string identityPoolId,
                                        string unAuthRoleArn, string authRoleArn, RegionEndpoint region)
            : base (accountId, identityPoolId, unAuthRoleArn, authRoleArn, region)
        {
        }

        public override string GetCachedIdentityId ()
        {
            return NcApplication.Instance.UserId;
        }
    }
}

