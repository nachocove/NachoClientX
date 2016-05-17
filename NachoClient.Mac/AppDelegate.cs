//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using AppKit;
using Foundation;
using NachoCore;
using NachoCore.Utils;
using NachoPlatform;
using NachoCore.Model;
using System.Security.Cryptography.X509Certificates;

namespace NachoClient.Mac
{
    [Register ("AppDelegate")]
    public class AppDelegate : NSApplicationDelegate
    {
        public AppDelegate ()
        {
        }

        public override void DidFinishLaunching (NSNotification notification)
        {
            NcApplication.GuaranteeGregorianCalendar ();
            // TODO: StartCrashReporting
            ServerCertificatePeek.Initialize ();
            // TODO: Start UI Monitoring (need to have it first)

            NcApplication.Instance.CredReqCallback = CredReqCallback;
            NcApplication.Instance.ServConfReqCallback = ServConfReqCallback;
            NcApplication.Instance.CertAskReqCallback = CertAskReqCallback;

            MdmConfig.Instance.ExtractValues ();

            CopyResourcesToDocuments ();

            // TODO: establish crash folder

            NcApplication.Instance.ContinueRemoveAccountIfNeeded ();

            // TODO: do we need this?
            // NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;

            NcApplication.Instance.StartBasalServices ();
            NcApplication.Instance.AppStartupTasks ();

            // TODO: register for remote notifications


            // TODO: Do we need this?
            NcApplication.Instance.Class4LateShowEvent += (object sender, EventArgs e) => {
                Telemetry.Instance.Throttling = false;
            };
        }

        public override void WillTerminate (NSNotification notification)
        {
            // Insert code here to tear down your application
        }
            
        public void CopyResourcesToDocuments ()
        {
            return;
            // FIXME: GetFolderPath returns the user's home folder (e.g., /Users/oshaw), totally wrong for us 
            var documentsPath = Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments);
            string[] resources = { "nacho.html", "nacho.css", "nacho.js", "chat-email.html" };
            foreach (var resourceName in resources) {
                var resourcePath = NSBundle.MainBundle.PathForResource (resourceName, null);
                var destinationPath = Path.Combine (documentsPath, resourceName);
                if (!File.Exists (destinationPath) || File.GetLastWriteTime (destinationPath) < File.GetLastWriteTime (resourcePath)) {
                    if (File.Exists (destinationPath)) {
                        File.Delete (destinationPath);
                    }
                    File.Copy (resourcePath, destinationPath);
                    // FIXME: this is a noop on mac
                    NcFileHandler.Instance.MarkFileForSkipBackup (destinationPath);
                }
            }
        }

        public void CredReqCallback (int accountId)
        {
            Log.Info (Log.LOG_UI, "CredReqCallback Called for account: {0}", accountId);
            LoginHelpers.UserInterventionStateChanged (accountId);
        }

        public void ServConfReqCallback (int accountId, McAccount.AccountCapabilityEnum capabilities, object arg = null)
        {
            Log.Info (Log.LOG_UI, "ServConfReqCallback Called for account: {0} with arg {1}", accountId, arg);
            LoginHelpers.UserInterventionStateChanged (accountId);
        }

        public void CertAskReqCallback (int accountId, X509Certificate2 certificate)
        {
            Log.Info (Log.LOG_UI, "CertAskReqCallback Called for account: {0}", accountId);
            LoginHelpers.UserInterventionStateChanged (accountId);
        }
    }
}

