// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using UIKit;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;
using System.Linq;

namespace NachoClient.iOS
{
    public partial class GeneralSettingsViewController : NcUIViewControllerNoLeaks, INachoAccountsTableDelegate
    {
        UITableView accountsTableView;
        AccountsTableViewSource accountsTableViewSource;

        SwitchAccountButton switchAccountButton;

        public GeneralSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            NavigationItem.TitleView = switchAccountButton;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (this.NavigationController.RespondsToSelector (new ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                this.NavigationController.InteractivePopGestureRecognizer.Enabled = true;
                this.NavigationController.InteractivePopGestureRecognizer.Delegate = null;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            // garf
            scrollView.RemoveFromSuperview ();

            View.BackgroundColor = A.Color_NachoBackgroundGray;
            View.BackgroundColor = A.Color_NachoBackgroundGray;

            Util.ConfigureNavBar (false, this.NavigationController);

            accountsTableViewSource = new AccountsTableViewSource ();
            accountsTableViewSource.Setup (this, showAccessory: true);

            accountsTableView = new UITableView (View.Frame);
            accountsTableView.Source = accountsTableViewSource;
            accountsTableView.SeparatorColor = A.Color_NachoBackgroundGray;
            accountsTableView.BackgroundColor = A.Color_NachoBackgroundGray;

            View.AddSubview (accountsTableView);           
        }

        void SwitchAccountButtonPressed ()
        {
            SwitchAccountViewController.ShowDropdown (this, SwitchToAccount);
        }

        void SwitchToAccount (McAccount account)
        {
            switchAccountButton.SetAccountImage (account);
        }

        protected override void ConfigureAndLayout ()
        {
            switchAccountButton.SetAccountImage (NcApplication.Instance.Account);
        }

        protected override void Cleanup ()
        {
        }

        // INachoAccountsTableDelegate
        public void AccountSelected (McAccount account)
        {
            View.EndEditing (true);
            PerformSegue ("SegueToAccountSettings", new SegueHolder (account));
        }

        // INachoAccountsTableDelegate
        public void AddAccountSelected ()
        {
            View.EndEditing (true);
            LaunchViewController.StartAccountSetup (this);
        }

        // INachoAccountsTableDelegate
        public void SettingsSelected (McAccount account)
        {
            NcAssert.CaseError ();
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToAccountSettings")) {
                var h = (SegueHolder)sender;
                var account = (McAccount)h.value;
                var vc = (AccountSettingsViewController)segue.DestinationViewController;
                vc.SetAccount (account);
                return;
            }
            if (segue.Identifier.Equals ("SegueToLaunch")) {
                return;
            }
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }
    }
}
