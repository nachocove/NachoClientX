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

        object BackgroundTimerLock = new object ();
        NcTimer GoToBackgroundTimer;

        void Application.IActivityLifecycleCallbacks.OnActivityPaused (Activity activity)
        {
            lock (BackgroundTimerLock) {
                if (null == GoToBackgroundTimer) {
                    GoToBackgroundTimer = new NcTimer ("LifecycleSpy:GoToBackgroundTimer", (state) => {
                        lock (BackgroundTimerLock) {
                            if (null != GoToBackgroundTimer) {
                                Log.Info (Log.LOG_LIFECYCLE, "LifecycleSpy:GoToBackgroundTimer called.");
                                GoToBackgroundTimer.Dispose ();
                                GoToBackgroundTimer = null;
                                isForeground = false;
                                NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
                                LoginHelpers.SetBackgroundTime (DateTime.UtcNow);
                                NcModel.Instance.CleanupOldDbConnections (TimeSpan.FromMinutes (10), 20);
                                Log.Info (Log.LOG_LIFECYCLE, "LifecycleSpy:GoToBackgroundTimer exited.");
                            }
                        }
                    }, null, TimeSpan.FromSeconds (5), TimeSpan.Zero);
                }
            }
        }

        void Application.IActivityLifecycleCallbacks.OnActivityResumed (Activity activity)
        {
            lock (BackgroundTimerLock) {
                if (null != GoToBackgroundTimer) {
                    GoToBackgroundTimer.Dispose ();
                    GoToBackgroundTimer = null;
                }
                isForeground = true;
                if (NcApplication.ExecutionContextEnum.Foreground != NcApplication.Instance.PlatformIndication) {
                    NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Foreground;
                    NotificationService.OnForeground ();
                }
            }
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

