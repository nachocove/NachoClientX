
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public interface WaitingFragmentDelegate
    {
        void WaitingFinished (McAccount account);
    }

    public class WaitingFragment : Fragment, ILoginEvents
    {
        private const string ACCOUNT_ID_KEY = "accountId";

        TextView statusLabel;
        ProgressBar activityIndicatorView;

        private class AccountSyncingStatusMessage
        {
            public string Title;
            public string Details;
            public bool IsWorking;

            public AccountSyncingStatusMessage (string title, string details, bool isWorking)
            {
                Title = title;
                Details = details;
                IsWorking = isWorking;
            }
        }

        private McAccount account;
        private bool mIsVisible;
        private bool DismissOnVisible;
        private NcTimer DismissTimer;

        private static AccountSyncingStatusMessage SyncingMessage = new AccountSyncingStatusMessage ("Syncing...", "Syncing your inbox...", true);
        private static AccountSyncingStatusMessage SuccessMessage = new AccountSyncingStatusMessage ("Account Created", "Your account is ready!", false);
        private static AccountSyncingStatusMessage ErrorMessage = new AccountSyncingStatusMessage ("Account Created", "Sorry, we could not fully sync your inbox.  Please see Settings for more information", false);
        private static AccountSyncingStatusMessage TooManyDevicesMessage = new AccountSyncingStatusMessage ("Cannot Create Account", "You are already using the maximum number of devices for this account.  Please contact your system administrator.", false);
        private static AccountSyncingStatusMessage NetworkMessage = new AccountSyncingStatusMessage ("Account Created", "Syncing will complete when network connectivity is restored", false);

        private AccountSyncingStatusMessage Message = SyncingMessage;

        public static WaitingFragment newInstance (McAccount account)
        {
            var fragment = new WaitingFragment ();
            fragment.account = account;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                account = McAccount.QueryById<McAccount> (savedInstanceState.GetInt (ACCOUNT_ID_KEY));
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.WaitingFragment, container, false);
            activityIndicatorView = view.FindViewById<ProgressBar> (Resource.Id.spinner);
            statusLabel = view.FindViewById<TextView> (Resource.Id.textview);

            LoginEvents.Owner = this;
            LoginEvents.AccountId = account.Id;
            LoginEvents.CheckBackendState ();

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();

            mIsVisible = true;
            Update ();

            mIsVisible = true;
            if (Message.IsWorking) {
                activityIndicatorView.Visibility = ViewStates.Visible;
            }
            if (DismissOnVisible) {
                DismissAfterDelay ();
            }

        }

        public override void OnPause ()
        {
            base.OnPause ();
            mIsVisible = false;
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (ACCOUNT_ID_KEY, account.Id);
        }

        void Update ()
        {
            statusLabel.Text = Message.Details;
            if (mIsVisible) {
                if (Message.IsWorking) {
                    activityIndicatorView.Visibility = ViewStates.Visible;
                } else {
                    activityIndicatorView.Visibility = ViewStates.Invisible;
                }
            }
//                if (Message.IsWorking) {
//                    NavigationItem.RightBarButtonItem = skipButton;
//                } else {
//                    NavigationItem.RightBarButtonItem = null;
//                }
        }

        private void CompleteAccount ()
        {
            if (account != null) {
                account = account.UpdateWithOCApply<McAccount> ((record) => {
                    var _account = record as McAccount;
                    _account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
                    return true;
                });
                NcApplication.Instance.InvokeStatusIndEventInfo (null, NcResult.SubKindEnum.Info_AccountSetChanged);
            }
        }

        void DismissAfterDelay ()
        {
            Log.Info (Log.LOG_UI, "AccountSyncingViewController starting dismiss timer");
            DismissTimer = new NcTimer ("AccountSyncViewControllerDismiss", (state) => {
                InvokeOnUIThread.Instance.Invoke (() => {
                    Log.Info (Log.LOG_UI, "AccountSyncingViewController dismissing by calling delegate");
                    if (DismissTimer != null) {
                        DismissTimer.Dispose ();
                        DismissTimer = null;
                    }
                    if (null != this.Activity) {
                        // The activity can be null if there was a configuration change while waiting for
                        // the timer to fire.
                        var parent = (WaitingFragmentDelegate)Activity;
                        parent.WaitingFinished (account);
                    }
                });
            }, null, TimeSpan.FromSeconds (2), TimeSpan.Zero);
        }

        #region Backend Events

        public void CredReq (int accountId)
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (ErrorMessage);
        }

        public void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg)
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (ErrorMessage);
        }

        public void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (ErrorMessage);
        }

        public void NetworkDown ()
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (NetworkMessage);
        }

        public void PostAutoDPreInboxSync (int accountId)
        {
            // we don't care about this state, so do nothing wait for something else
        }

        public void PostAutoDPostInboxSync (int accountId)
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (SuccessMessage);
        }

        public void ServerIndTooManyDevices (int accountId)
        {
            LoginEvents.Owner = null;
            BackEnd.Instance.Stop (accountId);
            NcAccountHandler.Instance.RemoveAccount (accountId);
            account = null;

            NcAlertView.Show (this.Activity,
                "Account Setup Failed",
                "You are already using the maximum number of devices for this account.  Please contact your system administrator.",
                () => {
                    var parent = (WaitingFragmentDelegate)Activity;
                    parent.WaitingFinished (account);
                });
        }

        public void ServerIndServerErrorRetryLater (int acccountId)
        {
            LoginEvents.Owner = null;
            CompleteWithMessage (ErrorMessage);
        }

        private void CompleteWithMessage (AccountSyncingStatusMessage message)
        {
            CompleteAccount ();
            Message = message;
            Update ();
            if (mIsVisible) {
                Log.Info (Log.LOG_UI, "AccountSyncingViewController will set dismiss delay immediately");
                DismissAfterDelay ();
            } else {
                Log.Info (Log.LOG_UI, "AccountSyncingViewController will set dismiss delay on visible");
                DismissOnVisible = true;
            }
        }

        #endregion

    }
}

