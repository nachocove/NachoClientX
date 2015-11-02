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
            // go back to main screen
            var context = MainApplication.Context;
            var intent = new Intent (context, typeof(MainActivity));
            intent.AddFlags (ActivityFlags.ClearTop | ActivityFlags.NewTask);
            context.StartActivity (intent);

        }

    }
}

