//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.Dialog;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcDialogViewController : DialogViewController
    {
        private string ClassName;

        public NcDialogViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcDialogViewController (RootElement root, bool push) : base (root, push)
        {
            Initialize ();
        }

        private void Initialize ()
        {
            ClassName = this.GetType ().Name;
        }

        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_BEGIN");
            base.ViewWillAppear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_END");
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_BEGIN");
            base.ViewDidAppear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.ViewWillDisappear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_END");
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_BEGIN");
            base.ViewDidDisappear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_END");
        }
    }
}

