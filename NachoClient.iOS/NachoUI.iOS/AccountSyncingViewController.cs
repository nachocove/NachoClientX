// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{

    #region Delegate

    public interface AccountSyncingViewControllerDelegate
    {
        void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc);
    }

    #endregion

    public partial class AccountSyncingViewController : UIViewController
    {

        #region Properties

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

        public AccountSyncingViewControllerDelegate AccountDelegate;
        private bool StatusIndCallbackIsSet;
        private McAccount account;
        private bool IsVisible;
        private bool DismissOnVisible;
        private NcTimer DismissTimer;

        private static AccountSyncingStatusMessage SyncingMessage = new AccountSyncingStatusMessage ("Syncing...", "Syncing your inbox...", true);
        private static AccountSyncingStatusMessage SuccessMessage = new AccountSyncingStatusMessage ("Account Created", "Your account is ready!", false);
        private static AccountSyncingStatusMessage ErrorMessage = new AccountSyncingStatusMessage ("Account Created", "Sorry, we could not fully sync your inbox.  Please see Settings for more information", false);
        private static AccountSyncingStatusMessage NetworkMessage = new AccountSyncingStatusMessage ("Account Created", "Syncing will complete when network connectivity is restored", false);

        private AccountSyncingStatusMessage Message = SyncingMessage;

        public McAccount Account {
            get {
                return account;
            }

            set {
                account = value;
                StartListeningForApplicationStatus ();
            }
        }

        #endregion

        #region Constructors

        public AccountSyncingViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.HidesBackButton = true;
        }

        #endregion

        #region iOS View Lifecycle

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            var attrs = new UITextAttributes ();
            attrs.Font = A.Font_AvenirNextMedium17;
            attrs.TextColor = A.Color_NachoSubmitButton;
            skipButton.SetTitleTextAttributes (attrs, UIControlState.Normal);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            Update ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            IsVisible = true;
            if (Message.IsWorking) {
                activityIndicatorView.StartAnimating ();
            }
            if (DismissOnVisible) {
                DismissAfterDelay ();
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            IsVisible = false;
            activityIndicatorView.StopAnimating ();
        }

        #endregion

        #region User Actions

        partial void Skip (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountSyncingViewController user did skip, dismissing as if we were done");
            StopListeningForApplicationStatus ();
            CompleteAccount ();
            Dismiss ();
        }

        #endregion

        #region View Helpers

        void Update ()
        {
            if (IsViewLoaded) {
                NavigationItem.Title = Message.Title;
                statusLabel.Text = Message.Details;
                if (IsVisible) {
                    if (Message.IsWorking) {
                        activityIndicatorView.StartAnimating ();
                    } else {
                        activityIndicatorView.StopAnimating ();
                    }
                }
                if (Message.IsWorking) {
                    NavigationItem.RightBarButtonItem = skipButton;
                } else {
                    NavigationItem.RightBarButtonItem = null;
                }
            }
        }

        private void CompleteAccount ()
        {
            if (Account != null) {
                Account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
                Account.Update ();
            }
        }

        public void Complete ()
        {
            CompleteWithMessage (SuccessMessage);
        }

        void DismissAfterDelay ()
        {
            Log.Info (Log.LOG_UI, "AccountSyncingViewController starting dismiss timer");
            DismissTimer = new NcTimer ("AccountSyncViewControllerDismiss", (state) => {
                InvokeOnUIThread.Instance.Invoke (() => {
                    Dismiss ();
                });
            }, null, TimeSpan.FromSeconds (2), TimeSpan.Zero);

        }

        private void Dismiss ()
        {
            Log.Info (Log.LOG_UI, "AccountSyncingViewController dismissing by calling delegate");
            if (DismissTimer != null) {
                DismissTimer.Dispose ();
                DismissTimer = null;
            }
            AccountDelegate.AccountSyncingViewControllerDidComplete (this);
        }

        #endregion

        #region Backend Events

        void StartListeningForApplicationStatus ()
        {
            if (!StatusIndCallbackIsSet) {
                StatusIndCallbackIsSet = true;
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        void StopListeningForApplicationStatus ()
        {
            if (StatusIndCallbackIsSet) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                StatusIndCallbackIsSet = false;
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (!StatusIndCallbackIsSet) {
                Log.Info (Log.LOG_UI, "AccountSyncingViewController ignoring status callback because listening has been disabled");
                return;
            }
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_BackEndStateChanged == s.Status.SubKind) {
                if (s.Account != null && s.Account.Id == Account.Id) {
                    var senderState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                    var readerState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                    Log.Info (Log.LOG_UI, "AccountSyncingViewController senderState {0}, readerState {1} for account {2}", senderState, readerState, Account.Id);
                    if ((BackEndStateEnum.ServerConfWait == senderState) || (BackEndStateEnum.ServerConfWait == readerState)) {
                        StopListeningForApplicationStatus ();
                        CompleteWithMessage (ErrorMessage);
                    } else if ((BackEndStateEnum.CredWait == senderState) || (BackEndStateEnum.CredWait == readerState)) {
                        StopListeningForApplicationStatus ();
                        CompleteWithMessage (ErrorMessage);
                    } else if ((BackEndStateEnum.CertAskWait == senderState) || (BackEndStateEnum.CertAskWait == readerState)) {
                        StopListeningForApplicationStatus ();
                        CompleteWithMessage (ErrorMessage);
                    } else if ((senderState == BackEndStateEnum.PostAutoDPostInboxSync) && (readerState == BackEndStateEnum.PostAutoDPostInboxSync)) {
                        Log.Info (Log.LOG_UI, "AccountSyncingViewController saw PostAutoDBPostInboxSync for both sender and reader, account ID{0}", Account.Id);
                        CompleteWithMessage (SuccessMessage);
                    }
                }
            } else if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                StopListeningForApplicationStatus ();
                CompleteWithMessage (NetworkMessage);
            }
        }

        private void CompleteWithMessage (AccountSyncingStatusMessage message)
        {
            StopListeningForApplicationStatus ();
            CompleteAccount ();
            Message = message;
            Update ();
            if (IsVisible) {
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
