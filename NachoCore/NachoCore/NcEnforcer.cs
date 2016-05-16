//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore
{
    public class NcEnforcer
    {
        private static volatile NcEnforcer instance;
        private static object syncRoot = new Object();

        private NcEnforcer () {}
        private NcTimer DelayTimer;

        public static NcEnforcer Instance
        {
            get 
            {
                if (instance == null) 
                {
                    lock (syncRoot)
                    {
                        if (instance == null) 
                            instance = new NcEnforcer ();
                    }
                }
                return instance;
            }
        }

        public Xml.Provision.PolicyReqStatusCode Compliance (McAccount account)
        {
            // By design, we always say we are compliant.
            return Xml.Provision.PolicyReqStatusCode.Success_1;
        }

        private void WipeAccountAndRestart(Object state)
        {
            var account = (McAccount)state;
            // dont bother checking for ack from the server since as per spec : The client SHOULD NOT wait for or rely on any specific response from the server before proceeding with the remote wipe.
            Log.Info (Log.LOG_AS, "Remote Wipe Initiated");
            DelayTimer.Dispose ();
            DelayTimer = null;
            Action action = () => {
                InvokeOnUIThread.Instance.Invoke (delegate () {
                    NcAccountHandler.Instance.RemoveAccount (account.Id);
                    NcUIRedirector.Instance.GoBackToMainScreen();                        
                    Log.Info (Log.LOG_AS, "Remote Wipe Completed.");
                });
            };
            NcTask.Run (action, "RemoveAccount");
        }

        public bool Wipe (McAccount account, string url, string protoVersion)
        {
            Log.Info (Log.LOG_AS, "Remote Wipe Marked");
            // mark wipe in progress. This will go thru even if the app is stopped/restarted.
            NcModel.Instance.WriteRemovingAccountIdToFile (account.Id);
            DelayTimer = new NcTimer ("RemoteWipeTimer", WipeAccountAndRestart, account, 1000, 1000);
            return true;
        }
    }
}

