using System;
using Android.App;
using Android.Runtime;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using System.Security.Cryptography.X509Certificates;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    [Application]
    public class MainApplication : Application
    {
        static MainApplication _instance;
        static bool checkForUpdates = true;

        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
            _instance = this;
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
        }

        public static void Startup ()
        {
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;
        }

        public static void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
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
