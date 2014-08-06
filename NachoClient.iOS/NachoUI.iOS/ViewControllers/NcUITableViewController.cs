//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUITableViewController : UITableViewController
    {
        private string AppearingName;
        private string InUseName;
        private string DisappearingName;
        public event EventHandler ViewDisappearing;

        NcCapture Appearing;
        NcCapture InUse;
        NcCapture Disappearing;

        public NcUITableViewController () : base ()
        {
            Initialize ();
        }

        public NcUITableViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        private void Initialize ()
        {
            string className = this.GetType ().Name;
            AppearingName = className + ".Appearing";
            InUseName = className + ".InUse";
            DisappearingName = className + ".Disappearing";

            NcCapture.AddKind (AppearingName);
            NcCapture.AddKind (InUseName);
            NcCapture.AddKind (DisappearingName);

            Appearing = NcCapture.Create (AppearingName);
            InUse = NcCapture.Create (InUseName);
            Disappearing = NcCapture.Create (DisappearingName);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NachoClient.Util.HighPriority ();
        }

        public override void ViewWillAppear (bool animated)
        {
            Appearing.Reset ();
            Appearing.Start ();
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            Appearing.Stop ();
            base.ViewDidAppear (animated);
            InUse.Reset ();
            InUse.Start ();
            NachoClient.Util.RegularPriority ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            InUse.Stop ();
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
            Disappearing.Reset ();
            Disappearing.Start ();
            NachoClient.Util.RegularPriority ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            Disappearing.Stop ();
        }
    }
}

