//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUIViewController : UIViewController
    {
        private string ClassName;
        public event EventHandler ViewDisappearing;

        public NcUIViewController () : base()
        {
            Initialize ();
        }

        public NcUIViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcUIViewController (string nibName, NSBundle bundle) : base (nibName, bundle)
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
            NachoClient.Util.HighPriority ();
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
            NachoClient.Util.RegularPriority ();
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
            NachoClient.Util.RegularPriority ();
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_END");
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_BEGIN");
            base.ViewDidDisappear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_END");
        }
    }

    public abstract class NcUIViewControllerNoLeaks : NcUIViewController
    {
        public NcUIViewControllerNoLeaks ()
            : base()
        {
        }

        public NcUIViewControllerNoLeaks (IntPtr handle)
            : base (handle)
        {
        }

        public NcUIViewControllerNoLeaks (string nibName, NSBundle bundle)
            : base (nibName, bundle)
        {
        }

        protected abstract void CreateViewHierarchy ();

        protected abstract void ConfigureAndLayout ();

        protected abstract void Cleanup ();

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateViewHierarchy ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            // Force the view hierarchy to be created by accessing the View property.
            this.View.GetHashCode ();
            ConfigureAndLayout ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (this.IsViewLoaded && null == this.NavigationController) {
                Cleanup ();
                ViewHelper.DisposeViewHierarchy (View);
                View = null;
            }
        }
    }
}

