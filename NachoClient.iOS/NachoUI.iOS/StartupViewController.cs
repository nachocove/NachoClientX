// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Linq;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class StartupViewController : NcUIViewController, GettingStartedViewControllerDelegate
    {

        #region Properties

        bool StatusIndCallbackIsSet = false;

        enum StartupViewState
        {
            Startup,
            Incompatible,
            Setup,
            Migration,
            Recovery,
            Blank,
            App
        }

        StartupViewState currentState = StartupViewState.Startup;

        #endregion

        #region Constructors

        public StartupViewController (IntPtr handle) : base (handle)
        {
        }

        #endregion

        #region iOS View Lifecycle

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            Log.Info (Log.LOG_UI, "StartupViewController: viewdidload");

            if (!NcMigration.IsCompatible ()) {
                Log.Info (Log.LOG_UI, "StartupViewController: found incompatible migration");
                currentState = StartupViewState.Incompatible;
                // Display an alert view and wait to get out
                NcAlertView.ShowMessage (this,
                    "Incompatible Version",
                    "Running this older version results in an incompatible downgrade from the previously installed version. Please install a newer version of the app.");
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);

            if (currentState == StartupViewState.Startup) {
                Log.Info (Log.LOG_UI, "StartupViewController: viewDidAppear in Startup state, determining where to go");
                ShowScreenForApplicationState ();
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "migration") {
                var vc = (StartupMigrationViewController)segue.DestinationViewController;
                if (currentState == StartupViewState.Startup) {
                    vc.AnimateFromLaunchImageFrame = circleImageView.Superview.ConvertRectToView (circleImageView.Frame, View);
                }
                return;
            }
            if (segue.Identifier == "recovery") {
                var vc = (StartupRecoveryViewController)segue.DestinationViewController;
                if (currentState == StartupViewState.Startup) {
                    vc.AnimateFromLaunchImageFrame = circleImageView.Superview.ConvertRectToView (circleImageView.Frame, View);
                }
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        #endregion

        #region Auto-Navigations

        void ShowScreenForApplicationState ()
        {
            if (NcApplication.Instance.IsUp ()) {
                StopListeningForApplicationStatus ();
                var configAccount = McAccount.GetAccountBeingConfigured ();
                var deviceAccount = McAccount.GetDeviceAccount ();
                var mdmAccount = McAccount.GetMDMAccount ();
                if (null != configAccount) {
                    Log.Info (Log.LOG_UI, "StartupViewController: found account being configured");
                    ShowSetupScreen ();
                } else if (null == mdmAccount && NcMdmConfig.Instance.IsPopulated) {
                    ShowSetupScreen ();
                } else if (null == NcApplication.Instance.Account) {
                    Log.Info (Log.LOG_UI, "StartupViewController: null NcApplication.Instance.Account");
                    ShowSetupScreen ();
                } else if ((null != deviceAccount) && (deviceAccount.Id == NcApplication.Instance.Account.Id)) {
                    Log.Info (Log.LOG_UI, "StartupViewController: NcApplication.Instance.Account is deviceAccount");
                    ShowSetupScreen ();
                } else if (!NcApplication.ReadyToStartUI ()) {
                    Log.Info (Log.LOG_UI, "StartupViewController: not ready to start UI, assuming tutorial still needs display");
                    // This should only be if the app closed before the tutorial was dismissed;
                    ShowSetupScreen (true);
                } else {
                    Log.Info (Log.LOG_UI, "StartupViewController: Ready to go, showing application");
                    ShowApplication ();
                }
            } else {
                StartListeningForApplicationStatus ();
                if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Migrating) {
                    Log.Info (Log.LOG_UI, "StartupViewController: instance isn't up yet, in Migrating state");
                    ShowMigrationScreen ();
                } else if (NcApplication.Instance.ExecutionContext == NcApplication.ExecutionContextEnum.Initializing) {
                    Log.Info (Log.LOG_UI, "StartupViewController: instance isn't up yet, in Initializing state");
                    if (NcApplication.Instance.InSafeMode ()) {
                        ShowRecoveryScreen ();
                    } else {
                        Log.Info (Log.LOG_UI, "StartupViewController initializing, but not in safe mode, keeping current screen");
                    }
                } else {
                    // I don't think we can ever get here based on the definition of NcApplication.IsUp.  If things
                    // change (like a new state is added to NcApplication), this will just result in a blank screen
                    // until the application is up.
                    currentState = StartupViewState.Blank;
                    Log.Info (Log.LOG_UI, "StartupViewController instance isn't up yet, in unexpected state, showing BLANK");
                }
            }
        }

        void ShowMigrationScreen ()
        {
            if (currentState == StartupViewState.Migration) {
                return;
            }
            Log.Info (Log.LOG_UI, "StartupViewController ShowMigrationScreen");
            if (PresentedViewController != null) {
                var window = UIApplication.SharedApplication.Delegate.GetWindow ();
                UIView.Transition (window, 0.3, UIViewAnimationOptions.TransitionCrossDissolve, () => {
                    DismissViewController (false, null);
                    PerformSegue ("migration", null);
                }, () => {
                });
            } else {
                PerformSegue ("migration", null);
            }
            currentState = StartupViewState.Migration;
        }

        void ShowRecoveryScreen ()
        {
            if (currentState == StartupViewState.Recovery) {
                return;
            }
            Log.Info (Log.LOG_UI, "StartupViewController ShowRecoveryScreen");
            if (PresentedViewController != null) {
                var window = UIApplication.SharedApplication.Delegate.GetWindow ();
                UIView.Transition (window, 0.3, UIViewAnimationOptions.TransitionCrossDissolve, () => {
                    DismissViewController (false, null);
                    PerformSegue ("recovery", null);
                }, () => {
                });
            } else {
                PerformSegue ("recovery", null);
            }
            currentState = StartupViewState.Recovery;
        }

        void ShowSetupScreen (bool startWithTutorial = false)
        {
            if (currentState == StartupViewState.Startup) {
                return;
            }
            Log.Info (Log.LOG_UI, "StartupViewController ShowSetupScreen");
            var storyboard = UIStoryboard.FromName ("Welcome", null);
            UINavigationController vc = (UINavigationController)storyboard.InstantiateInitialViewController ();
            var gettingStartedViewController = (GettingStartedViewController)vc.ViewControllers [0];
            gettingStartedViewController.StartWithTutorial = startWithTutorial;
            gettingStartedViewController.AccountDelegate = this;
            if (currentState == StartupViewState.Startup) {
                gettingStartedViewController.AnimateFromLaunchImageFrame = circleImageView.Superview.ConvertRectToView (circleImageView.Frame, View);
            }
            if (PresentedViewController != null) {
                var window = UIApplication.SharedApplication.Delegate.GetWindow ();
                UIView.Transition (window, 0.3, UIViewAnimationOptions.TransitionCrossDissolve, () => {
                    DismissViewController (false, null);
                    PresentViewController (vc, false, null);
                }, () => {
                });
            } else {
                PresentViewController (vc, false, null);
            }
            currentState = StartupViewState.Setup;
        }

        void ShowApplication ()
        {
            Log.Info (Log.LOG_UI, "StartupViewController ShowApplication");
            var mainStoryboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var appViewController = (UITabBarController)mainStoryboard.InstantiateInitialViewController ();

            // If we have an event, we'll push it on the selected nav controller so when the user hits back, they're at the app
            // NOTE: this assumes that the selected view controller will be a UINavigationController.  In this case, the selected
            // view controller should always be the first tab because we've just launched.
            var deviceAccount = McAccount.GetDeviceAccount ();
            var currentAccountIdString = LoginHelpers.GetCurrentAccountId ().ToString ();
            var eventId = McMutables.Get (deviceAccount.Id, "EventNotif", currentAccountIdString);
            if (null != eventId) {
                Log.Info (Log.LOG_UI, "StartupViewController ShowingEvent");
                var vc = (EventViewController)Storyboard.InstantiateViewController ("EventViewController");
                var item = McEvent.QueryById<McEvent> (Convert.ToInt32 (eventId));
                vc.SetCalendarItem (item);
                McMutables.Delete (deviceAccount.Id, "EventNotif", currentAccountIdString);
                var navController = (UINavigationController)appViewController.SelectedViewController;
                navController.PushViewController (vc, false);
            }

            var window = UIApplication.SharedApplication.Delegate.GetWindow ();
            // Swap us out as the window's root view controller because we are no longer needed
            var windowSnapshot = window.SnapshotView (false);
            window.RootViewController = appViewController;
            windowSnapshot.Frame = new CoreGraphics.CGRect (0, -appViewController.View.Frame.Top, windowSnapshot.Frame.Width, windowSnapshot.Frame.Height);
            appViewController.View.AddSubview (windowSnapshot);
            UIView.Animate (0.3, 0.0, 0, () => {
                windowSnapshot.Alpha = 0.0f;
            }, () => {
                windowSnapshot.RemoveFromSuperview ();
            });
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

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "StartupViewController got ExecutionContextChanged event");
                InvokeOnMainThread (() => {
                    ShowScreenForApplicationState ();
                });
            }
        }

        #endregion

        #region Getting Started Delegate

        public void GettingStartedViewControllerDidComplete (GettingStartedViewController vc)
        {
            Log.Info (Log.LOG_UI, "StartupViewController tutorial was dismissed, going direct to application");
            ShowApplication ();
        }

        #endregion
    }
}
