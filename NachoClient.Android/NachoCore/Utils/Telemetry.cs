﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
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
        MAX_TELEMETRY_EVENT_TYPE
    };

    [Serializable]
    public class TelemetryEvent : NcQueueElement
    {
        // iOS UI object monitoring strings
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

        // Average
        private uint _Average;

        public uint Average {
            get {
                return _Average;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _Average = value;
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

        // Capture Std. Dev
        private uint _StdDev;

        public uint StdDev {
            get {
                return _StdDev;
            }
            set {
                NcAssert.True (IsCaptureEvent ());
                _StdDev = value;
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
            _Type = type;
            _Message = null;
            _Wbxml = null;
            _CounterName = null;
            _Count = 0;
            _CounterStart = new DateTime ();
            _CounterEnd = new DateTime ();
            _CaptureName = null;
            _Average = 0;
            _Min = 0;
            _Max = 0;
            _StdDev = 0;
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
        private static bool PERSISTED = true;
        // Parse has a maximum data size of 128K for PFObject. But the
        // exact definition of data size of an object with multiple
        // fields is not clear. So, we just limit the log messages and
        // redacted WBXML to 120 KB to leave some headroom for other fields.
        private const int MAX_PARSE_LEN = 120 * 1024;

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

        private NcQueue<TelemetryEvent> EventQueue;

        private AutoResetEvent DbUpdated;

        private ITelemetryBE BackEnd;

        NcCounter[] Counters;
        NcCounter FailToSend;

        public Telemetry ()
        {
            EventQueue = new NcQueue<TelemetryEvent> ();
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

        private static void RecordRawEvent (TelemetryEvent tEvent)
        {
            SharedInstance.Counters [(int)tEvent.Type].Click ();
            if (!PERSISTED) {
                SharedInstance.EventQueue.Enqueue (tEvent);
            } else {
                McTelemetryEvent dbEvent = new McTelemetryEvent (tEvent);
                dbEvent.Insert ();
                Telemetry.SharedInstance.DbUpdated.Set ();
            }
        }

        public static void RecordLogEvent (TelemetryEventType type, string fmt, params object[] list)
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

        public static void RecordCapture (string name, uint count, uint average, uint min, uint max, uint stddev)
        {
            if (!ENABLED) {
                return;
            }

            TelemetryEvent tEvent = new TelemetryEvent (TelemetryEventType.CAPTURE);

            tEvent.CaptureName = name;
            tEvent.Count = count;
            tEvent.Average = average;
            tEvent.Min = min;
            tEvent.Max = max;
            tEvent.StdDev = stddev;

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

            TelemetryEvent tEvent = GetTelemetryEvent(uiType, uiObject);
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

        public static void RecordUiAlertView (string uiObject, long index)
        {
            RecordUiWithLong (TelemetryEvent.UIALERTVIEW, uiObject, index);
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

        private static string Sha2HashString (string s)
        {
            byte[] bytes = Encoding.ASCII.GetBytes (s);
            SHA256 sha256 = SHA256.Create ();
            sha256.ComputeHash (bytes);

            string hash = "";
            for (int n = 0; n < sha256.Hash.Length; n++) {
                hash += String.Format ("{0:x2}", sha256.Hash [n]);
            }
            return hash;
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
            string obfuscated = Sha2HashString (emailAddress.Substring (0, index)) + emailAddress.Substring (index);

            Dictionary<string, string> dict = new Dictionary<string, string> ();
            dict.Add ("sha256_email_address", obfuscated);
            RecordSupport (dict);
        }

        public void Start<T> () where T : ITelemetryBE, new()
        {
            if (!ENABLED) {
                return;
            }
            NcTask.Run (() => {
                EventQueue.Token = NcTask.Cts.Token;
                Process<T> ();
            }, "Telemetry");
        }

        /// Send a SHA1 hash of the email address of all McAccounts (that have an email addresss)
        private void SendSha1AccountEmailAddresses ()
        {
            foreach (McAccount account in McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange)) {
                RecordAccountEmailAddress (account);
            }
        }

        private void Process<T> () where T : ITelemetryBE, new()
        {
            BackEnd = new T ();
            Counters [0].ReportPeriod = 5 * 60; // report once every 5 min

            // Capture the transaction time to telemetry server
            const string CAPTURE_NAME = "Telemetry.SendEvent";
            NcCapture.AddKind (CAPTURE_NAME);
            NcCapture transactionTime = NcCapture.Create (CAPTURE_NAME);

            while (!BackEnd.IsUseable ()) {
                NcTask.Cts.Token.ThrowIfCancellationRequested ();
                Task.WaitAll (new Task[] { Task.Delay (5000, NcTask.Cts.Token) });
            }

            SendSha1AccountEmailAddresses ();
            while (true) {
                // TODO - We need to be smart about when we run. 
                // For example, if we don't have WiFi, it may not be a good
                // idea to upload a lot of data. The exact algorithm is TBD.
                // But for now, let's not run when we're scrolling.
                NcAssert.True (NcApplication.Instance.UiThreadId != System.Threading.Thread.CurrentThread.ManagedThreadId);
                TelemetryEvent tEvent = null;
                McTelemetryEvent dbEvent = null;
                if (!PERSISTED) {
                    tEvent = EventQueue.Dequeue ();
                } else {
                    dbEvent = McTelemetryEvent.QueryOne ();
                    if (null == dbEvent) {
                        // No pending event. Wait for one.
                        while (!DbUpdated.WaitOne (NcTask.MaxCancellationTestInterval)) {
                            NcTask.Cts.Token.ThrowIfCancellationRequested ();
                        }
                        continue;
                    } else {
                        NcTask.Cts.Token.ThrowIfCancellationRequested ();
                    }
                    tEvent = dbEvent.GetTelemetryEvent ();
                }

                // Send it to the telemetry server
                transactionTime.Start ();
                bool succeed = BackEnd.SendEvent (tEvent);
                transactionTime.Stop ();
                transactionTime.Reset ();

                if (succeed) {
                    // If it is a support, make the callback.
                    if (tEvent.IsSupportEvent () && (null != tEvent.Callback)) {
                        InvokeOnUIThread.Instance.Invoke (tEvent.Callback);
                    }

                    if (null != dbEvent) {
                        dbEvent.Delete ();
                    }
                } else {
                    // Log only to console. Logging telemetry failures to telemetry is
                    // a vicious cycle.
                    Console.WriteLine ("fail to reach telemetry server");
                    FailToSend.Click ();
                }
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
        bool IsUseable ();

        string GetUserName ();

        bool SendEvent (TelemetryEvent tEvent);
    }
}
