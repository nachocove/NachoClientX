//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUIViewController : UIViewController
    {
        private string AppearingName;
        private string InUseName;
        private string DisappearingName;

        NcCapture Appearing;
        NcCapture InUse;
        NcCapture Disappearing;

        public NcUIViewController () : base()
        {
            Initialize ();
        }

        public NcUIViewController (IntPtr handle) : base (handle)
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
        }

        public override void ViewWillDisappear (bool animated)
        {
            InUse.Stop ();
            base.ViewWillDisappear (animated);
            Disappearing.Reset ();
            Disappearing.Start ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            Disappearing.Stop ();
        }
    }
}

