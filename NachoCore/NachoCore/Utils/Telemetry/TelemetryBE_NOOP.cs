//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using NachoPlatform;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{

    public class Telemetry_NOOP : ITelemetry
    {
        #region ITelemetry implementation
        public void StartService ()
        {
        }
        public void FinalizeAll ()
        {
        }
        public void RecordLogEvent (Log log, Log.Level level, string fmt, params object[] list)
        {
        }
        public void RecordWbxmlEvent (bool isRequest, byte[] wbxml)
        {
        }
        public void RecordImapEvent (bool isRequest, byte[] payload)
        {
        }
        public void RecordCounter (string name, long count, DateTime start, DateTime end)
        {
        }
        public void RecordUiBarButtonItem (string uiObject)
        {
        }
        public void RecordUiButton (string uiObject)
        {
        }
        public void RecordUiSegmentedControl (string uiObject, long index)
        {
        }
        public void RecordUiSwitch (string uiObject, string onOff)
        {
        }
        public void RecordUiDatePicker (string uiObject, string date)
        {
        }
        public void RecordUiTextField (string uiObject)
        {
        }
        public void RecordUiPageControl (string uiObject, long page)
        {
        }
        public void RecordUiViewController (string uiObject, string state)
        {
        }
        public void RecordUiAlertView (string uiObject, string action)
        {
        }
        public void RecordUiActionSheet (string uiObject, long index)
        {
        }
        public void RecordUiTapGestureRecognizer (string uiObject, string touches)
        {
        }
        public void RecordUiTableView (string uiObject, string operation)
        {
        }
        public void RecordSupport (Dictionary<string, string> info, Action callback = null)
        {
        }
        public void RecordAccountEmailAddress (McAccount account)
        {
        }
        public void RecordIntSamples (string samplesName, List<int> samplesValues)
        {
        }
        public void RecordFloatSamples (string samplesName, List<double> samplesValues)
        {
        }
        public void RecordStringSamples (string samplesName, List<string> samplesValues)
        {
        }
        public void RecordStatistics2 (string name, int count, int min, int max, long sum, long sum2)
        {
        }
        public void RecordIntTimeSeries (string name, DateTime time, int value)
        {
        }
        public void RecordFloatTimeSeries (string name, DateTime time, double value)
        {
        }
        public void RecordStringTimeSeries (string name, DateTime time, string value)
        {
        }
        public bool Throttling { get; set; }
        #endregion
    }

    public class TelemetryBE_NOOP : ITelemetryBE
    {
        #region ITelemetryBE implementation

        public string GetUserName ()
        {
            return Device.Instance.Identity ();
        }

        public bool UploadEvents (string jsonFilePath)
        {
            return true;
        }

        #endregion
    }

    public class TelemetryJsonFileTable_NOOP : ITelementryDB
    {
        #region ITelementryDB implementation

        public string GetNextReadFile ()
        {
            return null;
        }

        public bool Add (TelemetryJsonEvent jsonEvent)
        {
            return true;
        }

        public void Remove (string fileName, out Action supportCallback)
        {
            supportCallback = null;
            return;
        }

        public void FinalizeAll ()
        {
        }

        #endregion
    }
}

