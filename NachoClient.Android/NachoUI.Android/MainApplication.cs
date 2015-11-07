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

namespace NachoClient.AndroidClient
{
    [Application (AllowBackup = true, BackupAgent = typeof(NcBackupAgentHelper), RestoreAnyVersion = true)]
    public class MainApplication : Application
    {
        static MainApplication _instance;
        static bool checkForUpdates = true;
        public BackupManager BackupManager;

        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
            _instance = this;
            LifecycleSpy.SharedInstance.Init (this);
            BackupManager = new BackupManager (this);
        }

        public static MainApplication Instance {
            get {
                return _instance;
            }
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;
            StartService (new Intent (this, typeof(NotificationService)));
        }

        public static void Startup ()
        {
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

    }
}
