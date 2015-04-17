//  Copyright (C) 2014-2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using NachoCore.Model;
using Newtonsoft.Json;
using System.Json;
using System.Security.Cryptography;
using System.Text;
using NachoPlatform;

namespace NachoCore.Utils
{
    public enum TelemetryEventType
    {
        UNKNOWN = 0,
        ERROR,
        WARN,
        INFO,
        DEBUG,
        WBXML_REQUEST,
        WBXML_RESPONSE,
        STATE_MACHINE,
        COUNTER,
        CAPTURE,
        UI,
        SUPPORT,
        MAX_TELEMETRY_EVENT_TYPE}

    ;

    [Serializable]
    public class TelemetryEvent : NcQueueElement
    {
        // iOS UI object monitoring strings
        public const string UIBARBUTTONITEM = "UIBarButtonItem";
        public const string UIBUTTON = "UIButton";
        public const string UISEGMENTEDCONTROL = "UISegmentedControl";
        public const string UISWITCH = "UISwitch";
        public const string UIDATEPICKER = "UIDatePicker";
        public const string UITEXTFIELD = "UITextField";
        public const string UIPAGECONTROL = "UIPageControl";
        public const string UIVIEWCONTROLER = "UIViewController";
        public const string UIALERTVIEW = "UIAlertView";
        public const string UIACTIONSHEET = "UIActionSheet";
        public const string UITAPGESTURERECOGNIZER = "UITapGestureRecognizer";
        public const string UITABLEVIEW = "UITableView";

        public const string UIVIEW_WILLAPPEAR = "WILL_APPEAR";
        public const string UIVIEW_DIDAPPEAR = "DID_APPEAR";
        public const string UIVIEW_WILLDISAPPEAR = "WILL_DISAPPEAR";
        public const string UIVIEW_DIDDISAPPEAR = "DID_DISAPPEAR";

        public Int64 dbId;

        public DateTime Timestamp { set; get; }

        // This is the event ID recorded in the server (not the id of the server);
        // as opposed to the local dbId.
        public string ServerId { set; get; }

        private TelemetryEventType _Type;

        public TelemetryEventType Type {
            get {
                return _Type;
            }
        }

        // Thread id of a log message
        private int _ThreadId;

        public int ThreadId {
            get {
                return _ThreadId;
            }
            set {
                NcAssert.True (IsLogEvent ());
                _ThreadId = value;
            }
        }

        // The format string of a log message.
        private string _Message;

        public string Message { 
            get {
                return _Message;
            }
            set {
                NcAssert.True (IsLogEvent ());
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
                NcAssert.True (IsWbxmlEvent ());
                _Wbxml = value;
            }
        }

        // Counter Name
        private string _CounterName;

        public string CounterName {
            get {
                return _CounterName;
            }
            set {
                NcAssert.True (IsCounterEvent ());
                _CounterName = value;
            }
        }

        // Counter Count
        private Int64 _Count;

        public Int64 Count {
            get {
                return _Count;
            }
            set {
                NcAssert.True (IsCounterEvent () || IsCaptureEvent ());
                _Count = value;
            }
        }

        // Counter start time
        private DateTime _CounterStart;

        public DateTime CounterStart {
            get {
                return _CounterStart;
            }
            set {
                NcAssert.True (IsCounterEvent ());
                _CounterStart = value;
            }
        }

        // Counter end time
        private DateTime _CounterEnd;

        public DateTime CounterEnd {
            get {
                return _CounterEnd;
            }
            set {
                NcAssert.True (IsCounterEvent ());
                _CounterEnd = value;
            }
        }


        // Capture Name
        private string _CaptureName;

        public string CaptureName {
            get {
                return _CaptureName;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _CaptureName = value;
            }
        }

        // Capture Min
        private uint _Min;

        public uint Min {
            get {
                return _Min;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Min = value;
            }
        }

        // Capture Max
        private uint _Max;

        public uint Max {
            get {
                return _Max;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Max = value;
            }
        }

        // Capture sum
        private ulong _Sum;

        public ulong Sum {
            get {
                return _Sum;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Sum = value;
            }
        }

        // Capture sum of squares
        private ulong _Sum2;

        public ulong Sum2 {
            get {
                return _Sum2;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Sum2 = value;
            }
        }

        // UI event fields
        // UI type is the type of objects - UIButton, UILabel and etc
        private string _UiType;

        public string UiType {
            get {
                return _UiType;
            }
            set {
                NcAssert.True (IsUiEvent ());
                _UiType = value;
            }
        }

        private string _UiObject;

        public string UiObject {
            get {
                return _UiObject;
            }
            set {
                NcAssert.True (IsUiEvent ());
                _UiObject = value;
            }
        }

        private string _UiString;

        public string UiString {
            get {
                return _UiString;
            }
            set {
                NcAssert.True (IsUiEvent ());
                _UiString = value;
            }
        }

        private long _UiLong;

        public long UiLong {
            get {
                return _UiLong;
            }
            set {
                NcAssert.True (IsUiEvent ());
                _UiLong = value;
            }
        }

        private string _Support;

        public string Support {
            get {
                return _Support;
            }
            set {
                NcAssert.True (IsSupportEvent ());
                _Support = value;
            }
        }

        private Action _Callback;

        public Action Callback {
            get {
                return _Callback;
            }
            set {
                NcAssert.True (IsSupportEvent ());
                _Callback = value;
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

        public static bool IsCounterEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.COUNTER == type);
        }

        public static bool IsCaptureEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.CAPTURE == type);
        }

        public static bool IsUiEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.UI == type);
        }

        public static bool IsSupportEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.SUPPORT == type);
        }

        public bool IsLogEvent ()
        {
            return IsLogEvent (Type);
        }

        public bool IsWbxmlEvent ()
        {
            return IsWbxmlEvent (Type);
        }

        public bool IsCounterEvent ()
        {
            return IsCounterEvent (Type);
        }

        public bool IsCaptureEvent ()
        {
            return IsCaptureEvent (Type);
        }

        public bool IsUiEvent ()
        {
            return IsUiEvent (Type);
        }

        public bool IsSupportEvent ()
        {
            return IsSupportEvent (Type);
        }

        public TelemetryEvent (TelemetryEventType type)
        {
            Timestamp = DateTime.UtcNow;
            ServerId = Guid.NewGuid ().ToString ().Replace ("-", "");
            _Type = type;
            _Message = null;
            _Wbxml = null;
            _CounterName = null;
            _Count = 0;
            _CounterStart = new DateTime ();
            _CounterEnd = new DateTime ();
            _CaptureName = null;
            _Min = 0;
            _Max = 0;
            _Sum = 0;
            _Sum2 = 0;
            _UiType = null;
            _UiObject = null;
            _UiString = null;
            _UiLong = 0;
            _Support = null;
        }

        public uint GetSize ()
        {
            return 0;
        }
    }

    public class Telemetry
    {
        public static bool ENABLED = true;
        // Parse has a maximum data size of 128K for PFObject. But the
        // exact definition of data size of an object with multiple
        // fields is not clear. So, we just limit the log messages and
        // redacted WBXML to 120 KB to leave some headroom for other fields.
        private const int MAX_PARSE_LEN = 120 * 1024;

        // Maximnum number of events to query per write to telemetry server
        private const int MAX_QUERY_ITEMS = 10;

        // Maximum number of seconds without any event to send before a message is generated
        // to confirm that telemetry is still running.
        private const double MAX_IDLE_PERIOD = 30.0;

        // The amount of pause (in milliseconds) between successive uploads when throttling is enabled.
        // Assuming typical upload time of 50 msec, 200 msec pause gives roughly 4 uploads per seconds.
        private const int THROTTLING_IDLE_PERIOD = 200;

        private static Telemetry _SharedInstance;

        public static Telemetry SharedInstance {
            get {
                if (null == _SharedInstance) {
                    _SharedInstance = new Telemetry ();
                }
                NcAssert.True (null != _SharedInstance);
                return _SharedInstance;
            }
        }

        public CancellationToken Token;

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

        public Telemetry ()
        {
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
        }

        // This is kind of a hack. When Telemetry is reporting the counter values,
        // they are being updated. For example, root counter "Telemetry" is updated
        // 8 more times (for 8 event types) after it is reported. So, its count is
        // wrong. The fix is pre-adjust the increment that will happen during the
        // reporting. All counts are reset after reporting so the adjustment has
        // no longer effect at all.
        private static void PreReportAdjustment ()
        {
            SharedInstance.Counters [(int)TelemetryEventType.COUNTER].Click (1);
            SharedInstance.Counters [0].Click ((int)TelemetryEventType.MAX_TELEMETRY_EVENT_TYPE - 1);
        }

        private static void MayPurgeEvents (int limit)
        {
            // Only check once every N events
            PurgeCounter = (PurgeCounter + 1) % 512;
            if (0 != PurgeCounter) {
                return;
            }
            McTelemetryEvent.Purge<McTelemetryEvent> (limit);
            McTelemetrySupportEvent.Purge<McTelemetrySupportEvent> (limit);
        }

        private static void RecordRawEvent (TelemetryEvent tEvent)
        {
            SharedInstance.Counters [(int)tEvent.Type].Click ();
            if (tEvent.IsSupportEvent ()) {
                McTelemetrySupportEvent dbEvent = new McTelemetrySupportEvent (tEvent);
                dbEvent.Insert ();
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
            }
            MayPurgeEvents (200000);
            Telemetry.SharedInstance.DbUpdated.Set ();
        }

        public static void RecordLogEvent (int threadId, TelemetryEventType type, string fmt, params object[] list)
        {
            if (!ENABLED) {
                return;
            }

            NcAssert.True (TelemetryEvent.IsLogEvent (type));

            TelemetryEvent tEvent = new TelemetryEvent (type);
            tEvent.Message = String.Format (fmt, list);

            if (MAX_PARSE_LEN < tEvent.Message.Length) {
                // Truncate the message
                tEvent.Message = tEvent.Message.Substring (0, MAX_PARSE_LEN - 4);
                tEvent.Message += " ...";
            }
            tEvent.ThreadId = threadId;

            RecordRawEvent (tEvent);
        }

        public static void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEventType type;
            if (isRequest) {
                type = TelemetryEventType.WBXML_REQUEST;
            } else {
                type = TelemetryEventType.WBXML_RESPONSE;
            }
            TelemetryEvent tEvent = new TelemetryEvent (type);

            if (MAX_PARSE_LEN < wbxml.Length) {
                Console.WriteLine ("Redacted WBXML too long (length={0})", wbxml.Length);
                StackTrace st = new StackTrace ();
                Console.WriteLine ("{0}", st.ToString ());
                // Can't truncate the WBXML and still have it remain valid.
                // TODO - Need to think of a better solution
            }

            tEvent.Wbxml = wbxml;

            RecordRawEvent (tEvent);
        }

        public static void RecordCounter (string name, Int64 count, DateTime start, DateTime end)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.COUNTER);

            tEvent.CounterName = name;
            tEvent.Count = count;
            tEvent.CounterStart = start;
            tEvent.CounterEnd = end;

            RecordRawEvent (tEvent);
        }

        public static void RecordCapture (string name, uint count, uint min, uint max, ulong sum, ulong sum2)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.CAPTURE);

            tEvent.CaptureName = name;
            tEvent.Count = count;
            tEvent.Min = min;
            tEvent.Max = max;
            tEvent.Sum = sum;
            tEvent.Sum2 = sum2;

            RecordRawEvent (tEvent);
        }

        private static TelemetryEvent GetTelemetryEvent (string uiType, string uiObject)
        {
            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.UI);
            if (null == uiType) {
                tEvent.UiType = "(unknown)";
            } else {
                tEvent.UiType = uiType;
            }
            tEvent.UiObject = uiObject;
            return tEvent;
        }

        private static void RecordUi (string uiType, string uiObject)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = GetTelemetryEvent (uiType, uiObject);
            RecordRawEvent (tEvent);
        }

        private static void RecordUiWithLong (string uiType, string uiObject, long value)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = GetTelemetryEvent (uiType, uiObject);
            tEvent.UiLong = value;

            RecordRawEvent (tEvent);
        }

        private static void RecordUiWithString (string uiType, string uiObject, string value)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = GetTelemetryEvent (uiType, uiObject);
            tEvent.UiString = value;

            RecordRawEvent (tEvent);
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

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.SUPPORT);
            tEvent.Support = JsonConvert.SerializeObject (info);
            tEvent.Callback = callback;
            RecordRawEvent (tEvent);
        }

        public static void RecordAccountEmailAddress (McAccount account)
        {
            string emailAddress = account.EmailAddr;
            if ((null == emailAddress) || ("" == emailAddress)) {
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
            string obfuscated = HashHelper.Sha256 (emailAddress.Substring (0, index)) + emailAddress.Substring (index);

            Dictionary<string, string> dict = new Dictionary<string, string> ();
            dict.Add ("sha256_email_address", obfuscated);
            RecordSupport (dict);
        }

        public static void StartService ()
        {
            #if __IOS__
            // FIXME - Add AWS SDK for Android so we can actually run telemetry for Android.
            SharedInstance.Throttling = true;
            SharedInstance.Start ();
            #endif
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
                }, "Telemetry", false, true);
            }
        }

        /// Send a SHA1 hash of the email address of all McAccounts (that have an email addresss)
        private void SendSha1AccountEmailAddresses ()
        {
            foreach (McAccount account in McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange)) {
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

                #if __IOS__
                // FIXME - Need to build Android version of AWS SDK for this
                BackEnd = new TelemetryBEAWS ();
                #endif
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

                while (!BackEnd.IsUseable ()) {
                    NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    Task.WaitAll (new Task[] { Task.Delay (5000, NcTask.Cts.Token) });
                }
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
                    NcAssert.True (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId);
                    List<TelemetryEvent> tEvents = null;
                    List<McTelemetryEvent> dbEvents = null;
                    // Always check for support event first
                    dbEvents = McTelemetrySupportEvent.QueryOne ();
                    if (0 == dbEvents.Count) {
                        // If doesn't have any, check for other events
                        dbEvents = McTelemetryEvent.QueryMultiple (MAX_QUERY_ITEMS);
                    }
                    if (0 == dbEvents.Count) {
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
                    bool succeed = BackEnd.SendEvents (tEvents);
                    transactionTime.Stop ();
                    transactionTime.Reset ();

                    if (succeed) {
                        // If it is a support, make the callback.
                        if ((1 == tEvents.Count) && (tEvents [0].IsSupportEvent ()) && (null != tEvents [0].Callback)) {
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

                        if (MAX_QUERY_ITEMS > dbEvents.Count) {
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
                        // Log only to console. Logging telemetry failures to telemetry is
                        // a vicious cycle.
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

        public string GetUserName ()
        {
            if (BackEnd != null) {
                return BackEnd.GetUserName ();
            } else {
                Log.Info (Log.LOG_LIFECYCLE, "Crash reporting has not been started but user name (clientId) was requested from BackEnd");
                return null;
            }
        }
    }

    public interface ITelemetryBE
    {
        void Initialize ();

        bool IsUseable ();

        string GetUserName ();

        bool SendEvents (List<TelemetryEvent> tEvents);
    }
}
