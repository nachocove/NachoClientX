//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;

namespace NachoCore.Utils
{
    public static class NcAbate
    {
        private class BackEndAbateImplementation : IDisposable
        {
            public BackEndAbateImplementation ()
            {
                lock (backEndLock) {
                    ++backEndCount;
                    backEndSignal.Reset ();
                }
            }

            public void Dispose ()
            {
                lock (backEndLock) {
                    --backEndCount;
                    if (0 == backEndCount) {
                        backEndSignal.Set ();
                    }
                }
            }
        }

        private class UIAbateImplementation : IDisposable
        {
            public UIAbateImplementation ()
            {
                Monitor.Enter (uiLock);
                isUiLocked = true;
                NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = true;
            }

            public void Dispose ()
            {
                NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = false;
                isUiLocked = false;
                Monitor.Exit (uiLock);
            }
        }

        private class UITimedAbateImplementation : IDisposable
        {
            bool disposed = false;
            Timer disposeTimer;

            void TimerCallback (object state)
            {
                if (!disposed) {
                    NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                        Dispose ();
                    });
                }
            }

            public UITimedAbateImplementation (TimeSpan maxDuration)
            {
                // Use C# Timer class, not NcTimer, because the timer must not be canceled if the app starts to shut down.
                disposeTimer = new Timer (TimerCallback, null, maxDuration, TimeSpan.Zero);
                Monitor.Enter (uiLock);
                isUiLocked = true;
                NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = true;
            }

            public void Dispose ()
            {
                if (!disposed) {
                    disposed = true;
                    disposeTimer.Dispose ();
                    NachoCore.Model.NcModel.Instance.RateLimiter.Enabled = false;
                    isUiLocked = false;
                    Monitor.Exit (uiLock);
                }
            }
        }

        private static object uiLock = new object ();
        private static bool isUiLocked = false;
        private static object backEndLock = new object ();
        private static int backEndCount = 0;
        private static ManualResetEventSlim backEndSignal = new ManualResetEventSlim (true, 0);

        /// <summary>
        /// Request that the back end stop working for a while.  This request can only be made from the UI thread.
        /// The abatement request ends when the returned object is disposed.  The object must be disposed in a
        /// timely manner.  To guarantee timely disposal, this method should always be used with a <code>using</code>
        /// statement, such as:
        /// <code>using (NcAbate.UIAbatement ()) { /* important work */ }</code>
        /// </summary>
        public static IDisposable UIAbatement ()
        {
            NcAssert.AreEqual (NcApplication.Instance.UiThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId,
                "NcAbate.UIAbatement() can only be called on the UI thread.");
            return new UIAbateImplementation ();
        }

        /// <summary>
        /// Request that the back end stop working for a while, with a maximum time for the request.  This request can
        /// only be made from the UI thread.  The abatement request ends when the returned object is disposed or the
        /// given amount of time has expired.  Dispose() may be safely called on the returned object even if the time
        /// has expired.
        /// </summary>
        /// <param name="maxDuration">The time after which the abatement will expire if it hasn't already been completed.</param>
        public static IDisposable UITimedAbatement (TimeSpan maxDuration)
        {
            NcAssert.AreEqual (NcApplication.Instance.UiThreadId, System.Threading.Thread.CurrentThread.ManagedThreadId,
                "NcAbate.UITimedAbatement() can only be called on the UI thread.");
            return new UITimedAbateImplementation (maxDuration);
        }

        /// <summary>
        /// Request that the back end stop working for a while.  This method should not be called from the UI thread,
        /// but should be used when high-priority back end work is happening.  The abatement request ends when the
        /// returned object is disposed.  The object must be disposed in a timely manner.  To guarantee timely disposal,
        /// this method should always be used with a <code>using</code> statement, such as:
        /// <code>using (NcAbate.BackEndAbatement ()) { /* important work */ }</code>
        /// </summary>
        public static IDisposable BackEndAbatement ()
        {
            return new BackEndAbateImplementation ();
        }

        /// <summary>
        /// Pause execution of the current thread for as long as an abatement request is in effect.  The abatement
        /// request could come from either the UI thread or the back end.  If there is no current abatement
        /// request, then this method returns immediately.  This method must never be called when the current thread
        /// holds a lock or other resource that could be used by the UI thread or other thread that should not block.
        /// </summary>
        public static void PauseWhileAbated ()
        {
            backEndSignal.Wait ();
            lock (uiLock) { }
        }

        /// <summary>
        /// Is there a current abatement request.
        /// </summary>
        public static bool IsAbated ()
        {
            return isUiLocked || !backEndSignal.IsSet;
        }
    }
}

