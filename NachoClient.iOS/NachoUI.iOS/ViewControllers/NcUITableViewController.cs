//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUITableViewController : UITableViewController
    {
        private string ClassName;
        public event EventHandler ViewDisappearing;

        public NcUITableViewController () : base ()
        {
            Initialize ();
        }

        public NcUITableViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcUITableViewController (UITableViewStyle style) : base (style)
        {
            Initialize ();
        }

        private void Initialize ()
        {
            ClassName = this.GetType ().Name;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NachoCore.Utils.NcAbate.HighPriority ("NcUITableViewController ViewDidLoad");
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
            NachoCore.Utils.NcAbate.RegularPriority ("NcUITableViewController ViewDidAppear");
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
            NachoCore.Utils.NcAbate.RegularPriority ("NcUITableViewController ViewWillDisappear");
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

