using System;

using Android.App;
using Android.Content;
using Android.Runtime;
using Android.OS;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.Design.Widget;

using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "Nacho Mail", MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : AppCompatActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            MainApplication.Startup ();

            SkipIfStarted ();

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            var startupFragment = new StartupFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, startupFragment).Commit ();
        }

        protected override void OnStart ()
        {
            base.OnStart ();

            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinishedLaunching: StartClass1Services complete");

            NcApplication.Instance.AppStartupTasks ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        void SkipIfStarted ()
        {
            if (NcApplication.Instance.IsUp ()) {
                Skip ();
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == s.Status.SubKind) {
                if (NcApplication.Instance.IsUp ()) {
                    NachoPlatform.InvokeOnUIThread.Instance.Invoke (delegate () {
                        Skip ();
                    });
                }
            }
        }

        protected override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StartupFinished ()
        {
            var recoveryFragment = new RecoveryFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, recoveryFragment).Commit ();
        }

        public void RecoveryFinished ()
        {
            var migrationFragment = new MigrationFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, migrationFragment).Commit ();
        }

        public void MigrationFinished ()
        {
            var intent = new Intent ();
            intent.SetClass (this, typeof(LaunchActivity));
            StartActivity (intent);
        }

        // Demo only
        public void Skip ()
        {
            var intent = new Intent ();
            intent.SetClass (this, typeof(LaunchActivity));
            StartActivity (intent);
        }

        public override void OnConfigurationChanged (Android.Content.Res.Configuration newConfig)
        {
            base.OnConfigurationChanged (newConfig);
        }

        public override void OnBackPressed ()
        {
//            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }
    }
}


