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

        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR);
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR);
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR);
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR);
            base.ViewDidDisappear (animated);
        }
    }
}

