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
            var appDelegate = (NachoClient.iOS.AppDelegate)UIApplication.SharedApplication.Delegate;
            var storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var vc = storyboard.InstantiateInitialViewController ();
            Log.Info (Log.LOG_UI, "RemoveAccount: back to startup navigation controller {0}", vc);
            appDelegate.Window.RootViewController = (UIViewController)vc;
        }

    }
}

