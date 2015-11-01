//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Support.V7.App;
using Android.OS;
using NachoCore.Utils;
using NachoClient.Build;

namespace NachoClient.AndroidClient
{
    public class NcActivity : AppCompatActivity
    {
        private string ClassName;

        private const string TELEMETRY_ON_CREATE = "ON_CREATE";
        private const string TELEMETRY_ON_START = "ON_START";
        private const string TELEMETRY_ON_RESUME = "ON_RESUME";
        private const string TELEMETRY_ON_PAUSE = "ON_PAUSE";
        private const string TELEMETRY_ON_STOP = "ON_STOP";
        private const string TELEMETRY_ON_DESTROY = "ON_DESTROY";
        private const string TELEMETRY_ON_RESTART = "ON_RESTART";

        bool updateRegistered;

        protected override void OnCreate (Bundle savedInstanceState)
        {
            ClassName = this.GetType ().Name;
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_CREATE);
            base.OnCreate (savedInstanceState);
        }

        protected override void OnStart ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_START);
            base.OnStart ();
        }

        protected override void OnResume ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_RESUME);
            base.OnResume ();

            if (MainApplication.CheckOnceForUpdates ()) {
                updateRegistered = true;
                HockeyApp.UpdateManager.Register (this, BuildInfo.HockeyAppAppId, new MyCustomUpdateManagerListener(), true);
            }

        }

        protected override void OnPause ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_PAUSE);
            base.OnPause ();

            if (updateRegistered) {
                HockeyApp.UpdateManager.Unregister ();
            }
        }

        protected override void OnStop ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_STOP);
            base.OnStop ();
        }

        protected override void OnDestroy ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_DESTROY);
            base.OnDestroy ();
        }

        protected override void OnRestart ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_RESTART);
            base.OnRestart ();
        }

        public class MyCustomUpdateManagerListener : HockeyApp.UpdateManagerListener
        {
            public override void OnUpdateAvailable ()
            {
                Log.Info (Log.LOG_SYS, "HA: OnUpdateAvailable");
                base.OnUpdateAvailable ();
            }

            public override void OnNoUpdateAvailable ()
            {
                Log.Info (Log.LOG_SYS, "HA: OnNoUpdateAvailable");
                base.OnNoUpdateAvailable ();
            }
        }

    }
}

