using System;
using System.Collections.Generic;

using Foundation;
using AppKit;
using NachoCore.Model;

namespace NachoClient.Mac
{
    public partial class WelcomePageController : NachoPageController, WelcomeViewDelegate, AccountTypeViewDelegate, CredentialsViewDelegate
	{
        
        
		public WelcomePageController (IntPtr handle) : base (handle)
		{
		}

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear ()
        {
            var welcomeViewController = Storyboard.InstantiateControllerWithIdentifier ("WelcomeViewController") as WelcomeViewController;
            welcomeViewController.WelcomeDelegate = this;
            PushViewController (welcomeViewController, animated: false);

//            if (null != configAccount) {
//                Log.Info (Log.LOG_UI, "StartupViewController: found account being configured");
//                ShowSetupScreen ();
//            } else if (null == mdmAccount && NcMdmConfig.Instance.IsPopulated) {
//                ShowSetupScreen ();
//            } else if (null == NcApplication.Instance.Account) {
//                Log.Info (Log.LOG_UI, "StartupViewController: null NcApplication.Instance.Account");
//                ShowSetupScreen ();
//            } else if ((null != deviceAccount) && (deviceAccount.Id == NcApplication.Instance.Account.Id)) {
//                Log.Info (Log.LOG_UI, "StartupViewController: NcApplication.Instance.Account is deviceAccount");
//                ShowSetupScreen ();
//            } else if (!NcApplication.ReadyToStartUI ()) {
//                Log.Info (Log.LOG_UI, "StartupViewController: not ready to start UI, assuming tutorial still needs display");
//                // This should only be if the app closed before the tutorial was dismissed;
//                ShowSetupScreen (true);
//            } else {
//                Log.Info (Log.LOG_UI, "StartupViewController: Ready to go, showing application");
//                ShowApplication ();
//            }
        }

        public override void ViewDidAppear ()
        {
            base.ViewDidAppear ();
            View.Window.MovableByWindowBackground = true;
        }

        public void WelcomeViewDidContinueWithAccount (McAccount account)
        {
            if (account == null) {
                var accountTypeController = Storyboard.InstantiateControllerWithIdentifier ("AccountTypeViewController") as AccountTypeViewController;
                accountTypeController.AccountDelegate = this;
                PushViewController (accountTypeController, animated: true);
            } else {
                var credentialsViewController = Storyboard.InstantiateControllerWithIdentifier ("StandardCredentialsViewController") as StandardCredentialsViewController;
                credentialsViewController.Service = account.AccountService;
                credentialsViewController.Account = account;
                PushViewController (credentialsViewController, animated: false);
            }
        }

        public void AccountTypeViewDidSelectService (McAccount.AccountServiceEnum service)
        {
            var credentialsViewController = Storyboard.InstantiateControllerWithIdentifier ("StandardCredentialsViewController") as StandardCredentialsViewController;
            credentialsViewController.Service = service;
            credentialsViewController.AccountDelegate = this;
            PushViewController (credentialsViewController, animated: true);
        }

        public void CredentialsViewDidCreateAccount (McAccount account)
        {
            View.Window.Close ();
        }

	}
}
