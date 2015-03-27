//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class UIRedirector : IPlatformUIRedirector
    {
        private static volatile UIRedirector instance;
        private static object syncRoot = new Object ();

        private UIRedirector ()
        {
        }

        public static UIRedirector Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null)
                            instance = new UIRedirector ();
                    }
                }
                return instance;
            }
        }

        public void GoBackToMainScreen ()
        {
            // go back to main screen
            // TODO: implement
        }

    }
}

