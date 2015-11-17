using System;
using Android.App;
using Android.Runtime;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using System.Security.Cryptography.X509Certificates;
using NachoPlatform;
using Android.App.Backup;
using Android.Content;
using System.IO;

namespace NachoClient.AndroidClient
{
    [Application (AllowBackup = true, BackupAgent = typeof(NcBackupAgentHelper), RestoreAnyVersion = true)]
    public class MainApplication : Application
    {
        static MainApplication _instance;
        static bool checkForUpdates = true;
        static bool startupCalled = false;
        public BackupManager BackupManager;

        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
            _instance = this;
            LifecycleSpy.SharedInstance.Init (this);
            BackupManager = new BackupManager (this);
            CopyAssetsToDocuments ();
        }

        public static MainApplication Instance {
            get {
                return _instance;
            }
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
        }

        // Start everthing after we have some UI
        public static void OneTimeStartup ()
        {

            if (startupCalled) {
                return;
            }
            startupCalled = true;

            // This creates the NcApplication object
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;

            Log.Info (Log.LOG_LIFECYCLE, "MainActivity: StartBasalServices");
            NcApplication.Instance.StartBasalServices ();

            Log.Info (Log.LOG_LIFECYCLE, "MainActivity: AppStartupTasks");
            NcApplication.Instance.AppStartupTasks ();

            Log.Info (Log.LOG_LIFECYCLE, "MainActivity: OnStart finished");

            MainApplication.Instance.StartService(new Intent(MainApplication.Instance, typeof(NotificationService)));

            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;
        }

        public static void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
        }

        public static bool CheckOnceForUpdates ()
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
            string[] assets = { "nacho.html", "nacho.css", "nacho.js" };
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

    }
}
