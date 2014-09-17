// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class StartupViewController : NcUIViewController
    {

        public StartupViewController (IntPtr handle) : base (handle)
        {
        }

        /// <summary>
        /// On first run, push the modal LaunchViewController to get credentials.
        /// </summary>
        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            PerformSegue (NextSegue (), this);
        }

        public static string NextSegue ()
        {
            bool hasSynced;
            bool hasCreds;
            bool hasViewedTutorial;
            string hasOpenedFromEvent;
            int accountId;

            if (LoginHelpers.IsCurrentAccountSet ()) {
                accountId = LoginHelpers.GetCurrentAccountId ();
                hasSynced = LoginHelpers.HasFirstSyncCompleted (accountId);
                hasCreds = LoginHelpers.HasProvidedCreds (accountId);
                hasViewedTutorial = LoginHelpers.HasViewedTutorial (accountId);
                hasOpenedFromEvent = McMutables.Get ("EventNotif", accountId.ToString ());
            } else {
                hasSynced = false;
                hasCreds = false;
                hasViewedTutorial = false;
                hasOpenedFromEvent = null;
            }

            if (!hasCreds) {
                return "SegueToLaunch";
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

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {

            if (segue.Identifier == "SegueToEventView") {
                var vc = (EventViewController)segue.DestinationViewController;
                var eventId = Convert.ToInt32(McMutables.Get ("EventNotif", LoginHelpers.GetCurrentAccountId ().ToString ()));
                var item = McEvent.QueryById<McEvent> (eventId);
                vc.SetCalendarItem (item, CalendarItemEditorAction.view);
                McMutables.Delete ("EventNotif", LoginHelpers.GetCurrentAccountId ().ToString ());
                //vc.SetOwner (this);
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
    