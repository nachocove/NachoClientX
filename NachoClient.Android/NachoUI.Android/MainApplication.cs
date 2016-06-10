using System;
using Android.App;
using Android.Runtime;

using NachoCore;
using NachoCore.Utils;

using System.Security.Cryptography.X509Certificates;
using NachoPlatform;
using Android.Content;
using System.IO;
using System.Threading;
#if !NO_HOCKEY_APP
using NachoClient.Build;
using System.Threading.Tasks;
#endif

namespace NachoClient.AndroidClient
{
    #if !DEBUG
    // DO NOT PUT THE [Application ...] tag here when running unit tests.
    [Application (AllowBackup = true, BackupAgent = typeof(NcBackupAgentHelper), RestoreAnyVersion = true)]
    #endif
    public class MainApplication : Application
    {
        /// <summary>
        /// Public for testing only.
        /// </summary>
        public static Application _instance;
        static bool checkForUpdates = true;
        static bool startupCalled = false;

        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
//           StrictMode.SetThreadPolicy(new StrictMode.ThreadPolicy.Builder()
//                .DetectAll()
//                .PenaltyLog()
//                .PenaltyDialog()
//                .Build());
//            StrictMode.SetVmPolicy(new StrictMode.VmPolicy.Builder().DetectAll()
//                .PenaltyLog()
//                .Build());

            _instance = this;
            Thread.CurrentThread.Priority = System.Threading.ThreadPriority.Highest;
            LifecycleSpy.SharedInstance.Init (this);
            MdmConfig.Instance.ExtractValues ();
            CopyAssetsToDocuments ();
            OneTimeStartup ("MainApplication");
        }

        public static Application Instance {
            get {
                return _instance;
            }
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
        }

        // Start everthing after we have some UI
        public static void OneTimeStartup (string caller)
        {
            if (startupCalled) {
                return;
            }
            startupCalled = true;

            Log.Info (Log.LOG_LIFECYCLE, "OneTimeStartup: {0}", caller);

            NcApplication.GuaranteeGregorianCalendar ();

            ServerCertificatePeek.Initialize ();

            // This creates the NcApplication object
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;

            NcApplication.Instance.StartBasalServices ();

            NcApplication.Instance.AppStartupTasks ();

            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                NcApplication.Instance.TelemetryService.Throttling = false;
                Calendars.Instance.DeviceCalendarChanged ();
            };

            MainApplication.Instance.StartService (new Intent (MainApplication.Instance, typeof(NotificationService)));

            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;

            Log.Info (Log.LOG_LIFECYCLE, "OneTimeStartup: OnStart finished");
        }

        public static void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
        }

        static bool CheckOnceForUpdates ()
        {
            if (BuildInfoHelper.IsAlpha || BuildInfoHelper.IsBeta) {
                var check = checkForUpdates;
                checkForUpdates = false;
                return check;
            } else {
                return false;
            }
        }

        void CopyAssetsToDocuments ()
        {
            var documentsPath = System.Environment.GetFolderPath (System.Environment.SpecialFolder.MyDocuments);
            string[] assets = { "nacho.html", "nacho.css", "nacho.js", "chat-email.html" };
            foreach (var assetName in assets) {
                var destinationPath = Path.Combine (documentsPath, assetName);
                // TODO: only copy if newer...how to check the modified time of an asset (don't think it's possible)
                using (var assetStream = Assets.Open (assetName)) {
                    using (var destinationStream = new FileStream (destinationPath, FileMode.Create)) {
                        assetStream.CopyTo (destinationStream);
                    }
                }
            }
        }

        #region HockeyApp

        public static void RegisterHockeyAppUpdateManager (Activity activity)
        {
            #if !NO_HOCKEY_APP
            if (BuildInfoHelper.IsDev) {
                return;
            }
            if (CheckOnceForUpdates ()) {
                updateRegistered = true;
                //Register to with the Update Manager
                HockeyApp.UpdateManager.Register (activity, BuildInfo.HockeyAppAppId, new MyCustomUpdateManagerListener (), true);
            }
            #endif
        }

        public static void UnregisterHockeyAppUpdateManager ()
        {
            #if !NO_HOCKEY_APP
            if (updateRegistered) {
                HockeyApp.UpdateManager.Unregister ();
                updateRegistered = false;
            }
            #endif
        }

        public static void SetupHockeyAppCrashManager (Activity activity)
        {
            #if !NO_HOCKEY_APP
            if (BuildInfoHelper.IsDev) {
                return;
            }
            if (IsHockeyInitialized) {
                return;
            }
            IsHockeyInitialized = true;

            var myListener = new MyCustomCrashManagerListener ();
            // Register the crash manager before Initializing the trace writer
            HockeyApp.CrashManager.Register (activity, BuildInfo.HockeyAppAppId, myListener); 

            // Initialize the Trace Writer
            HockeyApp.TraceWriter.Initialize (myListener);

            // Wire up Unhandled Expcetion handler from Android
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) => {
                // Use the trace writer to log exceptions so HockeyApp finds them
                myListener.LastTrace = args.Exception.ToString ();
                HockeyApp.TraceWriter.WriteTrace (args.Exception);
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
            #endif
        }

        #if !NO_HOCKEY_APP

        static bool updateRegistered = false;

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
                    if (!string.IsNullOrEmpty (LastTrace)) {
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

            public UnCaughtExceptionHandler (MyCustomCrashManagerListener theListener)
            {
                CrashListener = theListener;
            }

            public void UncaughtException (Java.Lang.Thread thread, Java.Lang.Throwable ex)
            {
                CrashListener.LastTrace = ex.GetStackTrace ().ToString ();
                HockeyApp.TraceWriter.WriteTrace (ex);
            }
        }

        [Activity]
        public class NcHAUpdateActivity : HockeyApp.UpdateActivity
        {
        }

        static bool IsHockeyInitialized;

        #endif

        #endregion
    }
}
