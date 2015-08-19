// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface GettingStartedViewControllerDelegate
    {
        void GettingStartedViewControllerDidComplete (GettingStartedViewController vc);
    }

    public partial class GettingStartedViewController : UIViewController, AccountTypeViewControllerDelegate, AccountCredentialsViewControllerDelegate, AccountSyncingViewControllerDelegate, HomeViewControllerDelegate
    {
        public GettingStartedViewControllerDelegate AccountDelegate;
        public CGRect? AnimateFromLaunchImageFrame = null;
        private CGSize originalCircleImageSize;
        private nfloat originalCircleImageOffset;
        private UIStoryboard accountStoryboard;
        public bool StartWithTutorial;
        AccountSyncingViewController syncingViewController;

        public GettingStartedViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            getStartedButton.Layer.CornerRadius = 6.0f;
            Util.ConfigureNavBar (false, NavigationController);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            var accountBeingConfigured = McAccount.GetAccountBeingConfigured ();
            if (accountBeingConfigured != null || StartWithTutorial) {
                Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with account being configured (or just tutorial left)");
                introLabel.Text = "Welcome Back!  We need to finish setting up your account.";
                getStartedButton.SetTitle ("Continue", UIControlState.Normal);
            } else {
                Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with no account");
                introLabel.Text = "Start by choosing your email service provider";
                getStartedButton.SetTitle ("Get Started", UIControlState.Normal);
            }
            if (AnimateFromLaunchImageFrame != null) {
                View.LayoutIfNeeded ();
                originalCircleImageSize = circleImageView.Frame.Size;
                originalCircleImageOffset = circleVerticalSpaceConstraint.Constant;
                circleWidthContstraint.Constant = AnimateFromLaunchImageFrame.Value.Width;
                circleHeightConstraint.Constant = AnimateFromLaunchImageFrame.Value.Height;
                View.Superview.LayoutIfNeeded ();
                var frame = circleImageView.Superview.ConvertRectFromView (AnimateFromLaunchImageFrame.Value, View);
                circleVerticalSpaceConstraint.Constant = frame.Top - NavigationController.NavigationBar.Frame.Height - NavigationController.NavigationBar.Frame.Top;
                introLabel.Alpha = 0.0f;
                getStartedButton.Alpha = 0.0f;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (AnimateFromLaunchImageFrame != null) {
                AnimateFromLaunchImageFrame = null;
                circleWidthContstraint.Constant = originalCircleImageSize.Width;
                circleHeightConstraint.Constant = originalCircleImageSize.Height;
                circleVerticalSpaceConstraint.Constant = originalCircleImageOffset;
                UIView.Animate (0.5, () => {
                    circleImageView.Superview.LayoutIfNeeded ();
                });
                UIView.Animate (0.2, 0.3, 0, () => {
                    introLabel.Alpha = 1.0f;
                    getStartedButton.Alpha = 1.0f;
                }, null);
            }
            if (StartWithTutorial) {
                AccountDelegate.GettingStartedViewControllerDidComplete (this);
            }
        }

        partial void getStarted (NSObject sender)
        {
            accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
            var accountBeingConfigured = McAccount.GetAccountBeingConfigured ();
            if (accountBeingConfigured != null){
                Log.Info (Log.LOG_UI, "GettingStartedViewController going to continue account ID{0} config", accountBeingConfigured.Id);
                var vc = (AccountCredentialsViewController)accountStoryboard.InstantiateViewController ("AccountCredentialsViewController");
                vc.AccountDelegate = this;
                vc.Account = accountBeingConfigured;
                NavigationController.PushViewController (vc, true);
            }else if (StartWithTutorial){
                Log.Info (Log.LOG_UI, "GettingStartedViewController going to continue with tutorial");
                PerformSegue ("tutorial", null);
            }else{
                Log.Info (Log.LOG_UI, "GettingStartedViewController going to start with fresh account");
                var vc = (AccountTypeViewController)accountStoryboard.InstantiateViewController ("AccountTypeViewController");
                vc.AccountDelegate = this;
                NavigationController.PushViewController (vc, true);
            }
        }

        public void AccountTypeViewControllerDidSelectService (AccountTypeViewController vc, McAccount.AccountServiceEnum service)
        {
            if (service == McAccount.AccountServiceEnum.GoogleDefault) {
                Log.Info (Log.LOG_UI, "GettingStartedViewController need google credentials");
                // Do the google thing
            } else {
                Log.Info (Log.LOG_UI, "GettingStartedViewController prompting for credentials for {0}", service);
                var credentialsViewController = (AccountCredentialsViewController)accountStoryboard.InstantiateViewController ("AccountCredentialsViewController");
                credentialsViewController.Service = service;
                credentialsViewController.AccountDelegate = this;
                NavigationController.PushViewController (credentialsViewController, true);
            }
        }

        public void AccountCredentialsViewControllerDidValidateAccount (AccountCredentialsViewController vc, McAccount account)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController account ID{0} is validated, starting sync", account.Id);
            syncingViewController = (AccountSyncingViewController)accountStoryboard.InstantiateViewController ("AccountSyncingViewController");
            syncingViewController.AccountDelegate = this;
            syncingViewController.Account = account;
            BackEnd.Instance.Start (syncingViewController.Account.Id);
            if (LoginHelpers.HasViewedTutorial ()) {
                Log.Info (Log.LOG_UI, "GettingStartedViewController tutorial has been viewed, just showing sync");
                NavigationController.PushViewController (syncingViewController, true);
            } else {
                Log.Info (Log.LOG_UI, "GettingStartedViewController showing tutorial over sync");
                PerformSegue ("tutorial", null); 
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "tutorial") {
                var vc = (HomeViewController)segue.DestinationViewController;
                vc.AccountDelegate = this;
            }
        }

        public void HomeViewControllerDidAppear (HomeViewController vc)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController inserting sync view under tutorial");
            UIViewController[] viewControllers = new UIViewController[NavigationController.ViewControllers.Length + 1];
            var i = 0;
            for (; i < NavigationController.ViewControllers.Length - 1; ++i) {
                viewControllers [i] = NavigationController.ViewControllers [i];
            }
            viewControllers [i] = syncingViewController;
            viewControllers [i + 1] = NavigationController.ViewControllers [i];
            NavigationController.SetViewControllers (viewControllers, false);
        }

        public void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController syncing complete");
            // FIXME: Only set if null or device
            NcApplication.Instance.Account = vc.Account;
            LoginHelpers.SetSwitchToTime (vc.Account);
            AccountDelegate.GettingStartedViewControllerDidComplete (this);
        }
    }
}
