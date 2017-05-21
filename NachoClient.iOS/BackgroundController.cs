//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Threading;
using System.Collections.Generic;

using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class BackgroundController
    {
        public enum FetchCause { PerformFetch, RemoteNotification };
        private enum FetchState { None, Active, Finishing, Done };

        private bool IsFetching = false;
        private FetchCause Cause;
        private int FetchCount = 0;

        private FetchState BadgeCountState = FetchState.None;
        private FetchState BackEndState = FetchState.None;

        private Action<UIBackgroundFetchResult> CompletionHandler = null;
        private UIBackgroundFetchResult Result;

        /// <summary>
        /// The PerformFetch timer. This needs to be a Timer, not an NcTimer,
        /// because performFetchTimer needs to keep running after FinalShutdown()
        /// has been called to make sure the badge count update code completes in
        /// time. If it is an NcTimer, then it will get killed during FinalShutdown().
        /// </summary>
        private Timer performFetchTimer = null;

        // A list of all account ids that are waiting to be synced.
        private List<int> Accounts;
        // A list of all accounts ids that are waiting for push assist to set up
        private List<int> PushAccounts;
        // PushAssist is active only when the app is registered for remote notifications


        private class PerformFetchCountObject
        {
            public int count;
            public PerformFetchCountObject (int count) { this.count = count; }
        }

        private bool IsFetchComplete {
            get {
                return (0 == Accounts.Count);
            }
        }

        private bool IsPushAssistArmComplete {
            get {
                return !NotificationsHandler.HasRegisteredForRemoteNotifications || (0 == PushAccounts.Count);
            }
        }

        public BackgroundController ()
        {
        }

        protected void StartFetch (UIApplication application, Action<UIBackgroundFetchResult> completionHandler, FetchCause cause)
        {

            if (IsFetching) {
                if (Cause == FetchCause.RemoteNotification && cause == PerformFetch){
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

            if (8.0 > application.BackgroundTimeRemaining) {
                // Launching the app took up most of the perform fetch window.  There isn't enough
                // time left to run a full quick sync.
                Log.Warn (Log.LOG_LIFECYCLE, "Skipping quick sync {0} because only {1:n2} seconds are left.", cause, application.BackgroundTimeRemaining);
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
                NcBrain.StartService ();
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
            var performFetchTime = application.BackgroundTimeRemaining;
            Log.Info (Log.LOG_LIFECYCLE, "Starting PerformFetch timer: {0:n2} seconds of background time remaining.", performFetchTime);
            performFetchTimer = new Timer (((object state) => {
                InvokeOnUIThread.Instance.Invoke (() => {
                    var remaining = application.BackgroundTimeRemaining;
                    Log.Info (Log.LOG_LIFECYCLE, "PerformFetch timer: {0:n2} seconds remaining.", remaining);
                    if (((PerformFetchCountObject)state).count == FetchCount) {
                        if (10.0 >= remaining && FetchState.Active == BackEndState) {
                            Log.Info (Log.LOG_LIFECYCLE, "PerformFetch ran out of time. Shutting down the app.");
                            if (FetchState.Active == BadgeCountState) {
                                FinalQuickSyncBadgeNotifications ();
                            }
                            CompleteFetchAndShutdown ();
                        }
                        if (4.0 >= remaining) {
                            Log.Error (Log.LOG_LIFECYCLE, "PerformFetch didn't shut down in time. Calling the completion handler now.");
                            FinalizeFetch (Result);
                        }
                    } else if (null != performFetchTimer) {
                        Log.Info (Log.LOG_LIFECYCLE, "PerformFetch timer fired after perform fetch completed. Disabling the timer.");
                        try {
                            performFetchTimer.Change (Timeout.Infinite, Timeout.Infinite);
                        } catch (Exception ex) {
                            // Wrapper to protect against unknown C# timer stupidity.
                            Log.Error (Log.LOG_LIFECYCLE, "PerformFetch timer exception: {0}", ex);
                        }
                    }
                });
            }), new PerformFetchCountObject (FetchCount), TimeSpan.FromSeconds (5), TimeSpan.FromSeconds (1));
        }

        private void FinalQuickSyncBadgeNotifications ()
        {
            int savedPerformFetchCount = FetchCount;
            BadgeCountState = FetchState.Finishing;
            BadgeCountAndMessageNotifications (() => {
                // The BadgeCountAndMessageNotifications() might survive across a shutdown and complete when the next
                // PerformFetch is running.  Only finalize the PerformFetch if it is the same one
                // as when the call was started.
                if (FetchCount == savedPerformFetchCount) {
                    BadgeCountState = FetchState.Done;
                    if (FetchState.Done == BackEndState) {
                        FinalizeFetch (Result);
                        NcApplication.Instance.PlatformIndication = NcApplication.ExecutionContextEnum.Background;
                    }
                }
            });
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
            if (null != performFetchTimer) {
                performFetchTimer.Dispose ();
                performFetchTimer = null;
            }
            StopListeningForStatusEvents ();
            var handler = CompletionHandler;
            CompletionHandler = null;
            Cause = null;
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
                bool fetchWasComplete = IsFetchComplete;
                Accounts.Remove (accountId);
                Log.Info (Log.LOG_LIFECYCLE, "FetchStatusHandler:Info_SyncSucceeded account {0}. {1} accounts and {2} push assists remaining.", accountId, Accounts.Count, PushAccounts.Count);
                if (IsFetchComplete) {
                    // There will sometimes be duplicate Info_SyncSucceeded for an account.
                    // Only call BadgeCountAndMessageNotifications once.
                    if (!fetchWasComplete) {
                        FinalQuickSyncBadgeNotifications ();
                    }
                    if (IsPushAssistArmComplete) {
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
                if (IsFetchComplete && IsPushAssistArmComplete) {
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
                if (IsFetchComplete) {
                    FinalQuickSyncBadgeNotifications ();
                    if (IsPushAssistArmComplete) {
                        CompleteFetchAndShutdown ();
                    }
                }
                break;
            }
        }

        #endregion
    }
}
