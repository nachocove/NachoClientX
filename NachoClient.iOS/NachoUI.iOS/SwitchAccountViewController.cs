// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;

using Foundation;
using UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoClient;

namespace NachoClient.iOS
{
    public partial class SwitchAccountViewController : UIViewController, IUIViewControllerTransitioningDelegate, INachoAccountsTableDelegate, AccountTypeViewControllerDelegate, AccountCredentialsViewControllerDelegate, AccountSyncingViewControllerDelegate
    {
        public SwitchAccountViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

        UITableView accountsTableView;
        SwitchAccountButton switchAccountButton;
        AccountsTableViewSource accountsTableViewSource;
        UIStoryboard accountStoryboard;

        public delegate void SwitchAccountCallback (McAccount account);

        SwitchAccountCallback switchAccountCallback;

        public static void ShowDropdown (UIViewController fromViewController, SwitchAccountCallback switchAccountCallback)
        {
            var storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var switchViewController = (SwitchAccountViewController)storyboard.InstantiateViewController ("SwitchAccountViewController");
            var navViewController = new UINavigationController (switchViewController);
            Util.ConfigureNavBar (false, navViewController);
            switchViewController.switchAccountCallback = switchAccountCallback;
            var segue = new SwitchAccountCustomSegue ("", fromViewController, navViewController);
            segue.Perform ();
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            switchAccountButton = new SwitchAccountButton (SwitchAccountButtonPressed);
            switchAccountButton.SetImage ("gen-avatar-backarrow");
            accountsTableView = new UITableView (View.Frame);
            accountsTableView.SeparatorColor = A.Color_NachoBackgroundGray;
            accountsTableView.BackgroundColor = A.Color_NachoBackgroundGray;
            accountsTableView.TableHeaderView = GetViewForHeader (accountsTableView);
            accountsTableView.TableFooterView = new AddAccountCell (new CGRect (0, 0, accountsTableView.Frame.Width, 80), AddAccountSelected);
            accountsTableViewSource = new AccountsTableViewSource ();
            accountsTableViewSource.Setup (this, showAccessory: false, showUnreadCount:true);
            accountsTableView.Source = accountsTableViewSource;
            View.AddSubview (accountsTableView);
            NavigationItem.TitleView = switchAccountButton;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ViewFramer.Create (accountsTableView).Y (0);
        }

        public override void ViewDidLayoutSubviews ()
        {
            base.ViewDidLayoutSubviews ();
            ViewFramer.Create (accountsTableView).Height (View.Frame.Height);
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

        // TitleView button
        void SwitchAccountButtonPressed ()
        {
            // No double presses
            switchAccountButton.UserInteractionEnabled = false;

            Deactivate (null, (McAccount account) => {
                DismissViewController (false, null);
            });
        }

        // INachoAccountsTableDelegate
        public void AccountSelected (McAccount account)
        {
            var spinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.Gray);
            spinner.Center = View.Center;
            View.AddSubview (spinner);
            spinner.StartAnimating ();
            spinner.HidesWhenStopped = true;
            NcApplication.Instance.Account = account;
            LoginHelpers.SetSwitchToTime (account);
            switchAccountCallback (account);
            Deactivate (null, (McAccount acct) => {
                UIView.Transition(UIApplication.SharedApplication.Delegate.GetWindow (), 0.75, UIViewAnimationOptions.TransitionFlipFromRight, () => {
                    DismissViewController (false, null);
                }, null);
            });
        }

        // INachoAccountsTableDelegate
        public void SettingsSelected (McAccount account)
        {
            PerformSegue ("SegueToAccountSettings", new SegueHolder (account));
        }

        // INachoAccountsTableDelegate
        public void AddAccountSelected ()
        {
            accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
            var vc = (AccountTypeViewController)accountStoryboard.InstantiateViewController ("AccountTypeViewController");
            vc.AccountDelegate = this;
            NavigationController.PushViewController (vc, true);
        }

        public void Deactivate (McAccount account, SwitchAccountCallback callback)
        {
            var shadeView = NavigationController.View.ViewWithTag (SwitchAccountCustomSegue.ShadeViewTag);
            View.Layer.ShadowColor = UIColor.Black.CGColor;
            View.Layer.ShadowOpacity = 0.4f;
            View.Layer.ShadowRadius = 10.0f;
            UIView.Animate (0.3, 0.0, UIViewAnimationOptions.CurveEaseOut, () => {
                if (shadeView != null){
                    shadeView.Alpha = 0.0f;
                }
                View.Transform = CGAffineTransform.MakeTranslation(0, -View.Frame.Height);
            }, () => {
                callback (account);
            });
        }

        protected const float LINE_HEIGHT = 20;

        UIView GetViewForHeader (UITableView tableView)
        {
            var headerView = new UIView (new CGRect (0, 0, tableView.Frame.Width, 0));
            headerView.BackgroundColor = UIColor.White;

            var accountInfoView = new UIView (new CGRect (A.Card_Horizontal_Indent, 0, tableView.Frame.Width - A.Card_Horizontal_Indent, 0));
            accountInfoView.BackgroundColor = UIColor.White;

            headerView.AddSubview (accountInfoView);

            // Create Views

            var accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
            accountInfoView.AddSubview (accountImageView);

            var userLabelView = new UILabel (new CGRect (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            accountInfoView.AddSubview (userLabelView);

            UILabel nameLabel = new UILabel (new CGRect (75, 20, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (nameLabel);

            UILabel accountEmailAddress = new UILabel (new CGRect (75, 40, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (accountEmailAddress);

            var settingsButton = Util.BlueButton ("Account Settings", 0);
            settingsButton.Frame = new CGRect (75, 70, 0, LINE_HEIGHT);
            settingsButton.SizeToFit ();
            ViewFramer.Create (settingsButton).AdjustWidth (A.Card_Horizontal_Indent);
            settingsButton.TouchUpInside += SettingsButtonTouchUpInside;
            accountInfoView.AddSubview (settingsButton);

            var unreadMessagesViewFrame = new CGRect (0, 120, accountInfoView.Frame.Width, 40);
            var unreadMessagesView = new UnreadMessagesView (unreadMessagesViewFrame, InboxClicked, DeadlinesClicked, DeferredClicked);
            accountInfoView.AddSubview (unreadMessagesView);

            var yOffset = unreadMessagesView.Frame.Bottom;

            ViewFramer.Create (headerView).Height (yOffset);
            ViewFramer.Create (accountInfoView).Height (yOffset);

            // Fill in views
            var account = NcApplication.Instance.Account;

            nameLabel.Text = Pretty.AccountName (account);
            using (var image = Util.ImageForAccount (account)) {
                accountImageView.Image = image;
            }
            accountEmailAddress.Text = account.EmailAddr;

            unreadMessagesView.Update (account);

            return headerView;
        }

        void SettingsButtonTouchUpInside (object sender, EventArgs e)
        {
            SettingsSelected (NcApplication.Instance.Account);
        }

        private void InboxClicked (object sender)
        {
            // No double presses
            switchAccountButton.UserInteractionEnabled = false;

            Deactivate (null, (McAccount account) => {
                DismissViewController (false, null);
                var nachoTabBar = Util.GetActiveTabBar ();
                nachoTabBar.SwitchToInbox ();
            });
        }

        private void DeferredClicked (object sender)
        {
            // No double presses
            switchAccountButton.UserInteractionEnabled = false;

            Deactivate (null, (McAccount account) => {
                DismissViewController (false, null);
                var nachoTabBar = Util.GetActiveTabBar ();
                nachoTabBar.SwitchToDeferred ();
            });
        }

        private void DeadlinesClicked (object sender)
        {
            // No double presses
            switchAccountButton.UserInteractionEnabled = false;

            Deactivate (null, (McAccount account) => {
                DismissViewController (false, null);
                var nachoTabBar = Util.GetActiveTabBar ();
                nachoTabBar.SwitchToDeadlines ();
            });
        }

        public void AccountTypeViewControllerDidSelectService (AccountTypeViewController vc, McAccount.AccountServiceEnum service)
        {
            var credentialsViewController = vc.SuggestedCredentialsViewController (service);
            credentialsViewController.Service = service;
            credentialsViewController.AccountDelegate = this;
            NavigationController.PushViewController (credentialsViewController, true);
        }

        public void AccountCredentialsViewControllerDidValidateAccount (AccountCredentialsViewController vc, McAccount account)
        {
            var syncingViewController = (AccountSyncingViewController)accountStoryboard.InstantiateViewController ("AccountSyncingViewController");
            syncingViewController.AccountDelegate = this;
            syncingViewController.Account = account;
            BackEnd.Instance.Start (syncingViewController.Account.Id);
            NavigationController.PushViewController (syncingViewController, true);
        }

        public void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc)
        {
            AccountSelected (vc.Account);
        }
    }

}
