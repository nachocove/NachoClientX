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

    public interface AccountSyncingViewControllerDelegate {
        void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc);
    }

    public partial class AccountSyncingViewController : UIViewController
    {

        public AccountSyncingViewControllerDelegate AccountDelegate;
        private bool StatusIndCallbackIsSet;
        private McAccount account;
        private bool IsSyncingComplete;
        private bool IsVisible;
        private bool DismissOnVisible;
        private NcTimer DismissTimer;

        public McAccount Account {
            get {
                return account;
            }

            set {
                account = value;
                StartListeningForApplicationStatus ();
            }
        }

        public AccountSyncingViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.HidesBackButton = true;
        }

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

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            Update ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            IsVisible = true;
            if (!IsSyncingComplete) {
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

        void Complete ()
        {
            IsSyncingComplete = true;
            Account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
            Account.Update ();
            StopListeningForApplicationStatus ();
            Update ();
            if (IsVisible) {
                DismissAfterDelay ();
            } else {
                DismissOnVisible = true;
            }
        }

        void Update ()
        {
            if (IsViewLoaded) {
                if (IsSyncingComplete) {
                    if (IsVisible) {
                        activityIndicatorView.StopAnimating ();
                    }
                    NavigationItem.Title = "Account Created";
                    statusLabel.Text = "Your account is ready!";
                } else {
                    if (IsVisible) {
                        activityIndicatorView.StopAnimating ();
                    }
                    NavigationItem.Title = "Syncing...";
                    statusLabel.Text = "Syncing your inbox...";
                }
            }
        }

        void DismissAfterDelay ()
        {
            DismissTimer = new NcTimer ("AccountSyncViewControllerDismiss", (state) => {
                InvokeOnUIThread.Instance.Invoke (() => {
                    Dismiss ();
                });
            }, null, TimeSpan.FromSeconds(2), TimeSpan.Zero);

        }

        private void Dismiss ()
        {
            DismissTimer.Dispose ();
            DismissTimer = null;
            AccountDelegate.AccountSyncingViewControllerDidComplete (this);
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Account != null && s.Account.Id == Account.Id) {
                var senderState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                var readerState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);
                if ((senderState == BackEndStateEnum.PostAutoDPostInboxSync) && (readerState == BackEndStateEnum.PostAutoDPostInboxSync)) {
                    Complete ();
                }
            }
        }
    }
}
