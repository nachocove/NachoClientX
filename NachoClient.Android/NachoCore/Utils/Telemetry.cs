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
    public class Telemetry
    {
        public static bool ENABLED = true;

        // AWS Redshift has a limit of 65,535 for varchar.
        private const int MAX_AWS_LEN = 65535;

        // Maximum number of seconds without any event to send before a message is generated
        // to confirm that telemetry is still running.
        private const double MAX_IDLE_PERIOD = 30.0;

        private static Telemetry _Instance;
        private static object lockObject = new object ();

        public static Telemetry Instance {
            get {
                if (null == _Instance) {
                    lock (lockObject) {
                        if (null == _Instance) {
                            _Instance = new Telemetry ();
                        }
                    }
                }
                return _Instance;
            }
        }

        public static bool Initialized { get; protected set; }

        public void FinalizeAll ()
        {
            DBBackEnd.FinalizeAll ();
        }

        CancellationToken Token;

        private AutoResetEvent DbUpdated;

        ITelemetryBE BackEnd;
        ITelementryDB DBBackEnd;

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
            #if !TELEMETRY_BE_NOOP
            DBBackEnd = new TelemetryJsonFileTable ();
            #else
            DBBackEnd = new TelemetryJsonFileTable_NOOP ();
            #endif
            BackEnd = null;
            DbUpdated = new AutoResetEvent (false);
            Counters = new NcCounter[(int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE];
            Counters [0] = new NcCounter ("Telemetry", true);
            Counters [0].AutoReset = true;
            Counters [0].ReportPeriod = 0;
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
            Counters [(int)TelemetryEventType.COUNTER] = Counters [0].AddChild ("COUNTER");

            // Add other non-event type related counters
            FailToSend = Counters [0].AddChild ("FAIL_TO_SEND");

            // Allow one failure log per 64 sec or roughly 1 per min. 1/64
            // is chosen so that the arithematic is exact.
            FailToSendLogLimiter = new NcRateLimter (1.0 / 64.0, 64.0);
            FailToSendLogLimiter.Enabled = true;
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
            Instance.DBBackEnd.Add (jsonEvent);
            MayPurgeEvents ();
            Telemetry.Instance.DbUpdated.Set ();
        }

        public static void RecordLogEvent (int threadId, TelemetryEventType type, ulong subsystem, string fmt, params object[] list)
        {
            if (!ENABLED) {
                return;
            }

            NcAssert.True (TelemetryEvent.IsLogEvent (type));
            var jsonEvent = new TelemetryLogEvent (type) {
                thread_id = threadId,
                module = Log.ModuleString (subsystem),
                message = String.Format (fmt, list)
            };
            if (MAX_AWS_LEN < jsonEvent.message.Length) {
                jsonEvent.message = jsonEvent.message.Substring (0, MAX_AWS_LEN - 4);
                jsonEvent.message += " ...";
            }
            RecordJsonEvent (type, jsonEvent);
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
                #if !TELEMETRY_BE_NOOP
                BackEnd = new TelemetryBES3 ();
                #else
                BackEnd = new TelemetryBE_NOOP ();
                #endif

                if (Token.IsCancellationRequested) {
                    // If cancellation occurred and this telemetry didn't quit in time.
                    // This happens because some AWS initialization routines are synchronous
                    // and do not accept a cancellation token.
                    // Better late than never. Quit now.
                    Log.Warn (Log.LOG_LIFECYCLE, "Delay cancellation of telemetry task");
                    return;
                }
                Counters [0].ReportPeriod = 5 * 60; // report once every 5 min

                Log.Info (Log.LOG_LIFECYCLE, "Telemetry starts running");

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

                    // TODO - We need to be smart about when we run. 
                    // For example, if we don't have WiFi, it may not be a good
                    // idea to upload a lot of data. The exact algorithm is TBD.
                    // But for now, let's not run when we're scrolling.
                    if ((DateTime.Now - heartBeat).TotalSeconds > 30) {
                        heartBeat = DateTime.Now;
                        Console.WriteLine ("Telemetry heartbeat {0}", heartBeat);
                    }

                    bool succeed = false;
                    NcAssert.True (NcApplication.Instance.UiThreadId != Thread.CurrentThread.ManagedThreadId);

                    var readFile = DBBackEnd.GetNextReadFile ();
                    if (null != readFile) {
                        // New log file-based telemetry
                        succeed = BackEnd.UploadEvents (readFile);
                        if (succeed) {
                            Action supportCallback;
                            DBBackEnd.Remove (readFile, out supportCallback);
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
                }
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_UTILS, "Telemetry task exits");
            }
        }
    }

    public interface ITelemetryBE
    {
        string GetUserName ();

        bool UploadEvents (string jsonFilePath);

        bool SupportsSupportMessage ();
    }

    public interface ITelementryDB
    {
        string GetNextReadFile ();

        void FinalizeAll ();

        bool Add (TelemetryJsonEvent jsonEvent);

        void Remove (string fileName, out Action supportCallback);
    }
}
