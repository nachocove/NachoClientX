//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

namespace NachoCore.Utils
{
    // There are serialized objects using these enum in teledb. So, you must
    // add new enums to the end.
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
        // TODO - Capture will be replaced by Statistics2. Capture will continue to
        // go to DynamoDB while Statistics2 will go to S3 on day 1.
        CAPTURE,
        UI,
        SUPPORT,
        SAMPLES,
        DISTRIBUTION,
        STATISTICS2,
        IMAP_REQUEST,
        IMAP_RESPONSE,
        TIME_SERIES,
        SUPPORT_REQUEST,
        MAX_TELEMETRY_EVENT_TYPE,
    };

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

        ///////////////////////////////////////////////////////////////////////////////////////
        /// LOG EVENTS
        ///////////////////////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////////////////////
        /// WBXML EVENTS
        ///////////////////////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////////////////////
        /// COUNTER EVENT
        ///////////////////////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////////////////////
        /// CAPTURE EVENT
        ///////////////////////////////////////////////////////////////////////////////////////

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

        ///////////////////////////////////////////////////////////////////////////////////////
        /// SAMPLES EVENT
        ///////////////////////////////////////////////////////////////////////////////////////

        // Samples name
        private string _SamplesName;

        public string SamplesName {
            get {
                return _SamplesName;
            }
            set {
                NcAssert.True (IsSamplesEvent ());
                _SamplesName = value;
            }
        }

        // Samples values
        private List<int> _Samples;

        public List<int> Samples {
            get {
                return _Samples;
            }
            set {
                NcAssert.True (IsSamplesEvent ());
                _Samples = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////
        /// DISTRIBTION EVENT
        ///////////////////////////////////////////////////////////////////////////////////////

        // Distribution Name
        private string _DistributionName;

        public string DistributionName {
            get {
                return _DistributionName;
            }
            set {
                NcAssert.True (IsDistributionEvent ());
                _DistributionName = value;
            }
        }

        // CDF expresses as a list of upper bin value and frequency count
        public List<KeyValuePair<int, int>> _Cdf;

        public List<KeyValuePair<int, int>> Cdf {
            get {
                return _Cdf;
            }
            set {
                NcAssert.True (IsDistributionEvent ());
                _Cdf = value;
            }
        }

        ///////////////////////////////////////////////////////////////////////////////////////
        /// UI EVENT - Note that types and objects platform-dependent.
        ///////////////////////////////////////////////////////////////////////////////////////

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
                NcAssert.True (IsSupportEvent () || IsSupportRequestEvent ());
                _Support = value;
            }
        }

        private Action _Callback;

        public Action Callback {
            get {
                return _Callback;
            }
            set {
                NcAssert.True (IsSupportRequestEvent ());
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

        public static bool IsSamplesEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.SAMPLES == type);
        }

        public static bool IsDistributionEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.DISTRIBUTION == type);
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

        public static bool IsSupportRequestEvent (TelemetryEventType type)
        {
            return (TelemetryEventType.SUPPORT_REQUEST == type);
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

        public bool IsSamplesEvent ()
        {
            return IsSamplesEvent (Type);
        }

        public bool IsDistributionEvent ()
        {
            return IsDistributionEvent (Type);
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

        public bool IsSupportRequestEvent ()
        {
            return IsSupportRequestEvent (Type);
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

}

