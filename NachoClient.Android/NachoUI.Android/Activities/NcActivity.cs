//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.OS;
using Android.Support.V7.App;
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

            MainApplication.RegisterHockeyAppUpdateManager (this);
        }

        protected override void OnPause ()
        {
            Telemetry.RecordUiViewController (ClassName, TELEMETRY_ON_PAUSE);
            base.OnPause ();

            MainApplication.UnregisterHockeyAppUpdateManager ();

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
    }

    /// <summary>
    /// Store an object within a fragment.  This class cannot be generic, which means it cannot
    /// be nested inside NcActivityWithData&lt;T&gt;, which is the only place that should use
    /// this class.  (See https://developer.xamarin.com/guides/android/advanced_topics/limitations/
    /// for an explatation.  In particular: "Instances of Generic types must not be created from
    /// Java code. They can only safely be created from managed code.")
    /// </summary>
    public class DataFragment : Android.App.Fragment
    {
        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.RetainInstance = true;
        }

        private object data = null;

        public T GetData<T> () where T : class
        {
            return (T)data;
        }

        public void SetData<T> (T data) where T : class
        {
            this.data = data;
        }
    }

    /// <summary>
    /// Activity base class on top of NcActivity that provides a way for activities to save data
    /// that must survive across a configuration change such as rotating the device.  Preserving
    /// the data is not automatic.  Derived classes must call RetainedData at the appropriate time.
    /// </summary>
    public class NcActivityWithData<T> : NcActivity where T : class
    {
        private const string DATA_FRAGMENT_TAG = "DataFragment";

        public T RetainedData {
            get {
                var fragment = FragmentManager.FindFragmentByTag<DataFragment> (DATA_FRAGMENT_TAG);
                if (null == fragment) {
                    return null;
                }
                return fragment.GetData<T> ();
            }
            set {
                var fragment = FragmentManager.FindFragmentByTag<DataFragment> (DATA_FRAGMENT_TAG);
                if (null == fragment) {
                    fragment = new DataFragment ();
                    FragmentManager.BeginTransaction ().Add (fragment, DATA_FRAGMENT_TAG).Commit ();
                }
                fragment.SetData (value);
            }
        }
    }
}
