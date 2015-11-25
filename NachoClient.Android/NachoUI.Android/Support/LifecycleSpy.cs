//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using NachoCore.Utils;
using Android.OS;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.AndroidClient
{
    public class LifecycleSpy : Java.Lang.Object, Application.IActivityLifecycleCallbacks
    {
        private static LifecycleSpy instance;
        private static object syncRoot = new Object ();

        private bool isForeground;
        private bool isPaused;

        public void Init (Application app)
        {
            app.RegisterActivityLifecycleCallbacks (LifecycleSpy.SharedInstance);
        }

        public static LifecycleSpy SharedInstance {
            
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new LifecycleSpy ();
                        }
                    }
                }
                return instance; 
            }
        }

        public bool IsForeground ()
        {
            return isForeground;
        }

        public bool IsBackground ()
        {
            return !isForeground;
        }

        void  Application.IActivityLifecycleCallbacks.OnActivityCreated (Activity activity, Bundle savedInstanceState)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityDestroyed (Activity activity)
        {
        }

        // Note that transitions between activities will always
        // pause/resume, so blindly sending a notification when
        // there's a change is not good. If a notification must
        // be sent, 1) delay it, and 2) use IsPaused to prevent
        // redundant notifications.
        void Application.IActivityLifecycleCallbacks.OnActivityPaused (Activity activity)
        {
            isPaused = true;
            isForeground = false;
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            McMutables.Set (McAccount.GetDeviceAccount ().Id, "Android", "BackgroundTime", DateTime.UtcNow.ToString ());
        }

        void Application.IActivityLifecycleCallbacks.OnActivityResumed (Activity activity)
        {
            isPaused = true;
            isForeground = true;
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;
            NotificationService.OnForeground ();
        }

        void Application.IActivityLifecycleCallbacks.OnActivitySaveInstanceState (Activity activity, Bundle outState)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityStarted (Activity activity)
        {
        }

        void Application.IActivityLifecycleCallbacks.OnActivityStopped (Activity activity)
        {
        }



    }
}

