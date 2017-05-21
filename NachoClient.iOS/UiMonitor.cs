//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using Foundation;
using UIKit;

using NachoUIMonitorBinding;
using NachoCore;

namespace NachoClient.iOS
{
    public class UiMonitor
    {
        public UiMonitor ()
        {
        }

        public void Start ()
        {
            NachoUIMonitor.SetupUIButton (delegate(string description) {
                NcApplication.Instance.TelemetryService.RecordUiButton (description);
            });

            NachoUIMonitor.SetupUISegmentedControl (delegate(string description, int index) {
                NcApplication.Instance.TelemetryService.RecordUiSegmentedControl (description, index);
            });

            NachoUIMonitor.SetupUISwitch (delegate(string description, string onOff) {
                NcApplication.Instance.TelemetryService.RecordUiSwitch (description, onOff);
            });

            NachoUIMonitor.SetupUIDatePicker (delegate(string description, string date) {
                NcApplication.Instance.TelemetryService.RecordUiDatePicker (description, date);
            });

            NachoUIMonitor.SetupUITextField (delegate(string description) {
                NcApplication.Instance.TelemetryService.RecordUiTextField (description);
            });

            NachoUIMonitor.SetupUIPageControl (delegate(string description, int page) {
                NcApplication.Instance.TelemetryService.RecordUiPageControl (description, page);
            });

            // Alert views are monitored inside NcAlertView

            NachoUIMonitor.SetupUIActionSheet (delegate(string description, int index) {
                NcApplication.Instance.TelemetryService.RecordUiActionSheet (description, index);
            });

            NachoUIMonitor.SetupUITapGestureRecognizer (delegate(string description, int numTouches,
                                                                 PointF point1, PointF point2, PointF point3) {
                string touches = "";
                if (0 < numTouches) {
                    touches = String.Format ("({0},{1})", point1.X, point1.Y);
                    if (1 < numTouches) {
                        touches += String.Format (", ({0},{1})", point2.X, point2.Y);
                        if (2 < numTouches) {
                            touches += String.Format (", ({0},{1})", point3.X, point3.Y);
                        }
                    }
                }
                NcApplication.Instance.TelemetryService.RecordUiTapGestureRecognizer (description, touches);
            });

            NachoUIMonitor.SetupUITableView (delegate(string description, string operation) {
                NcApplication.Instance.TelemetryService.RecordUiTableView (description, operation);
            });
        }
    }
}
