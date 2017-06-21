//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;

namespace NachoCore.Utils
{
    public interface ITelemetry
    {
        void StartService ();

        bool Throttling { get; set; }

        void FinalizeAll ();

        void RecordLogEvent (Log log, Log.Level level, string fmt, params object[] list);

        void RecordWbxmlEvent (bool isRequest, byte[] wbxml);

        void RecordImapEvent (bool isRequest, byte[] payload);

        void RecordCounter (string name, Int64 count, DateTime start, DateTime end);

        void RecordUiBarButtonItem (string uiObject);

        void RecordUiButton (string uiObject);

        void RecordUiSegmentedControl (string uiObject, long index);

        void RecordUiSwitch (string uiObject, string onOff);

        void RecordUiDatePicker (string uiObject, string date);

        void RecordUiTextField (string uiObject);

        void RecordUiPageControl (string uiObject, long page);

        void RecordUiViewController (string uiObject, string state);

        void RecordUiAlertView (string uiObject, string action);

        void RecordUiActionSheet (string uiObject, long index);

        void RecordUiTapGestureRecognizer (string uiObject, string touches);

        void RecordUiTableView (string uiObject, string operation);

        void RecordSupport (Dictionary<string, string> info, Action callback = null);

        void RecordAccountEmailAddress (McAccount account);

        void RecordIntSamples (string samplesName, List<int> samplesValues);

        void RecordFloatSamples (string samplesName, List<double> samplesValues);

        void RecordStringSamples (string samplesName, List<string> samplesValues);

        void RecordStatistics2 (string name, int count, int min, int max, long sum, long sum2);

        void RecordIntTimeSeries (string name, DateTime time, int value);

        void RecordFloatTimeSeries (string name, DateTime time, double value);

        void RecordStringTimeSeries (string name, DateTime time, string value);
    }


}

