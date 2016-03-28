//  Copyright (C) 2014-2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;
using Newtonsoft.Json;
using NachoPlatform;

namespace NachoCore.Utils
{
    public partial class Telemetry
    {
        public static bool ENABLED = true;

        // AWS Redshift has a limit of 65,535 for varchar.
        private const int MAX_AWS_LEN = 65535;

        // Maximnum number of events to query per write to telemetry server
        private const int MAX_QUERY_ITEMS = 10;

        // Maximum number of seconds without any event to send before a message is generated
        // to confirm that telemetry is still running.
        private const double MAX_IDLE_PERIOD = 30.0;

        // The amount of pause (in milliseconds) between successive uploads when throttling is enabled.
        // Assuming typical upload time of 50 msec, 200 msec pause gives roughly 4 uploads per seconds.
        private const int THROTTLING_IDLE_PERIOD = 200;

        private static Telemetry _Instance;
        private static object lockObject = new object();

        public static Telemetry Instance {
            get {
                if (null == _Instance) {
                    NcTimeStamp.Add ("Create SharedInstance before lock");
                    lock (lockObject) {
                        NcTimeStamp.Add ("Create SharedInstance lock acquired");
                        if (null == _Instance) {
                            NcTimeStamp.Add ("Before new Telemetry()");
                            _Instance = new Telemetry ();
                            NcTimeStamp.Add ("After new Telemetry()");
                        }
                    }
                }
                return _Instance;
            }
        }

        public static bool Initialized { get; protected set; }

        public bool TelemetryPending ()
        {
            return Telemetry.TelemetryJsonFileTable.Instance.GetNextReadFile () != null;
        }

        public void FinalizeAll ()
        {
            Telemetry.TelemetryJsonFileTable.Instance.FinalizeAll ();
        }

        CancellationToken Token;

        private AutoResetEvent DbUpdated;

        private ITelemetryBE BackEnd;

        NcCounter[] Counters;
        NcCounter FailToSend;

        private NcRateLimter FailToSendLogLimiter;

        // Telemetry event upload can be throttled by this boolean. It is set by StartService() and
        // clear by class 4 late show event
        public bool Throttling;

        // In order to prevent the teledb file from getting too big when it fails to talk
        // to DynamoDB, we periodically check if there are too many rows in the tables and
        // optionally purge them
        protected static int PurgeCounter = 0;

        Telemetry ()
        {
            Telemetry.TelemetryJsonFileTable.Instance.Initialize ();
            BackEnd = null;
            NcTimeStamp.Add ("Before DbUpdated");
            DbUpdated = new AutoResetEvent (false);
            NcTimeStamp.Add ("Before Counters");
            Counters = new NcCounter[(int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE];
            Counters [0] = new NcCounter ("Telemetry", true);
            Counters [0].AutoReset = true;
            Counters [0].ReportPeriod = 0;
            Counters [0].PreReportCallback = PreReportAdjustment;
            NcTimeStamp.Add ("After Counters [0]");

            Type teleEvtType = typeof(TelemetryEventType);
            foreach (TelemetryEventType type in Enum.GetValues(teleEvtType)) {
                if ((TelemetryEventType.COUNTER == type) ||
                    (TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE == type) ||
                    (TelemetryEventType.UNKNOWN == type)) {
                    continue;
                }
                Counters [(int)type] = Counters [0].AddChild (Enum.GetName (teleEvtType, type));
            }
            NcTimeStamp.Add ("After Counters loop");
            // Counter must be the last counter created!
            Counters [(int)TelemetryEventType.COUNTER] = Counters [0].AddChild ("COUNTER");
            NcTimeStamp.Add ("After last counter");

            // Add other non-event type related counters
            FailToSend = Counters [0].AddChild ("FAIL_TO_SEND");
            NcTimeStamp.Add ("After FailToSend");

            // Allow one failure log per 64 sec or roughly 1 per min. 1/64
            // is chosen so that the arithematic is exact.
            FailToSendLogLimiter = new NcRateLimter (1.0 / 64.0, 64.0);
            FailToSendLogLimiter.Enabled = true;
            NcTimeStamp.Add ("Telementry() end");
            Initialized = true;
        }

        // This is kind of a hack. When Telemetry is reporting the counter values,
        // they are being updated. For example, root counter "Telemetry" is updated
        // 8 more times (for 8 event types) after it is reported. So, its count is
        // wrong. The fix is pre-adjust the increment that will happen during the
        // reporting. All counts are reset after reporting so the adjustment has
        // no longer effect at all.
        private static void PreReportAdjustment ()
        {
            Instance.Counters [(int)TelemetryEventType.COUNTER].Click ();
            Instance.Counters [0].Click ((int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE - 1);
        }

        private static void MayPurgeEvents ()
        {
            // Only check once every N events
            PurgeCounter = (PurgeCounter + 1) % 512;
            if (0 != PurgeCounter) {
                return;
            }
            // TODO - Add purging mechanism if the backlog exceeds some threshold
        }

        private static void RecordJsonEvent (TelemetryEventType eventType, TelemetryJsonEvent jsonEvent)
        {
            Instance.Counters [(int)eventType].Click ();
            NcTimeStamp.Add ("After SharedInstance.Counters.Click()");
            Telemetry.TelemetryJsonFileTable.Instance.Add (jsonEvent);
            NcTimeStamp.Add ("After TelemetryJsonFileTable.Instance.Add()");
            MayPurgeEvents ();
            NcTimeStamp.Add ("After MayPurgeEvents()");
            Telemetry.Instance.DbUpdated.Set ();
            NcTimeStamp.Add ("After DbUpdated.Set()");
        }

        public static void RecordLogEvent (int threadId, TelemetryEventType type, ulong subsystem, string fmt, params object[] list)
        {
            if (!ENABLED) {
                return;
            }

            NcAssert.True (TelemetryEvent.IsLogEvent (type));
            NcTimeStamp.Add ("Before create jsonEvent");
            var jsonEvent = new TelemetryLogEvent (type) {
                thread_id = threadId,
                module = Log.ModuleString (subsystem),
                message = String.Format (fmt, list)
            };
            if (MAX_AWS_LEN < jsonEvent.message.Length) {
                jsonEvent.message = jsonEvent.message.Substring (0, MAX_AWS_LEN - 4);
                jsonEvent.message += " ...";
            }
            NcTimeStamp.Add ("After create jsonEvent");
            RecordJsonEvent (type, jsonEvent);
            NcTimeStamp.Add ("After RecordJsonEvent");
        }

        protected static void RecordProtocolEvent (TelemetryEventType type, byte[] payload)
        {
            if (!ENABLED) {
                return;
            }

            // TODO - Add check for the limit of wbxml. But we need to limit the base64 encode no the binary bytes.
            var jsonEvent = new TelemetryProtocolEvent (type) {
                payload = payload,
            };
            RecordJsonEvent (type, jsonEvent);
        }

        public static void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
            var type = isRequest ? TelemetryEventType.WBXML_REQUEST : TelemetryEventType.WBXML_RESPONSE;
            RecordProtocolEvent (type, wbxml);
        }

        public static void RecordImapEvent (bool isRequest, byte[] payload)
        {
            var type = isRequest ? TelemetryEventType.IMAP_REQUEST : TelemetryEventType.IMAP_RESPONSE;
            RecordProtocolEvent (type, payload);
        }

        public static void RecordCounter (string name, Int64 count, DateTime start, DateTime end)
        {
            if (!ENABLED) {
                return;
            }

            var jsonEvent = new TelemetryCounterEvent () {
                counter_name = name,
                count = count,
                counter_start = TelemetryJsonEvent.AwsDateTime (start),
                counter_end = TelemetryJsonEvent.AwsDateTime (end)
            };
            RecordJsonEvent (TelemetryEventType.COUNTER, jsonEvent);
        }

        private static TelemetryUiEvent GetTelemetryUiEvent (string uiType, string uiObject)
        {
            TelemetryUiEvent uiEvent = new TelemetryUiEvent ();
            if (null == uiType) {
                uiEvent.ui_type = "(unknown)";
            } else {
                uiEvent.ui_type = uiType;
            }
            uiEvent.ui_object = uiObject;
            if (String.IsNullOrEmpty (uiEvent.ui_object)) {
                Log.Warn (Log.LOG_UI, "UI {0} object without accessibility label", uiType);
            }
            return uiEvent;
        }

        private static void RecordUi (string uiType, string uiObject)
        {
            if (!ENABLED) {
                return;
            }

            var jsonEvent = GetTelemetryUiEvent (uiType, uiObject);
            RecordJsonEvent (TelemetryEventType.UI, jsonEvent);
        }

        private static void RecordUiWithLong (string uiType, string uiObject, long value)
        {
            if (!ENABLED) {
                return;
            }

            var jsonEvent = GetTelemetryUiEvent (uiType, uiObject);
            jsonEvent.ui_long = value;
            RecordJsonEvent (TelemetryEventType.UI, jsonEvent);
        }

        private static void RecordUiWithString (string uiType, string uiObject, string value)
        {
            if (!ENABLED) {
                return;
            }

            var jsonEvent = GetTelemetryUiEvent (uiType, uiObject);
            jsonEvent.ui_string = value;
            RecordJsonEvent (TelemetryEventType.UI, jsonEvent);
        }

        public static void RecordUiBarButtonItem (string uiObject)
        {
            RecordUi (TelemetryEvent.UIBARBUTTONITEM, uiObject);
        }

        public static void RecordUiButton (string uiObject)
        {
            RecordUi (TelemetryEvent.UIBUTTON, uiObject);
        }

        public static void RecordUiSegmentedControl (string uiObject, long index)
        {
            RecordUiWithLong (TelemetryEvent.UISEGMENTEDCONTROL, uiObject, index);
        }

        public static void RecordUiSwitch (string uiObject, string onOff)
        {
            RecordUiWithString (TelemetryEvent.UISWITCH, uiObject, onOff);
        }

        public static void RecordUiDatePicker (string uiObject, string date)
        {
            RecordUiWithString (TelemetryEvent.UIDATEPICKER, uiObject, date);
        }

        public static void RecordUiTextField (string uiObject)
        {
            RecordUi (TelemetryEvent.UITEXTFIELD, uiObject);
        }

        public static void RecordUiPageControl (string uiObject, long page)
        {
            RecordUiWithLong (TelemetryEvent.UIPAGECONTROL, uiObject, page);
        }

        public static void RecordUiViewController (string uiObject, string state)
        {
            RecordUiWithString (TelemetryEvent.UIVIEWCONTROLER, uiObject, state);
        }

        public static void RecordUiAlertView (string uiObject, string action)
        {
            RecordUiWithString (TelemetryEvent.UIALERTVIEW, uiObject, action);
        }

        public static void RecordUiActionSheet (string uiObject, long index)
        {
            RecordUiWithLong (TelemetryEvent.UIACTIONSHEET, uiObject, index);
        }

        public static void RecordUiTapGestureRecognizer (string uiObject, string touches)
        {
            RecordUiWithString (TelemetryEvent.UITAPGESTURERECOGNIZER, uiObject, touches);
        }

        public static void RecordUiTableView (string uiObject, string operation)
        {
            RecordUiWithString (TelemetryEvent.UITABLEVIEW, uiObject, operation);
        }

        public static void RecordSupport (Dictionary<string, string> info, Action callback = null)
        {
            if (!ENABLED) {
                return;
            }

            var json = JsonConvert.SerializeObject (info);
            var jsonEvent = new TelemetrySupportEvent () {
                support = json,
            };
            RecordJsonEvent (TelemetryEventType.SUPPORT, jsonEvent);

            if (null != callback) {
                var requestEvent = new TelemetrySupportRequestEvent () {
                    support = json,
                    callback = callback
                };
                RecordJsonEvent (TelemetryEventType.SUPPORT_REQUEST, requestEvent);
            }
        }

        public static void RecordAccountEmailAddress (McAccount account)
        {               
            string emailAddress = account.EmailAddr;
            if (String.IsNullOrEmpty (emailAddress)) {
                return;
            }

            // Split the string using "@"
            int index = emailAddress.IndexOf ("@");
            if (0 > index) {
                return; // malformed email address - missing "@"
            }
            if (emailAddress.LastIndexOf ("@") != index) {
                return; // malformed email address - more than 1 "@"
            }
            Dictionary<string, string> dict = new Dictionary<string, string> ();
            dict.Add ("sha256_email_address", HashHelper.Sha256 (emailAddress.Substring (0, index)) + emailAddress.Substring (index));
            RecordSupport (dict);
        }

        public static void RecordIntSamples (string samplesName, List<int> samplesValues)
        {
            if (!ENABLED) {
                return;
            }
            foreach (var value in samplesValues) {
                var jsonEvent = new TelemetrySamplesEvent () {
                    sample_name = samplesName,
                    sample_int = value,
                };
                RecordJsonEvent (TelemetryEventType.SAMPLES, jsonEvent);
            }
        }

        public static void RecordFloatSamples (string samplesName, List<double> samplesValues)
        {
            if (!ENABLED) {
                return;
            }
            foreach (var value in samplesValues) {
                var jsonEvent = new TelemetrySamplesEvent () {
                    sample_name = samplesName,
                    sample_float = value,
                };
                RecordJsonEvent (TelemetryEventType.SAMPLES, jsonEvent);
            }
        }

        public static void RecordStringSamples (string samplesName, List<string> samplesValues)
        {
            if (!ENABLED) {
                return;
            }
            foreach (var value in samplesValues) {
                var jsonEvent = new TelemetrySamplesEvent () {
                    sample_name = samplesName,
                    sample_string = value,
                };
                RecordJsonEvent (TelemetryEventType.SAMPLES, jsonEvent);
            }
        }

        public static void RecordStatistics2 (string name, int count, int min, int max, long sum, long sum2)
        {
            if (!ENABLED) {
                return;
            }
            var jsonEvent = new TelemetryStatistics2Event () {
                stat2_name = name,
                count = count,
                min = min,
                max = max,
                sum = sum,
                sum2 = sum2,
            };
            RecordJsonEvent (TelemetryEventType.STATISTICS2, jsonEvent);
        }

        public static void RecordIntTimeSeries (string name, DateTime time, int value)
        {
            if (!ENABLED) {
                return;
            }
            var jsonEvent = new TelemetryTimeSeriesEvent () {
                time_series_name = name,
                time_series_timestamp = TelemetryJsonEvent.AwsDateTime (time),
                time_series_int = value,
            };
            RecordJsonEvent (TelemetryEventType.TIME_SERIES, jsonEvent);
        }

        public static void RecordFloatTimeSeries (string name, DateTime time, double value)
        {
            if (!ENABLED) {
                return;
            }
            var jsonEvent = new TelemetryTimeSeriesEvent () {
                time_series_name = name,
                time_series_timestamp = TelemetryJsonEvent.AwsDateTime (time),
                time_series_float = value,
            };
            RecordJsonEvent (TelemetryEventType.TIME_SERIES, jsonEvent);
        }

        public static void RecordStringTimeSeries (string name, DateTime time, string value)
        {
            if (!ENABLED) {
                return;
            }
            var jsonEvent = new TelemetryTimeSeriesEvent () {
                time_series_name = name,
                time_series_timestamp = TelemetryJsonEvent.AwsDateTime (time),
                time_series_string = value,
            };
            RecordJsonEvent (TelemetryEventType.TIME_SERIES, jsonEvent);
        }

        public static void StartService ()
        {
            Instance.Throttling = true;
            Instance.Start ();
        }

        public void Start ()
        {
            if (!ENABLED) {
                return;
            }
            if (Token.GetHashCode () != NcTask.Cts.Token.GetHashCode ()) {
                NcTask.Run (() => {
                    Token = NcTask.Cts.Token;
                    Process ();
                }, "Telemetry", false, true, TaskCreationOptions.LongRunning);
            }
        }

        /// Send a SHA1 hash of the email address of all McAccounts (that have an email addresss)
        private void SendSha1AccountEmailAddresses ()
        {
            foreach (var account in McAccount.GetAllAccounts()) {
                if (McAccount.AccountTypeEnum.Device == account.AccountType) {
                    continue;
                }
                RecordAccountEmailAddress (account);
            }
        }

        private void WaitOrCancel (int millisecond)
        {
            if (NcTask.Cts.Token.WaitHandle.WaitOne (millisecond)) {
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
            }
        }

        private void Process ()
        {
            try {
                bool ranOnce = false;

                BackEnd = new TelemetryBEAWS ();
                BackEnd.Initialize ();
                if (Token.IsCancellationRequested) {
                    // If cancellation occurred and this telemetry didn't quit in time.
                    // This happens because some AWS initialization routines are synchronous
                    // and do not accept a cancellation token.
                    // Better late than never. Quit now.
                    Log.Warn (Log.LOG_LIFECYCLE, "Delay cancellation of telemetry task");
                    return;
                }
                Counters [0].ReportPeriod = 5 * 60; // report once every 5 min

                // Capture the transaction time to telemetry server
                const string CAPTURE_NAME = "Telemetry.SendEvent";
                NcCapture.AddKind (CAPTURE_NAME);
                NcCapture transactionTime = NcCapture.Create (CAPTURE_NAME);

                Log.Info (Log.LOG_LIFECYCLE, "Telemetry starts running");

                int eventDeleted = 0;
                SendSha1AccountEmailAddresses ();
                DateTime heartBeat = DateTime.Now;
                while (true) {
                    // If we are in quick sync, just wait for either:
                    //   1. Cancellation
                    //   2. Transition to foreground
                    while (NcApplication.ExecutionContextEnum.QuickSync ==
                           NcApplication.Instance.ExecutionContext) {
                        WaitOrCancel (500);
                    }

                    if (!ranOnce) {
                        // Record how much back log we have in telemetry db
                        Dictionary<string, string> dict = new Dictionary<string, string> ();
                        var numEvents = McTelemetryEvent.QueryCount ();
                        dict.Add ("num_events", numEvents.ToString ());
                        if (0 < numEvents) {
                            // Get the oldest event and report its timestamp
                            var oldestDbEvent = McTelemetryEvent.QueryMultiple (1) [0];
                            var oldestTeleEvent = oldestDbEvent.GetTelemetryEvent ();
                            dict.Add ("oldest_event", oldestTeleEvent.Timestamp.ToString ("yyyy-MM-ddTHH:mm:ssK"));
                            RecordSupport (dict);
                        }
                        ranOnce = true;
                    }

                    // TODO - We need to be smart about when we run. 
                    // For example, if we don't have WiFi, it may not be a good
                    // idea to upload a lot of data. The exact algorithm is TBD.
                    // But for now, let's not run when we're scrolling.
                    if ((DateTime.Now - heartBeat).TotalSeconds > 30) {
                        heartBeat = DateTime.Now;
                        Console.WriteLine ("Telemetry heartbeat {0}", heartBeat);
                    }

                    bool succeed = false;
                    // Old teledb-based telemetry
                    NcAssert.True (NcApplication.Instance.UiThreadId != Thread.CurrentThread.ManagedThreadId);
                    List<TelemetryEvent> tEvents = null;
                    List<McTelemetryEvent> dbEvents = null;
                    // Always check for support event first
                    dbEvents = McTelemetrySupportEvent.QueryOne ();
                    if (0 == dbEvents.Count) {
                        // If doesn't have any, check for other events
                        dbEvents = McTelemetryEvent.QueryMultiple (MAX_QUERY_ITEMS);
                    }
                    if (0 == dbEvents.Count) {
                        var readFile = Telemetry.TelemetryJsonFileTable.Instance.GetNextReadFile ();
                        if (null != readFile) {
                            // New log file-based telemetry
                            succeed = BackEnd.UploadEvents (readFile);
                            if (succeed) {
                                Action supportCallback;
                                Telemetry.TelemetryJsonFileTable.Instance.Remove (readFile, out supportCallback);
                                if (null != supportCallback) {
                                    InvokeOnUIThread.Instance.Invoke (supportCallback);
                                }
                            } else {
                                FailToSend.Click ();
                                if (FailToSendLogLimiter.TakeToken ()) {
                                    Log.Warn (Log.LOG_UTILS, "fail to reach telemetry server (count={0})", FailToSend.Count);
                                }
                            }
                        } else {
                            // No pending event. Wait for one.
                            DateTime then = DateTime.Now;
                            while (!DbUpdated.WaitOne (NcTask.MaxCancellationTestInterval)) {
                                NcTask.Cts.Token.ThrowIfCancellationRequested ();
                                if (MAX_IDLE_PERIOD < (DateTime.Now - then).TotalSeconds) {
                                    Log.Info (Log.LOG_UTILS, "Telemetry has no event for more than {0} seconds",
                                        MAX_IDLE_PERIOD);
                                    then = DateTime.Now;
                                }
                            }
                        }
                        continue;
                    } else {
                        NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    }
                    tEvents = new List<TelemetryEvent> ();
                    foreach (var dbEvent in dbEvents) {
                        tEvents.Add (dbEvent.GetTelemetryEvent ());
                    }

                    // Send it to the telemetry server
                    transactionTime.Start ();
                    succeed = BackEnd.SendEvents (tEvents);
                    transactionTime.Stop ();
                    transactionTime.Reset ();

                    if (succeed) {
                        // If it is a support, make the callback.
                        if ((1 == tEvents.Count) && (tEvents [0].IsSupportRequestEvent ()) && (null != tEvents [0].Callback)) {
                            InvokeOnUIThread.Instance.Invoke (tEvents [0].Callback);
                        }

                        // Delete the ones that are sent
                        foreach (var dbEvent in dbEvents) {
                            var rowsDeleted = dbEvent.Delete ();
                            if (1 != rowsDeleted) {
                                Log.Error (Log.LOG_UTILS, "Telemetry fails to delete event. (rowsDeleted={0}, id={1})",
                                    rowsDeleted, dbEvent.Id);
                                NcTask.Dump ();
                                if (0 == rowsDeleted) {
                                    Log.Error (Log.LOG_UTILS, "Duplicate telemetry task exits.");
                                    return;
                                }
                            }
                            NcAssert.True (1 == rowsDeleted);
                            eventDeleted = (eventDeleted + 1) & 0xfff;
                            if (0 == eventDeleted) {
                                // 4K events deleted. Try to vacuum
                                NcModel.MayIncrementallyVacuum (NcModel.Instance.TeleDb, 256);
                            }
                        }

                        if ((MAX_QUERY_ITEMS > dbEvents.Count) && !(dbEvents [0] is McTelemetrySupportEvent)) {
                            // We have completely caught up. Don't want to continue
                            // to the next message immediately because we want to
                            // send multiple messages at a time. This leads to a more
                            // efficient utilization of write capacity.
                            WaitOrCancel (5000);
                        } else {
                            // We still have more events to upload. Check if we are throttling still.
                            if (Throttling) {
                                WaitOrCancel (THROTTLING_IDLE_PERIOD);
                            }
                        }
                    } else {
                        FailToSend.Click ();
                        if (FailToSendLogLimiter.TakeToken ()) {
                            Log.Warn (Log.LOG_UTILS, "fail to reach telemetry server (count={0})", FailToSend.Count);
                        }
                    }
                }
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_UTILS, "Telemetry task exits");
            }
        }
    }

    public interface ITelemetryBE
    {
        void Initialize ();

        string GetUserName ();

        bool SendEvents (List<TelemetryEvent> tEvents);

        bool UploadEvents (string jsonFilePath);
    }
}
