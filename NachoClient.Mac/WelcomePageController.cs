using System;
using System.Collections.Generic;

using Foundation;
using AppKit;
using NachoCore.Model;

namespace NachoClient.Mac
{
	public partial class WelcomePageController : NSViewController
	{

        List<NSViewController> ViewControllers;
        
		public WelcomePageController (IntPtr handle) : base (handle)
		{
            ViewControllers = new List<NSViewController> ();
		}

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear ()
        {
            var welcomeViewController = Storyboard.InstantiateControllerWithIdentifier ("WelcomeViewController") as WelcomeViewController;
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

        public void GetStarted ()
        {
            var accountTypeController = Storyboard.InstantiateControllerWithIdentifier ("AccountTypeViewController") as AccountTypeViewController;
            PushViewController (accountTypeController, animated: true);
        }

        public void ContinueWithAccountType (McAccount.AccountServiceEnum accountType)
        {
            var credentialsViewController = Storyboard.InstantiateControllerWithIdentifier ("StandardCredentialsViewController") as StandardCredentialsViewController;
            credentialsViewController.Service = accountType;
            PushViewController (credentialsViewController, animated: true);
        }

        public void ContinueWithAccount (McAccount account)
        {
            var credentialsViewController = Storyboard.InstantiateControllerWithIdentifier ("StandardCredentialsViewController") as StandardCredentialsViewController;
            credentialsViewController.Service = account.AccountService;
            credentialsViewController.Account = account;
            PushViewController (credentialsViewController, animated: false);
        }

        public void Complete (McAccount account)
        {
            View.Window.Close ();
        }

        void PushViewController (NSViewController viewController, bool animated = true)
        {
            animated = false; // TODO: support animation
            viewController.View.Frame = View.Bounds;
            AddChildViewController (viewController);
            View.AddSubview (viewController.View);
            NSViewController topViewController = null;
            if (ViewControllers.Count > 0) {
                topViewController = ViewControllers [ViewControllers.Count - 1];
            }
            ViewControllers.Add (viewController);
            if (animated) {
            } else {
                if (topViewController != null) {
                    topViewController.View.RemoveFromSuperview ();
                }
            }
        }

        void PopViewController (bool animated = true)
        {
            animated = false; // TODO: support animation
            if (ViewControllers.Count > 0) {
                var topViewController = ViewControllers [ViewControllers.Count - 1];
                if (animated) {
                } else {
                    topViewController.RemoveFromParentViewController ();
                    topViewController.View.RemoveFromSuperview ();
                    ViewControllers.RemoveAt (ViewControllers.Count - 1);
                    var viewController = ViewControllers [ViewControllers.Count - 1];
                    View.AddSubview (viewController.View);
                }
            }
        }

	}
}
