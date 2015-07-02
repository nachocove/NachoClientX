using System;
using Android.App;
using Android.Runtime;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using System.Security.Cryptography.X509Certificates;

namespace NachoClient.AndroidClient
{
    [Application]
    public class MainApplication : Application
    {
        public MainApplication (IntPtr javaReference, JniHandleOwnership transfer) : base (javaReference, transfer)
        {
        }

        public override void OnCreate ()
        {
            base.OnCreate ();
        }

        public static void Startup()
        {
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;
        }

        public static void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
        }
    }
}