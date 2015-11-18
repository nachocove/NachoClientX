using System;
using System.IO;

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
using NachoCore.Model;
using System.Threading.Tasks;
using NachoClient.Build;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    [Activity (MainLauncher = true, Icon = "@drawable/icon")]
    public class MainActivity : AppCompatActivity
    {
        bool StatusIndCallbackIsSet = false;

        enum StartupViewState
        {
            Startup,
            Incompatible,
            Setup,
            Migration,
            Recovery,
            Blank,
            App
        }

        StartupViewState currentState = StartupViewState.Startup;

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetupHockeyAppUpdateManager ();

            MainApplication.OneTimeStartup ();

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            var startupFragment = new StartupFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, startupFragment).Commit ();
        }

        protected override void OnStart ()
        {
            base.OnStart ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();

            SetupHockeyAppCrashManager ();

            if (!NcMigration.IsCompatible ()) {
                Log.Info (Log.LOG_UI, "MainActivity: found incompatible migration");
                currentState = StartupViewState.Incompatible;
                // Display an alert view and wait to get out
                new Android.Support.V7.App.AlertDialog.Builder (this).SetTitle (Resource.String.incompatible_version).SetMessage (Resource.String.incompatible_message).Show ();
                return;
            }
            if (currentState == StartupViewState.Startup) {
                Log.Info (Log.LOG_UI, "MainActivity: onResume in Startup state, determining where to go");
                ShowScreenForApplicationState ();
            }
        }

        protected override void OnPause ()
        {
            base.OnPause ();
            UnregisterHockeyAppUpdateManager ();
        }

        protected override void OnDestroy ()
        {
            base.OnDestroy ();
            UnregisterHockeyAppUpdateManager ();
        }

        void ShowScreenForApplicationState ()
        {
            if (NcApplication.Instance.IsUp ()) {
                StopListeningForApplicationStatus ();
                var configAccount = McAccount.GetAccountBeingConfigured ();
                var deviceAccount = McAccount.GetDeviceAccount ();
                var mdmAccount = McAccount.GetMDMAccount ();
                if (null != configAccount) {
                    Log.Info (Log.LOG_UI, "MainActivity: found account being configured");
                    ShowSetupScreen ();
                } else if (null == mdmAccount && NcMdmConfig.Instance.IsPopulated) {
                    ShowSetupScreen ();
                } else if (null == NcApplication.Instance.Account) {
                    Log.Info (Log.LOG_UI, "MainActivity: null NcApplication.Instance.Account");
                    ShowSetupScreen ();
                } else if ((null != deviceAccount) && (deviceAccount.Id == NcApplication.Instance.Account.Id)) {
                    Log.Info (Log.LOG_UI, "MainActivity: NcApplication.Instance.Account is deviceAccount");
                    ShowSetupScreen ();
                } else if (!NcApplication.ReadyToStartUI ()) {
                    Log.Info (Log.LOG_UI, "MainActivity: not ready to start UI, assuming tutorial still needs display");
                    // This should only be if the app closed before the tutorial was dismissed;
                    ShowSetupScreen (true);
                } else {
                    Log.Info (Log.LOG_UI, "MainActivity: Ready to go, showing application");
                    ShowApplication ();
                }
            } else {
                StartListeningForApplicationStatus ();
                if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Migrating) {
                    Log.Info (Log.LOG_UI, "MainActivity: instance isn't up yet, in Migrating state");
                    ShowMigrationScreen ();
                } else if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Initializing) {
                    Log.Info (Log.LOG_UI, "MainActivity: instance isn't up yet, in Initializing state");
                    if (NcApplication.Instance.InSafeMode ()) {
                        ShowRecoveryScreen ();
                    } else {
                        Log.Info (Log.LOG_UI, "MainActivity initializing, but not in safe mode, keeping current screen");
                    }
                } else {
                    // I don't think we can ever get here based on the definition of NcApplication.IsUp.  If things
                    // change (like a new state is added to NcApplication), this will just result in a blank screen
                    // until the application is up.
                    currentState = StartupViewState.Blank;
                    Log.Info (Log.LOG_UI, "MainActivity instance isn't up yet, in unexpected state, showing BLANK");
                }
            }
        }

        void ShowApplication ()
        {
            if (currentState == StartupViewState.App) {
                return;
            }
            currentState = StartupViewState.App;

            Log.Info (Log.LOG_UI, "MainActivity ShowApplication");

            var intent = NcTabBarActivity.HotListIntent (this);
            StartActivity (intent);
        }

        void ShowSetupScreen (bool startWithTutorial = false)
        {
            if (currentState == StartupViewState.Setup) {
                return;
            }
            currentState = StartupViewState.Setup;

            Log.Info (Log.LOG_UI, "MainActivity ShowApplication");

            var intent = new Intent ();
            intent.SetClass (this, typeof(LaunchActivity));
            StartActivity (intent);
        }

        void ShowRecoveryScreen ()
        {
            if (currentState == StartupViewState.Recovery) {
                return;
            }
            currentState = StartupViewState.Recovery;

            Log.Info (Log.LOG_UI, "MainActivity ShowRecoveryScreen");

            var recoveryFragment = new RecoveryFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, recoveryFragment).Commit ();
        }

        void ShowMigrationScreen ()
        {
            if (currentState == StartupViewState.Migration) {
                return;
            }
            currentState = StartupViewState.Migration;

            Log.Info (Log.LOG_UI, "MainActivity ShowMigrationScreen");

            var migrationFragment = new MigrationFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, migrationFragment).Commit ();

            currentState = StartupViewState.Migration;
        }

        void StartListeningForApplicationStatus ()
        {
            if (!StatusIndCallbackIsSet) {
                StatusIndCallbackIsSet = true;
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        void StopListeningForApplicationStatus ()
        {
            if (StatusIndCallbackIsSet) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                StatusIndCallbackIsSet = false;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "MainActivity got ExecutionContextChanged event");
                ShowScreenForApplicationState ();
            }
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

        #region HockeyApp
        public class MyCustomCrashManagerListener : HockeyApp.CrashManagerListener
        {
            public string LastTrace { get; set; }

            public override bool ShouldAutoUploadCrashes ()
            {
                return true;
            }
            public override string Description {
                get {
                    var descr = NcApplication.ApplicationLogForCrashManager ();
                    if (!string.IsNullOrEmpty (LastTrace))
                    {
                        descr += "\n" + LastTrace;
                        LastTrace = null;
                    }
                    return descr;
                }
            }
            public override bool IncludeDeviceData ()
            {
                return true;
            }
            public override bool IncludeDeviceIdentifier ()
            {
                return true;
            }
            public override int MaxRetryAttempts {
                get {
                    return 1000;
                }
            }
        }

        class UnCaughtExceptionHandler : Java.Lang.Object, Java.Lang.Thread.IUncaughtExceptionHandler
        {
            MyCustomCrashManagerListener CrashListener;
            public UnCaughtExceptionHandler(MyCustomCrashManagerListener theListener)
            {
                CrashListener = theListener;
            }

            public void UncaughtException(Java.Lang.Thread thread, Java.Lang.Throwable ex)
            {
                CrashListener.LastTrace = ex.GetStackTrace ().ToString ();
                HockeyApp.TraceWriter.WriteTrace (ex);
            }
        }

        private void SetupHockeyAppCrashManager ()
        {
            if (BuildInfoHelper.IsDev) {
                return;
            }
            var myListener = new MyCustomCrashManagerListener ();
            // Register the crash manager before Initializing the trace writer
            HockeyApp.CrashManager.Register (this, BuildInfo.HockeyAppAppId, myListener); 

            // Initialize the Trace Writer
            HockeyApp.TraceWriter.Initialize (myListener);

            // Wire up Unhandled Expcetion handler from Android
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) => {
                // Use the trace writer to log exceptions so HockeyApp finds them
                myListener.LastTrace = args.Exception.ToString ();
                HockeyApp.TraceWriter.WriteTrace(args.Exception);
                args.Handled = true;
            };

            // Wire up the .NET Unhandled Exception handler
            AppDomain.CurrentDomain.UnhandledException += (sender, args) => {
                myListener.LastTrace = args.ExceptionObject.ToString ();
                HockeyApp.TraceWriter.WriteTrace (args.ExceptionObject);
            };

            // Wire up the unobserved task exception handler
            TaskScheduler.UnobservedTaskException += (sender, args) => {
                myListener.LastTrace = args.Exception.ToString ();
                HockeyApp.TraceWriter.WriteTrace (args.Exception);
            };

            Java.Lang.Thread.DefaultUncaughtExceptionHandler = new UnCaughtExceptionHandler (myListener);
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

        private void SetupHockeyAppUpdateManager ()
        {
            if (BuildInfoHelper.IsDev) {
                return;
            }
            //Register to with the Update Manager
            HockeyApp.UpdateManager.Register (this, BuildInfo.HockeyAppAppId, new MyCustomUpdateManagerListener(), true);
        }

        private void UnregisterHockeyAppUpdateManager ()
        {
            HockeyApp.UpdateManager.Unregister ();
        }
        #endregion
    }
}


