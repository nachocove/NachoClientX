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
    public partial class GeneralSettingsViewController : NcUIViewControllerNoLeaks
    {
        protected nfloat yOffset;

        UITableView accountsTableView;
        AccountsTableViewSource accountsTableViewSource;

        public GeneralSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NavigationItem.Title = "Settings";
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
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            contentView.BackgroundColor = A.Color_NachoBackgroundGray;

            Util.ConfigureNavBar (false, this.NavigationController);

            yOffset = A.Card_Vertical_Indent;

            accountsTableViewSource = new AccountsTableViewSource ();
            accountsTableViewSource.owner = this;

            accountsTableView = new UITableView ();
            accountsTableView.BackgroundColor = A.Color_NachoBackgroundGray;

            accountsTableView.Source = accountsTableViewSource;

            var n = accountsTableViewSource.RowsInSection (accountsTableView, 0);
            var h = n * 80;

            accountsTableView.Frame = new CGRect (A.Card_Horizontal_Indent, yOffset, contentView.Frame.Width - (A.Card_Horizontal_Indent * 2), h);
            accountsTableView.Bounces = false;

            contentView.AddSubview (accountsTableView);

            yOffset = accountsTableView.Frame.Bottom + 30;

        }

        void NewAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            NachoPlatform.NcUIRedirector.Instance.GoBackToMainScreen ();                        
        }

        protected override void ConfigureAndLayout ()
        {
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            var contentFrame = new CGRect (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected override void Cleanup ()
        {

        }

        public void ShowAccount (McAccount account)
        {
            View.EndEditing (true);
            PerformSegue ("SegueToAccountSettings", new SegueHolder (account));
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
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }
    }
}
