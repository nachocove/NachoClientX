//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Utils;
using Android.Content;
using NachoClient.AndroidClient;

namespace NachoPlatform
{
    public class NcUIRedirector : IPlatformUIRedirector
    {
        private static volatile NcUIRedirector instance;
        private static object syncRoot = new Object ();

        private NcUIRedirector ()
        {
        }

        public static NcUIRedirector Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new NcUIRedirector ();
                    }
                }
                return instance;
            }
        }

        public void GoBackToMainScreen ()
        {
            var context = MainApplication.Context;
            MainTabsActivity.ShowSetup (context);
        }

    }
}

