// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using System.Linq;

namespace NachoClient.iOS
{

    #region Delegate

    public interface GettingStartedViewControllerDelegate
    {
        void GettingStartedViewControllerDidComplete (GettingStartedViewController vc);
    }

    #endregion

    public partial class GettingStartedViewController : NcUIViewController, AccountTypeViewControllerDelegate, AccountCredentialsViewControllerDelegate, AccountSyncingViewControllerDelegate, HomeViewControllerDelegate
    {

        #region Properties

        public GettingStartedViewControllerDelegate AccountDelegate;
        public CGRect? AnimateFromLaunchImageFrame = null;
        private CGSize originalCircleImageSize;
        private nfloat originalCircleImageOffset;
        private UIStoryboard _accountStoryboard;

        private UIStoryboard accountStoryboard {
            get { 
                if (_accountStoryboard == null) {
                    _accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
                }
                return _accountStoryboard;
            }
        }

        public bool StartWithTutorial;
        AccountSyncingViewController syncingViewController;
        EventHandler StartEventHandler;
        byte[] prefetchedImageBytes = null;

        #endregion

        #region Constructors

        public GettingStartedViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

        #endregion

        #region iOS View Lifecycle

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            getStartedButton.Layer.CornerRadius = 6.0f;
            Util.ConfigureNavBar (false, NavigationController);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            getStartedButton.Hidden = false;
            var accountBeingConfigured = McAccount.GetAccountBeingConfigured ();
            var mdmAccount = McAccount.GetMDMAccount ();
            EventHandler startEventHandler = null;
            if (accountBeingConfigured != null || StartWithTutorial) {
                Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with account being configured (or just tutorial left)");
                introLabel.Text = "Welcome Back!  We need to finish setting up your account.";
                getStartedButton.SetTitle ("Continue", UIControlState.Normal);
                if (accountBeingConfigured != null) {
                    startEventHandler = ShowAccountBeingConfigured;
                } else {
                    startEventHandler = ShowTutorial;
                }
            } else if (NcMdmConfig.Instance.IsPopulated && mdmAccount == null) {
                if (NcMdmConfig.Instance.IsValid) {
                    Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with mdm account");
                    var companyName = NcMdmConfig.Instance.BrandingName;
                    if (String.IsNullOrEmpty (companyName)) {
                        companyName = "company";
                    }
                    if (!String.IsNullOrEmpty (NcMdmConfig.Instance.BrandingLogoUrl)) {
                        PrefetchAccountImageUrl (new NSUrl (NcMdmConfig.Instance.BrandingLogoUrl));
                    }
                    introLabel.Text = String.Format ("Start by setting up your {0} account.", companyName);
                    getStartedButton.SetTitle ("Get Started", UIControlState.Normal);
                    startEventHandler = ShowMDMAccount;
                } else {
                    // The user is stuck here at a dead end until their MDM profile is fixed.  Will we be relaunched, or do we need to
                    // force a re-query of the mdm parameters somehow?
                    Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with invalid mdm account");
                    var companyName = NcMdmConfig.Instance.BrandingName;
                    if (String.IsNullOrEmpty (companyName)) {
                        companyName = "company";
                    }
                    introLabel.Text = String.Format ("Please contact the administrator for your {0} account.  We received an invalid configuration.", companyName);
                    getStartedButton.Hidden = true;
                }
            } else {
                Log.Info (Log.LOG_UI, "GettingStartedViewController will appear with no account");
                introLabel.Text = "Start by choosing your email service provider";
                getStartedButton.SetTitle ("Get Started", UIControlState.Normal);
                startEventHandler = ShowAccountTypeChooser;
            }
            if (StartEventHandler != null) {
                getStartedButton.RemoveTarget (StartEventHandler, UIControlEvent.TouchUpInside);
            }
            StartEventHandler = startEventHandler;
            if (StartEventHandler != null) {
                getStartedButton.AddTarget (StartEventHandler, UIControlEvent.TouchUpInside);
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
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "tutorial") {
                var vc = (HomeViewController)segue.DestinationViewController;
                vc.AccountDelegate = this;
                vc.Service = (McAccount.AccountServiceEnum)((SegueHolder)sender).value;
            }
        }

        #endregion

        #region User Actions

        private void ShowAccountBeingConfigured (object sender, EventArgs e)
        {
            var accountBeingConfigured = McAccount.GetAccountBeingConfigured ();
            Log.Info (Log.LOG_UI, "GettingStartedViewController going to continue account ID{0} config", accountBeingConfigured.Id);
            var vc = (AccountTypeViewController)accountStoryboard.InstantiateViewController ("AccountTypeViewController");
            var credentialsViewController = vc.SuggestedCredentialsViewController (accountBeingConfigured.AccountService);
            credentialsViewController.AccountDelegate = this;
            credentialsViewController.Account = accountBeingConfigured;
            NavigationController.PushViewController (credentialsViewController, true);
        }

        private void ShowAccountTypeChooser (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController going to start with fresh account");
            var vc = (AccountTypeViewController)accountStoryboard.InstantiateViewController ("AccountTypeViewController");
            vc.AccountDelegate = this;
            NavigationController.PushViewController (vc, true);
        }

        private void ShowTutorial (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController going to continue with tutorial");
            syncingViewController = (AccountSyncingViewController)accountStoryboard.InstantiateViewController ("AccountSyncingViewController");
            syncingViewController.AccountDelegate = this;
            syncingViewController.Complete ();
            PerformSegue ("tutorial", new SegueHolder (NcApplication.Instance.Account.AccountService));
        }

        private void ShowMDMAccount (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController going to mdm account config");
            var vc = (StandardCredentialsViewController)accountStoryboard.InstantiateViewController ("AccountCredentialsViewController");
            vc.AccountDelegate = this;
            vc.Account = NcAccountHandler.Instance.CreateAccount (NcMdmConfig.Instance);
            if (prefetchedImageBytes != null) {
                var portrait = McPortrait.InsertFile (vc.Account.Id, prefetchedImageBytes);
                vc.Account.DisplayPortraitId = portrait.Id;
                vc.Account.Update ();
                // FIXME: what if the prefetch hasn't finished yet?
            }
            NavigationController.PushViewController (vc, true);
        }

        #endregion

        #region View Helpers

        public async void PrefetchAccountImageUrl (NSUrl url)
        {
            try {
                var httpClient = new System.Net.Http.HttpClient ();
                byte[] imageBytes = await httpClient.GetByteArrayAsync (url);
                // this line will throw an exception if the native UIImage can't be constructed
                new UIImage (NSData.FromArray (imageBytes));
                // so if we get here, we know the bytes are a valid image
                prefetchedImageBytes = imageBytes;
            } catch (Exception e) {
                Log.Info (Log.LOG_DB, "GettingStartedViewController: PrefetchAccountImageUrl exception: {0}", e);
            }
        }

        #endregion

        #region Account Type Delegate

        public void AccountTypeViewControllerDidSelectService (AccountTypeViewController vc, McAccount.AccountServiceEnum service)
        {
            var credentialsViewController = vc.SuggestedCredentialsViewController (service);
            credentialsViewController.Service = service;
            credentialsViewController.AccountDelegate = this;
            NavigationController.PushViewController (credentialsViewController, true);
        }

        #endregion

        #region Account Credentials Delegate

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
                PerformSegue ("tutorial", new SegueHolder (account.AccountService)); 
            }
        }

        #endregion

        #region Home View Delegate

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

        #endregion

        #region Account Syncing Delegate

        public void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc)
        {
            Log.Info (Log.LOG_UI, "GettingStartedViewController syncing complete");
            if (vc.Account != null) {
                LoginHelpers.SetSwitchAwayTime (NcApplication.Instance.Account.Id);
                NcApplication.Instance.Account = vc.Account;
            }
            AccountDelegate.GettingStartedViewControllerDidComplete (this);
        }

        #endregion
    }
}
