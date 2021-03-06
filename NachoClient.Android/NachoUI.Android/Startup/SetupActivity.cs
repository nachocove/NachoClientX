﻿using System;
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
    [Activity (LaunchMode=Android.Content.PM.LaunchMode.SingleTop)]
    public class SetupActivity : NcActivity
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

        private const int REQUEST_WELCOME = 1;

        #region Intents

        public static Intent BuildIntent (Context context)
        {
            var intent = new Intent (context, typeof (SetupActivity));
            return intent;
        }

        #endregion

        #region Activity Lifecycle

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            MainApplication.RegisterHockeyAppUpdateManager (this);
            MainApplication.OneTimeStartup ("SetupActivity");

            SetContentView (Resource.Layout.SetupActivity);
        }

        protected override void OnResume ()
        {
            base.OnResume ();

            MainApplication.SetupHockeyAppCrashManager (this);

            if (!NcMigration.IsCompatible ()) {
                Log.Info (Log.LOG_UI, "SetupActivity: found incompatible migration");
                currentState = StartupViewState.Incompatible;
                // Display an alert view and wait to get out
                new Android.Support.V7.App.AlertDialog.Builder (this).SetTitle (Resource.String.startup_incompatible_version).SetMessage (Resource.String.startup_incompatible_message).Show ();
                return;
            }
            if (currentState == StartupViewState.Startup) {
                Log.Info (Log.LOG_UI, "SetupActivity: onResume in Startup state, determining where to go");
                ShowScreenForApplicationState ();
            }
        }

        protected override void OnPause ()
        {
            base.OnPause ();
            MainApplication.UnregisterHockeyAppUpdateManager ();
        }

        protected override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_WELCOME:
                ShowScreenForApplicationState ();
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region View Management

        void ShowScreenForApplicationState ()
        {
            if (NcApplication.Instance.IsUp ()) {
                StopListeningForApplicationStatus ();
                if (NcApplication.ReadyToStartUI ()) {
                    Log.Info (Log.LOG_UI, "SetupActivity: Ready to go, showing application");
                    ShowApplication ();
                } else {
                    Log.Info (Log.LOG_UI, "SetupActivity: not ready to start UI, assuming tutorial still needs display");
					// This should only be if the app closed before the tutosed;
                    ShowSetupScreen ();
                }
            } else {
                StartListeningForApplicationStatus ();
                if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Migrating) {
                    Log.Info (Log.LOG_UI, "SetupActivity: instance isn't up yet, in Migrating state");
                    ShowMigrationScreen ();
                } else if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Initializing) {
                    Log.Info (Log.LOG_UI, "SetupActivity: instance isn't up yet, in Initializing state");
                    if (NcApplication.Instance.InSafeMode ()) {
                        ShowRecoveryScreen ();
                    } else {
                        Log.Info (Log.LOG_UI, "SetupActivity initializing, but not in safe mode, keeping current screen");
                    }
                } else {
                    // I don't think we can ever get here based on the definition of NcApplication.IsUp.  If things
                    // change (like a new state is added to NcApplication), this will just result in a blank screen
                    // until the application is up.
                    currentState = StartupViewState.Blank;
                    Log.Info (Log.LOG_UI, "SetupActivity instance isn't up yet, in unexpected state, showing BLANK");
                }
            }
        }

        void ShowApplication ()
        {
            if (currentState == StartupViewState.App) {
                return;
            }
            currentState = StartupViewState.App;

            Log.Info (Log.LOG_UI, "SetupActivity ShowApplication");

            var intent = new Intent (this, typeof(MainTabsActivity));
            StartActivity (intent);
            Finish ();
        }

        void ShowSetupScreen ()
        {
            if (currentState == StartupViewState.Setup) {
                return;
            }
            currentState = StartupViewState.Setup;

            Log.Info (Log.LOG_UI, "SetupActivity ShowApplication");

            var intent = WelcomeActivity.BuildIntent (this);
            StartActivityForResult (intent, REQUEST_WELCOME);
        }

        void ShowRecoveryScreen ()
        {
            if (currentState == StartupViewState.Recovery) {
                return;
            }
            currentState = StartupViewState.Recovery;

            Log.Info (Log.LOG_UI, "SetupActivity ShowRecoveryScreen");

            var transaction = FragmentManager.BeginTransaction ();
            transaction.Replace (Resource.Id.container, new RecoveryFragment ());
            transaction.Commit ();
        }

        void ShowMigrationScreen ()
        {
            if (currentState == StartupViewState.Migration) {
                return;
            }
            currentState = StartupViewState.Migration;

            Log.Info (Log.LOG_UI, "SetupActivity ShowMigrationScreen");

            var transaction = FragmentManager.BeginTransaction ();
            transaction.Replace (Resource.Id.container, new MigrationFragment ());
            transaction.Commit ();
        }

        #endregion

        #region Event Listener

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
                Log.Info (Log.LOG_UI, "SetupActivity got ExecutionContextChanged event");
                ShowScreenForApplicationState ();
            }
        }

        #endregion

    }
}


