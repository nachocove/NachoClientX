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
    [Activity ()]
    public class MainActivity : NcActivity
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

            MainApplication.RegisterHockeyAppUpdateManager (this);

            MainApplication.OneTimeStartup ("MainActivity");

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            var startupFragment = new StartupFragment ();
            FragmentManager.BeginTransaction ().Replace (Resource.Id.content, startupFragment).Commit ();
        }

        protected override void OnResume ()
        {
            base.OnResume ();

            MainApplication.SetupHockeyAppCrashManager (this);

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
            MainApplication.UnregisterHockeyAppUpdateManager ();
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
            base.OnBackPressed ();
        }

        protected override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }


    }
}


