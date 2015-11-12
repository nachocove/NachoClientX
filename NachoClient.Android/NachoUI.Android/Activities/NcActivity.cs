//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.OS;
using Android.Support.V7.App;
using NachoClient.Build;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    /// <summary>
    /// Activity base class that (1) logs state transitions, and (2) checks with HockeyApp for updates.
    /// </summary>
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
        private const string TELEMETRY_ON_NEWINTENT = "ON_NEWINTENT";

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

        protected override void OnNewIntent (Android.Content.Intent intent)
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_NEWINTENT);
            base.OnNewIntent (intent);
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

    /// <summary>
    /// Activity base class on top of NcActivity that provides a way for activities to save data
    /// that must survive across a configuration change such as rotating the device.  Preserving
    /// the data is not automatic.  Derived classes must call RetainedData at the appropriate time.
    /// </summary>
    public class NcActivityWithData<T> : NcActivity
    {
        private class DataFragment : Android.App.Fragment
        {
            public T Data {
                get;
                set;
            }

            public override void OnCreate (Bundle savedInstanceState)
            {
                base.OnCreate (savedInstanceState);
                this.RetainInstance = true;
            }
        }

        private const string DATA_FRAGMENT_TAG = "DataFragment";

        public T RetainedData {
            get {
                var fragment = FragmentManager.FindFragmentByTag<DataFragment> (DATA_FRAGMENT_TAG);
                if (null == fragment) {
                    return default(T);
                }
                return fragment.Data;
            }
            set {
                var fragment = FragmentManager.FindFragmentByTag<DataFragment> (DATA_FRAGMENT_TAG);
                if (null == fragment) {
                    fragment = new DataFragment ();
                    FragmentManager.BeginTransaction ().Add (fragment, DATA_FRAGMENT_TAG).Commit ();
                }
                fragment.Data = value;
            }
        }
    }
}

