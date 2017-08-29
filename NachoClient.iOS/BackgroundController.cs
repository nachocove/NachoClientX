//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using NachoCore.Brain;

namespace NachoClient.iOS
{
    public class BackgroundController
    {

        public NotificationsHandler NotificationsHandler;

        public enum FetchCause { None, PerformFetch, RemoteNotification };
        private enum FetchState { None, Active, Finishing, Done };

        public bool IsFetching { get; private set; } = false;
        private FetchCause Cause = FetchCause.None;
        private int FetchCount = 0;

        private FetchState BadgeCountState = FetchState.None;
        private FetchState BackEndState = FetchState.None;

        private Action<UIBackgroundFetchResult> CompletionHandler = null;
        private UIBackgroundFetchResult Result;

        // used to ensure that a race condition doesn't let the ShutdownTimer stop things after re-activation.
        private int ShutdownCounter = 0;
        private bool FinalShutdownHasHappened = false;

        // Don't use NcTimer here - use the raw timer to avoid any future chicken-egg issues.
#pragma warning disable 414
        private Timer ShutdownTimer = null;
#pragma warning restore 414

        private nint BackgroundTaskId = -1;

        /// <summary>
        /// The PerformFetch timer. This needs to be a Timer, not an NcTimer,
        /// because FetchTimer needs to keep running after FinalShutdown()
        /// has been called to make sure the badge count update code completes in
        /// time. If it is an NcTimer, then it will get killed during FinalShutdown().
        /// </summary>
        private Timer FetchTimer = null;

        // A list of all account ids that are waiting to be synced.
        private List<int> Accounts;
        // A list of all accounts ids that are waiting for push assist to set up
        private List<int> PushAccounts;
        // PushAssist is active only when the app is registered for remote notifications

        public string FetchCauseString {
            get {
                switch (Cause) {
                case FetchCause.None:
                    return "BG";
                case FetchCause.PerformFetch:
                    return "PF";
                case FetchCause.RemoteNotification:
                    return "RN";
                default:
                    throw new NcAssert.NachoDefaultCaseFailure ("");
                }
            }
        }

        private class FetchCountObject
        {
            public int count;
            public FetchCountObject (int count) { this.count = count; }
        }

        private bool HasFetchedAllAccounts {
            get {
                return Accounts.Count == 0;
            }
        }

        private bool HasArmedAllPushAccounts {
            get {
                return PushAccounts.Count == 0;
            }
        }

        public BackgroundController ()
        {
        }

        public void BecomeActive ()
        {
            if (IsFetching) {
                CompleteFetch ();
            }

            if (BackgroundTaskId >= 0) {
                UIApplication.SharedApplication.EndBackgroundTask (BackgroundTaskId);
            }
            BackgroundTaskId = UIApplication.SharedApplication.BeginBackgroundTask (() => {
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback time remaining: {0:n2}", UIApplication.SharedApplication.BackgroundTimeRemaining);
                FinalShutdown (null);
                Log.Info (Log.LOG_LIFECYCLE, "BeginBackgroundTask: Callback exit");
            });
        }

        public void EnterForeground ()
        {
            Interlocked.Increment (ref ShutdownCounter);
            if (null != ShutdownTimer) {
                ShutdownTimer.Dispose ();
                ShutdownTimer = null;
            }
            if (IsFetching) {
                CompleteFetch ();
            }
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
            }
        }

        public void EnterBackground ()
        {
            var timeRemaining = UIApplication.SharedApplication.BackgroundTimeRemaining;
            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: time remaining: {0:n2}", timeRemaining);
            if (25.0 > timeRemaining) {
                FinalShutdown (null);
            } else {
                var didShutdown = false;
                TimeSpan initialTimerDelay = TimeSpan.FromSeconds (1);
                if (35 < timeRemaining && timeRemaining < 1000) {
                    initialTimerDelay = TimeSpan.FromSeconds (timeRemaining - 30);
                }
                ShutdownTimer = new Timer ((opaque) => {
                    InvokeOnUIThread.Instance.Invoke (delegate {
                        // check remaining background time. If too little, shut us down.
                        // iOS caveat: BackgroundTimeRemaining can be MAX_DOUBLE early on.
                        // It also seems to return to MAX_DOUBLE value after we call EndBackgroundTask().
                        var remaining = UIApplication.SharedApplication.BackgroundTimeRemaining;
                        Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer: time remaining: {0:n2}", remaining);
                        if (!didShutdown && 25.0 > remaining) {
                            Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer: Background time is running low. Shutting down the app.");
                            try {
                                // This seems to work, but we do get some extra callbacks after Change().
                                ShutdownTimer.Change (Timeout.Infinite, Timeout.Infinite);
                            } catch (Exception ex) {
                                // Wrapper to protect against unknown C# timer stupidity.
                                Log.Error (Log.LOG_LIFECYCLE, "DidEnterBackground:ShutdownTimer exception: {0}", ex);
                            }
                            didShutdown = true;
                            FinalShutdown (opaque);
                        }
                    });
                }, ShutdownCounter, initialTimerDelay, TimeSpan.FromSeconds (1));
                Log.Info (Log.LOG_LIFECYCLE, "DidEnterBackground: ShutdownTimer");
            }
        }

        public void StartFetch (Action<UIBackgroundFetchResult> completionHandler, FetchCause cause)
        {

            if (IsFetching) {
                if (Cause == FetchCause.RemoteNotification && cause == FetchCause.PerformFetch) {
                    // iOS often starts a PerformFetch while a remote notification is still in progress.
                    // This is not desirable, but it is normal.
                    Log.Info (Log.LOG_LIFECYCLE, "RemoteNotification was immediately followed by PerformFetch.");
                } else {
                    Log.Warn (Log.LOG_LIFECYCLE, "PerformFetch ({0}) was called while a previous PerformFetch ({1}) was still running.", cause, Cause);
                }
                CompleteFetch ();
            }

            Cause = cause;
            Result = UIBackgroundFetchResult.NoData;

            // Crashes while launching in the background shouldn't increment the safe mode counter.
            // (It would be nice if background launches could simply not increment the counter rather
            // than clear it completely, but that is not worth the effort.)
            NcApplication.Instance.UnmarkStartup ();

            CompletionHandler = completionHandler;
            // check to see if migrations need to run. If so, we shouldn't let the PerformFetch proceed!
            NcMigration.Setup ();
            if (NcMigration.WillStartService ()) {
                Log.Error (Log.LOG_SYS, "PerformFetch called while migrations still need to run.");
                CompleteFetch ();
                return;
            }

            if (8.0 > UIApplication.SharedApplication.BackgroundTimeRemaining) {
                // Launching the app took up most of the perform fetch window.  There isn't enough
                // time left to run a full quick sync.
                Log.Warn (Log.LOG_LIFECYCLE, "Skipping quick sync {0} because only {1:n2} seconds are left.", cause, UIApplication.SharedApplication.BackgroundTimeRemaining);
                CompleteFetch ();
                return;
            }

            Accounts = McAccount.GetAllConfiguredNormalAccountIds ();
            if (NotificationsHandler.HasRegisteredForRemoteNotifications) {
                // Info_PushAssistArmed event will never be sent for accounts that have fast
                // notification disabled, so those accounts can't be in the list of accounts
                // to wait for.
                PushAccounts = McAccount.GetAllConfiguredNormalAccountIds ().Where (accountId => {
                    var account = McAccount.QueryById<McAccount> (accountId);
                    return null != account && account.FastNotificationEnabled;
                }).ToList ();
            } else {
                PushAccounts = new List<int> ();
            }
            // Need to set ExecutionContext before Start of BE so that strategy can see it.
            NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.QuickSync;
            NcApplication.Instance.UnmarkStartup ();
            if (FinalShutdownHasHappened) {
                ReverseFinalShutdown ();
                BackEnd.Instance.Start ();
                if (NcBrain.ENABLED){
                    NcBrain.StartService ();
                }
                NcApplicationMonitor.Instance.Start (1, 60);
            } else {
                NcCommStatus.Instance.Reset ("StartFetch");
            }
            StartListeningForStatusEvents ();

            ++FetchCount;
            IsFetching = true;
            BadgeCountState = 0 == Accounts.Count ? FetchState.Done : FetchState.Active;
            BackEndState = FetchState.Active;

            // iOS only allows a limited amount of time to fetch data in the background.
            // Set a timer to force everything to shut down before iOS kills the app.
            // The timer should fire once per second starting when the app has about
            // ten seconds left.
            Log.Info (Log.LOG_LIFECYCLE, "Starting PerformFetch timer: {0:n2} seconds of background time remaining.", UIApplication.SharedApplication.BackgroundTimeRemaining);
            FetchTimer = new Timer (((object state) => {
                InvokeOnUIThread.Instance.Invoke (() => {
                    var remaining = UIApplication.SharedApplication.BackgroundTimeRemaining;
                    Log.Info (Log.LOG_LIFECYCLE, "PerformFetch timer: {0:n2} seconds remaining.", remaining);
                    if (((FetchCountObject)state).count == FetchCount) {
                        if (10.0 >= remaining && FetchState.Active == BackEndState) {
                            Log.Info (Log.LOG_LIFECYCLE, "PerformFetch ran out of time. Shutting down the app.");
                            if (FetchState.Active == BadgeCountState) {
                                NotifyAndCompleteFetch ();
                            }
                            CompleteFetchAndShutdown ();
                        }
                        if (4.0 >= remaining) {
                            Log.Error (Log.LOG_LIFECYCLE, "PerformFetch didn't shut down in time. Calling the completion handler now.");
                            CompleteFetch ();
                        }
                    } else if (null != FetchTimer) {
                        Log.Info (Log.LOG_LIFECYCLE, "PerformFetch timer fired after perform fetch completed. Disabling the timer.");
                        try {
                            FetchTimer.Change (Timeout.Infinite, Timeout.Infinite);
                        } catch (Exception ex) {
                            // Wrapper to protect against unknown C# timer stupidity.
                            Log.Error (Log.LOG_LIFECYCLE, "PerformFetch timer exception: {0}", ex);
                        }
                    }
                });
            }), new FetchCountObject (FetchCount), TimeSpan.FromSeconds (5), TimeSpan.FromSeconds (1));
        }

        private void NotifyAndCompleteFetch ()
        {
            int fetchCount = FetchCount;
            BadgeCountState = FetchState.Finishing;
            NotificationsHandler.BadgeCountAndMessageNotifications (() => {
                // The BadgeCountAndMessageNotifications() might survive across a shutdown and complete when the next
                // PerformFetch is running.  Only finalize the PerformFetch if it is the same one
                // as when the call was started.
                if (FetchCount == fetchCount) {
                    BadgeCountState = FetchState.Done;
                    if (FetchState.Done == BackEndState) {
                        CompleteFetch ();
                        NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
                    }
                }
            });
        }

        public void FinalShutdown (object opaque)
        {
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Called");
            if (null != opaque && (int)opaque != ShutdownCounter) {
                Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Stale");
                return;
            }
            NcApplication.Instance.StopBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: StopBasalServices complete");
            if (BackgroundTaskId >= 0) {
                UIApplication.SharedApplication.EndBackgroundTask (BackgroundTaskId);
                BackgroundTaskId = -1;
            }
            FinalShutdownHasHappened = true;
            Log.Info (Log.LOG_PUSH, "[PA] finalshutdown: client_id={0}", NcApplication.Instance.ClientId);
            Log.Info (Log.LOG_LIFECYCLE, "FinalShutdown: Exit");
        }

        private void ReverseFinalShutdown ()
        {
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Called");
            NcApplication.Instance.StartBasalServices ();
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: StartBasalServices complete");
            FinalShutdownHasHappened = false;
            NcTask.Run (() => NcModel.Instance.CleanupOldDbConnections (TimeSpan.FromMinutes (10), 20), "ReverseFinalShutdownCleanupOldDbConnections");
            Log.Info (Log.LOG_LIFECYCLE, "ReverseFinalShutdown: Exit");
        }

        #region Ending a Fetch

        private void CompleteFetch ()
        {
            FinalizeFetch (Result);
        }

        private void CompleteFetchAndShutdown ()
        {
            StopListeningForStatusEvents ();
            BackEndState = FetchState.Finishing;
            FinalShutdown (null);
            BackEndState = FetchState.Done;
            if (FetchState.Done == BadgeCountState) {
                FinalizeFetch (Result);
                NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
            }
        }

        private void FinalizeFetch (UIBackgroundFetchResult result)
        {
            Log.Info (Log.LOG_LIFECYCLE, "Finalize PerformFetch ({0})", result.ToString ());
            if (null != FetchTimer) {
                FetchTimer.Dispose ();
                FetchTimer = null;
            }
            StopListeningForStatusEvents ();
            var handler = CompletionHandler;
            CompletionHandler = null;
            Cause = FetchCause.None;
            ++FetchCount;
            IsFetching = false;
            BadgeCountState = FetchState.None;
            BackEndState = FetchState.None;
            handler (result);
        }

        #endregion

        #region Status Events

        private bool IsListeningForStatusEvents = false;

        private void StartListeningForStatusEvents ()
        {
            NcApplication.Instance.StatusIndEvent += StatusIndEventHandler;
            IsListeningForStatusEvents = true;
        }

        private void StopListeningForStatusEvents ()
        {
            if (IsListeningForStatusEvents) {
                NcApplication.Instance.StatusIndEvent -= StatusIndEventHandler;
                IsListeningForStatusEvents = false;
            }
        }

        private void StatusIndEventHandler (object sender, EventArgs e)
        {
            if (!IsFetching) {
                // The delivery of the event was delayed.  PerformFetch is no longer active.
                return;
            }
            StatusIndEventArgs statusEvent = (StatusIndEventArgs)e;
            int accountId = (null != statusEvent.Account) ? statusEvent.Account.Id : -1;
            switch (statusEvent.Status.SubKind) {
            case NcResult.SubKindEnum.Info_NewUnreadEmailMessageInInbox:
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_NewUnreadEmailMessageInInbox account {0}", accountId);
                Result = UIBackgroundFetchResult.NewData;
                break;

            case NcResult.SubKindEnum.Info_SyncSucceeded:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded for unspecified account {0}", accountId);
                }
                bool fetchWasComplete = HasFetchedAllAccounts;
                Accounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded account {0}. {1} accounts and {2} push assists remaining.", accountId, Accounts.Count, PushAccounts.Count);
                if (HasFetchedAllAccounts) {
                    // There will sometimes be duplicate Info_SyncSucceeded for an account.
                    // Only call BadgeCountAndMessageNotifications once.
                    if (!fetchWasComplete) {
                        NotifyAndCompleteFetch ();
                    }
                    if (HasArmedAllPushAccounts) {
                        CompleteFetchAndShutdown ();
                    }
                }
                break;

            case NcResult.SubKindEnum.Info_PushAssistArmed:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_PushAssistArmed for unspecified account {0}", accountId);
                }
                PushAccounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_PushAssistArmed account {0}. {1} accounts and {2} push assists remaining.", accountId, Accounts.Count, PushAccounts.Count);
                if (HasFetchedAllAccounts && HasArmedAllPushAccounts) {
                    CompleteFetchAndShutdown ();
                }
                break;

            case NcResult.SubKindEnum.Error_SyncFailed:
                if (0 >= accountId) {
                    Log.Error (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed for unspecified account {0}", accountId);
                }
                Accounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Error_SyncFailed account {0}. {1} accounts and {2} push assists remaining.", accountId, Accounts.Count, PushAccounts.Count);
                // If one account found some new messages and a different account failed to sync,
                // return a successful result.
                if (UIBackgroundFetchResult.NoData == Result) {
                    Result = UIBackgroundFetchResult.Failed;
                }
                if (HasFetchedAllAccounts) {
                    NotifyAndCompleteFetch ();
                    if (HasArmedAllPushAccounts) {
                        CompleteFetchAndShutdown ();
                    }
                }
                break;
            }
        }

        #endregion
    }
}
