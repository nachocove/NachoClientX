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
    public partial class StartupViewController : NcUIViewController
    {
        UIProgressView MigrationProgressBar = null;
        UITextView MigrationMessageTextView = null;

        public StartupViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            if (NcApplication.Instance.IsUp ()) {
                var segueIdentifer = NextSegue ();
                Log.Info (Log.LOG_UI, "svc: PerformSegue({0})", segueIdentifer);
                PerformSegue (NextSegue (), this);
            } else {
                CreateView ();
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        public static string NextSegue ()
        {
            bool hasSynced;
            bool hasCreds;
            bool hasViewedTutorial;
            bool hasAutoDCompleted; 
            string hasOpenedFromEvent;
            int accountId;

            if (LoginHelpers.IsCurrentAccountSet ()) {
                accountId = LoginHelpers.GetCurrentAccountId ();
                hasSynced = LoginHelpers.HasFirstSyncCompleted (accountId);
                hasCreds = LoginHelpers.HasProvidedCreds (accountId);
                hasAutoDCompleted = LoginHelpers.HasAutoDCompleted (accountId);
                hasViewedTutorial = LoginHelpers.HasViewedTutorial (accountId);
                hasOpenedFromEvent = McMutables.Get (McAccount.GetDeviceAccount ().Id, "EventNotif", accountId.ToString ());
            } else {
                hasSynced = false;
                hasCreds = false;
                hasViewedTutorial = false;
                hasAutoDCompleted = false;
                hasOpenedFromEvent = null;
            }

            if (!hasCreds) {
                return "SegueToLaunch";
            } else if (!hasAutoDCompleted) {
                return "SegueToAdvancedLogin";
            } else if (!hasViewedTutorial) {
                return "SegueToHome";
            } else if (!hasSynced) {
                return "SegueToAdvancedLogin";
            } else if (null != hasOpenedFromEvent) {
                return "SegueToEventView";
            } else {
                return "SegueToTabController";
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            ConfigureView ();
        }

        public void CreateView ()
        {
            // We need to migrate. Put up a spinner until this is done.
            this.NavigationItem.Title = "Upgrade";
            this.View.BackgroundColor = A.Color_NachoGreen;

            if (!NcMigration.IsCompatible ()) {
                // Display an alert view and wait to get out
                UIAlertView av = new UIAlertView ();
                av.Title = "Incompatible Version";
                av.Message = "Running this older version results in an incompatible " +
                "downgrade from the previously installed version. Please install a newer version.";
                av.AccessibilityLabel = "Incompatible Version";
                av.Show ();
                return;
            }

            var frame = this.View.Frame;
            var halfHeight = frame.Height / 2.0f;

            MigrationMessageTextView = new UITextView ();
            ViewFramer.Create (MigrationMessageTextView)
                .X (0)
                .Y (halfHeight - 35.0f)
                .Width (frame.Width)
                .Height (35.0f);
            MigrationMessageTextView.TextColor = UIColor.White;
            MigrationMessageTextView.Font = A.Font_AvenirNextRegular14;
            MigrationMessageTextView.Text = String.Format ("Updating your app with latest features... (1 of {0})",
                NcMigration.NumberOfMigrations);
            MigrationMessageTextView.BackgroundColor = A.Color_NachoGreen;
            MigrationMessageTextView.TextAlignment = UITextAlignment.Center;

            MigrationProgressBar = new UIProgressView (frame);
            ViewFramer.Create (MigrationProgressBar).Y (halfHeight + 10.0f).AdjustHeight (20.0f);
            MigrationProgressBar.ProgressTintColor = A.Color_NachoYellow;
            MigrationProgressBar.TrackTintColor = A.Color_NachoIconGray;
            this.Add (MigrationMessageTextView);
            this.Add (MigrationProgressBar);
        }

        void ConfigureView ()
        {
            if (!NcMigration.IsCompatible ()) {
                return;
            }
            if (NcApplication.ExecutionContextEnum.Migrating == NcApplication.Instance.ExecutionContext) {
                this.NavigationItem.Title = "Upgrade";
                MigrationMessageTextView.Hidden = false;
                MigrationProgressBar.Hidden = false;
            } else if (NcApplication.ExecutionContextEnum.Initializing == NcApplication.Instance.ExecutionContext) {
                this.NavigationItem.Title = "Initializing";
                MigrationMessageTextView.Hidden = true;
                MigrationProgressBar.Hidden = true;
            } else {
                this.NavigationItem.Title = "Initialization";
            }
            Log.Info (Log.LOG_UI, "svc: {0}", NcApplication.Instance.ExecutionContext);
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_ExecutionContextChanged == s.Status.SubKind) {
                var execContext = (NcApplication.ExecutionContextEnum)s.Status.Value;
                if (NcApplication.Instance.IsUp ()) {
                    InvokeOnMainThread (() => {
                        if (null != MigrationProgressBar) {
                            MigrationProgressBar.Hidden = false;
                        }
                        PerformSegue (NextSegue (), this);
                    });
                } else {
                    ConfigureView ();
                }
            }
            if (NcResult.SubKindEnum.Info_MigrationProgress == s.Status.SubKind) {
                var percentage = (float)s.Status.Value;
                if (null != MigrationProgressBar) {
                    InvokeOnMainThread (() => {
                        // Skip animation for 0%. That happens right before starting
                        // the next migration. Animation when rewinding to 0% looks weird
                        MigrationProgressBar.SetProgress (percentage, 0.0 != percentage);
                    });
                }
            }
            if (NcResult.SubKindEnum.Info_MigrationDescription == s.Status.SubKind) {
                var description = (string)s.Status.Value;
                if (null != MigrationMessageTextView) {
                    InvokeOnMainThread (() => {
                        MigrationMessageTextView.Text = description;
                    });
                }
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {

            if (segue.Identifier == "SegueToEventView") {
                var vc = (EventViewController)segue.DestinationViewController;
                var devAccountId = McAccount.GetDeviceAccount ().Id;
                var eventId = Convert.ToInt32 (McMutables.Get (devAccountId, "EventNotif", LoginHelpers.GetCurrentAccountId ().ToString ()));
                var item = McEvent.QueryById<McEvent> (eventId);
                vc.SetCalendarItem (item);
                McMutables.Delete (devAccountId, "EventNotif", LoginHelpers.GetCurrentAccountId ().ToString ());
                return;
            }
            if (segue.Identifier == "SegueToNachoNow") {
                return;
            }
            if (segue.Identifier == "SegueToAdvancedLogin") {
                return;
            }
            if (segue.Identifier == "SegueToHome") {
                return;
            }
            if (segue.Identifier == "SegueToLaunch") {
                return;
            }
            if (segue.Identifier == "SegueToTabController") {
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }


    }
}
