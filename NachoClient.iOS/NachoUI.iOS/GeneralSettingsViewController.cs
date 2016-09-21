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
    public partial class GeneralSettingsViewController : NachoTableViewController, AccountTypeViewControllerDelegate, AccountCredentialsViewControllerDelegate, AccountSyncingViewControllerDelegate
    {

        #region Constants

        const string NameValueCellIdentifier = "NameValueCellIdentifier";
        const string AccountCellIdentifier = "AccountCellIdentifier";
        const string ButttonCellIdentifier = "ButtonCellIdentifier";

        const int SectionGeneralSettings = 0;
        const int SectionAccounts = 1;
        const int SectionAbout = 2;

        const int GeneralSettingsRowUnreadCount = 0;

        const int AccountsExtraRowAddAccount = 0;
        const int AccountsExtraRowConnectToSalesforce = 1;

        const int AboutRowAbout = 0;

        #endregion

        #region Propreties

        List<McAccount> Accounts;
        bool HasSalesforce;
        UINavigationController AddAccountNavigationController;

        #endregion

        #region Constructors

        public GeneralSettingsViewController () : base (UITableViewStyle.Grouped)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
            NavigationItem.Title = "Settings";
        }

        #endregion

        #region View Lifecycle

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.RegisterClassForCellReuse (typeof (AccountCell), AccountCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof (NameValueCell), NameValueCellIdentifier);
            TableView.RegisterClassForCellReuse (typeof (ButtonCell), ButttonCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ReloadAccounts ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            // Refresh the ! on the status line
            LoginHelpers.UserInterventionStateChanged (NcApplication.Instance.Account.Id);
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        #endregion

        #region Table Delegate & Data Source

        void ReloadAccounts ()
        {
            Accounts = McAccount.GetAllConfiguredNormalAccounts ();
            var salesforceAccount = McAccount.GetSalesForceAccount ();
            if (salesforceAccount != null) {
                HasSalesforce = true;
                Accounts.Add (salesforceAccount);
            } else {
                HasSalesforce = false;
            }
            TableView.ReloadData ();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 3;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            if (section == SectionGeneralSettings) {
                return 1;
            }
            if (section == SectionAccounts) {
                return Accounts.Count + 1;
            }
            if (section == SectionAbout) {
                return 1;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("NcAssert.CaseError: GeneralSettingsViewController.RowsInSection unknown table section {0}", section));
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionGeneralSettings) {
                return NameValueCell.PreferredHeight;
            }
            if (indexPath.Section == SectionAccounts) {
                if (indexPath.Row < Accounts.Count) {
                    return AccountCell.PreferredHeight;
                }
                return ButtonCell.PreferredHeight;
            }
            if (indexPath.Section == SectionAbout) {
                return NameValueCell.PreferredHeight;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("NcAssert.CaseError: GeneralSettingsViewController.GetHeightForRow unknown table section {0}", indexPath.Section));
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            if (section == SectionAccounts) {
                return AccountsHeader.PreferredHeight;
            }
            return 0.0f;
        }

        private InsetLabelView _AccountsHeader;
        private InsetLabelView AccountsHeader {
            get {
                if (_AccountsHeader == null) {
                    _AccountsHeader = new InsetLabelView ();
                    _AccountsHeader.LabelInsets = new UIEdgeInsets (5.0f, GroupedCellInset + 6.0f, 5.0f, GroupedCellInset);
                    _AccountsHeader.Label.Text = "Accounts";
                    _AccountsHeader.Label.Font = A.Font_AvenirNextRegular14;
                    _AccountsHeader.Label.TextColor = TableView.BackgroundColor.ColorDarkenedByAmount (0.6f);
                    _AccountsHeader.Frame = new CGRect (0.0f, 0.0f, 100.0f, 20.0f);
                }
                return _AccountsHeader;
            }
        }
        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (section == SectionAccounts) {
                return AccountsHeader;
            }
            return null;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionGeneralSettings) {
                if (indexPath.Row == GeneralSettingsRowUnreadCount) {
                    var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                    cell.TextLabel.Text = "Unread Count";
                    cell.ValueLabel.Text = ValueForUnreadCount ();
                    if ((cell.AccessoryView as DisclosureAccessoryView) == null) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                }
            } else if (indexPath.Section == SectionAccounts) {
                if (indexPath.Row < Accounts.Count) {
                    var cell = tableView.DequeueReusableCell (AccountCellIdentifier) as AccountCell;
                    var account = Accounts [indexPath.Row];
                    cell.TextLabel.Text = account.DisplayName;
                    cell.DetailTextLabel.Text = account.EmailAddr;
                    cell.IndicateError = LoginHelpers.ShouldAlertUser (account.Id);
                    using (var image = Util.ImageForAccount (account)) {
                        cell.AccountImageView.Image = image;
                    }
                    if ((cell.AccessoryView as DisclosureAccessoryView) == null) {
                        cell.AccessoryView = new DisclosureAccessoryView ();
                    }
                    return cell;
                } else {
                    var actionRow = indexPath.Row - Accounts.Count;
                    var cell = tableView.DequeueReusableCell (ButttonCellIdentifier) as ButtonCell;
                    //                    cell.SeparatorInset = new UIEdgeInsets (0.0f, AccountCell.PreferredHeight, 0.0f, 0.0f);
                    if (actionRow == AccountsExtraRowAddAccount) {
                        cell.TextLabel.Text = "Add Account";
                        if ((cell.AccessoryView as AddAccessoryView) == null) {
                            cell.AccessoryView = new AddAccessoryView ();
                        }
                        return cell;
                    } else if (actionRow == AccountsExtraRowConnectToSalesforce) {
                        cell.TextLabel.Text = "Connect to Salesforce";
                        if ((cell.AccessoryView as AddAccessoryView) == null) {
                            cell.AccessoryView = new AddAccessoryView ();
                        }
                        return cell;
                    }
                }
            } else if (indexPath.Section == SectionAbout){
                var cell = tableView.DequeueReusableCell (NameValueCellIdentifier) as NameValueCell;
                cell.TextLabel.Text = "About Apollo Mail";
                cell.ValueLabel.Text = "";
                if ((cell.AccessoryView as DisclosureAccessoryView) == null) {
                    cell.AccessoryView = new DisclosureAccessoryView ();
                }
                return cell;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("NcAssert.CaseError: GeneralSettingsViewController.GetCell unknown table row {0}.{1}", indexPath.Section, indexPath.Row));
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == SectionGeneralSettings) {
                if (indexPath.Row == GeneralSettingsRowUnreadCount) {
                    var vc = new SettingsUnreadCountViewController ();
                    NavigationController.PushViewController (vc, true);
                }
            } else if (indexPath.Section == SectionAccounts) {
                if (indexPath.Row < Accounts.Count) {
                    var account = Accounts [indexPath.Row];
                    if (account.AccountType == McAccount.AccountTypeEnum.SalesForce) {
                        ShowSalesforceAccount (account);
                    } else {
                        ShowAccount (account);
                    }
                } else {
                    var actionRow = indexPath.Row - Accounts.Count;
                    if (actionRow == AccountsExtraRowAddAccount) {
                        AddAccount ();
                    } else if (actionRow == AccountsExtraRowConnectToSalesforce) {
                        ConnectToSalesforce ();
                    }
                }
            } else if (indexPath.Section == SectionAbout) {
                if (indexPath.Row == AboutRowAbout) {
                    ShowAbout ();
                }
            }
        }

        #endregion

        #region Private Helpers

        private void ShowAbout ()
        {
            var vc = new AboutUsViewController ();
            NavigationController.PushViewController (vc, true);
        }

        private void ShowAccount (McAccount account)
        {
            var vc = new AccountSettingsViewController ();
            vc.SetAccount (account);
            NavigationController.PushViewController (vc, true);
        }

        private void ShowSalesforceAccount (McAccount account)
        {
            var vc = new SalesforceSettingsViewController ();
            vc.SetAccount (account);
            NavigationController.PushViewController (vc, true);
        }

        private void AddAccount ()
        {
            View.EndEditing (true);
            var accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
            var vc = (AccountTypeViewController)accountStoryboard.InstantiateViewController ("AccountTypeViewController");
            using (var image = UIImage.FromBundle ("modal-close")) {
                vc.NavigationItem.LeftBarButtonItem = new UIBarButtonItem (image, UIBarButtonItemStyle.Plain, (object sender, EventArgs e) => {
                    DismissViewController(true, () => {
                        AddAccountNavigationController = null;
                    });
                });
            }
            vc.AccountDelegate = this;
            AddAccountNavigationController = new UINavigationController (vc);
            PresentViewController (AddAccountNavigationController, true, null);
        }

        private void ConnectToSalesforce ()
        {
            var accountStoryboard = UIStoryboard.FromName ("AccountCreation", null);
            var credentialsViewController = (SalesforceCredentialsViewController)accountStoryboard.InstantiateViewController ("SalesforceCredentialsViewController");
            credentialsViewController.Service = McAccount.AccountServiceEnum.SalesForce;
            credentialsViewController.AccountDelegate = this;
            var closeButton = new NcUIBarButtonItem ();
            Util.SetAutomaticImageForButton (closeButton, "icn-close");
            closeButton.AccessibilityLabel = "Close";
            closeButton.Clicked += (object sender, EventArgs e) => { 
                credentialsViewController.Cancel ();
                DismissViewController(true, null); 
            };
            credentialsViewController.NavigationItem.LeftBarButtonItem = closeButton;
            var navigationController = new UINavigationController (credentialsViewController);
            PresentViewController (navigationController, true, null);
        }

        #endregion

        #region Account Creation Delegate

        public void AccountTypeViewControllerDidSelectService (AccountTypeViewController vc, McAccount.AccountServiceEnum service)
        {
            var credentialsViewController = vc.SuggestedCredentialsViewController (service);
            credentialsViewController.Service = service;
            credentialsViewController.AccountDelegate = this;
            AddAccountNavigationController.PushViewController (credentialsViewController, true);
        }

        public void AccountCredentialsViewControllerDidValidateAccount (AccountCredentialsViewController vc, McAccount account)
        {
            if (account.AccountService == McAccount.AccountServiceEnum.SalesForce) {
                BackEnd.Instance.Start (account.Id);
                DismissViewController (true, () => {
                    ShowSalesforceAccount (account);
                });
            }else{
                var syncingViewController = (AccountSyncingViewController)vc.Storyboard.InstantiateViewController ("AccountSyncingViewController");
                syncingViewController.AccountDelegate = this;
                syncingViewController.Account = account;
                BackEnd.Instance.Start (syncingViewController.Account.Id);
                AddAccountNavigationController.PushViewController (syncingViewController, true);
            }
        }

        public void AccountSyncingViewControllerDidComplete (AccountSyncingViewController vc)
        {
            ReloadAccounts ();
            DismissViewController (true, () => {
                AddAccountNavigationController = null;
            });
        }

        #endregion

        #region System Events

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_AccountSetChanged == s.Status.SubKind) {
                ReloadAccounts ();
            }
        }

        #endregion

        #region Cells

        private class DisclosureAccessoryView : ImageAccessoryView
        {
            public DisclosureAccessoryView () : base ("gen-more-arrow")
            {
            }
        }

        private class AddAccessoryView : ImageAccessoryView
        {
            public AddAccessoryView () : base ("email-add")
            {
            }
        }

        private class AccountCell : SwipeTableViewCell
        {

            public readonly UIImageView AccountImageView;
            public static nfloat PreferredHeight = 72.0f;
            private ErrorIndicatorView ErrorIndicator;
            private bool _IndicateError;
            nfloat ImageSize = 40.0f;
            nfloat ErrorIndicatorSize = 24.0f;

            public AccountCell (IntPtr handle) : base (handle)
            {
                AccountImageView = new UIImageView (new CGRect(0.0f, 0.0f, ImageSize, ImageSize));
                AccountImageView.ClipsToBounds = true;
                AccountImageView.Layer.CornerRadius = ImageSize / 2.0f;
                ContentView.AddSubview(AccountImageView);

                TextLabel.Font = A.Font_AvenirNextDemiBold14;
                TextLabel.TextColor = A.Color_NachoBlack;

                DetailTextLabel.Font = A.Font_AvenirNextRegular14;
                DetailTextLabel.TextColor = A.Color_NachoTextGray;

                SeparatorInset = new UIEdgeInsets(0.0f, PreferredHeight, 0.0f, 0.0f);
            }

            public bool IndicateError {
                get{
                    return _IndicateError;
                }
                set{
                    _IndicateError = value;
                    if (_IndicateError && ErrorIndicator == null) {
                        ErrorIndicator = new ErrorIndicatorView (ErrorIndicatorSize);
                        ContentView.AddSubview (ErrorIndicator);
                    }
                    if (ErrorIndicator != null) {
                        ErrorIndicator.Hidden = !_IndicateError;
                        SetNeedsLayout ();
                    }
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                var imagePadding = (ContentView.Bounds.Height - AccountImageView.Bounds.Height) / 2.0f;
                AccountImageView.Frame = new CGRect (imagePadding, imagePadding, AccountImageView.Frame.Width, AccountImageView.Frame.Height);
                if (ErrorIndicator != null && !ErrorIndicator.Hidden) {
                    ErrorIndicator.Center = new CGPoint (ContentView.Bounds.Width - ErrorIndicator.Frame.Width / 2.0f, ContentView.Bounds.Height / 2.0f);
                    var frame = TextLabel.Frame;
                    frame.Width -= ErrorIndicator.Frame.Width;
                    TextLabel.Frame = frame;
                    frame = DetailTextLabel.Frame;
                    frame.Width -= ErrorIndicator.Frame.Width;
                    DetailTextLabel.Frame = frame;
                }
            }
        }

        private class ButtonCell : SwipeTableViewCell
        {

            public static nfloat PreferredHeight = 44.0f;

            public ButtonCell (IntPtr handle) : base (handle)
            {
                TextLabel.Font = A.Font_AvenirNextRegular14;
                TextLabel.TextColor = A.Color_NachoGreen;
            }
        }

        #endregion

        protected string ValueForUnreadCount ()
        {
            switch (EmailHelper.HowToDisplayUnreadCount ()) {
            case EmailHelper.ShowUnreadEnum.AllMessages:
                return "All Messages";
            case EmailHelper.ShowUnreadEnum.RecentMessages:
                return "Recent Messages";
            case EmailHelper.ShowUnreadEnum.TodaysMessages:
                return "Today's Messages";
            default:
                NcAssert.CaseError ();
                return "";
            }
        }

    }
}
