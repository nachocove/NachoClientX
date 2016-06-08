//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using CoreGraphics;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;

namespace NachoClient.iOS
{

    public interface AccountPickerViewControllerDelegate
    {
        void AccountPickerDidPickAccount (AccountPickerViewController vc, McAccount account);
    }

    public partial class AccountPickerViewController : NachoTableViewController
    {

        const string AccountCellIdentifier = "AccountCellIdentifier";

        List<McAccount> _Accounts;
        McAccount _SelectedAccount;

        public McAccount SelectedAccount {
            get {
                return _SelectedAccount;
            }
            set {
                _SelectedAccount = value;
                if (IsViewLoaded) {
                    TableView.ReloadData ();
                }
            }
        }

        public List<McAccount> Accounts {
            get {
                return _Accounts;
            }
            set {
                _Accounts = value;
                if (IsViewLoaded) {
                    TableView.ReloadData ();
                }
            }
        }
        public AccountPickerViewControllerDelegate PickerDelegate;

        public AccountPickerViewController () : base (UITableViewStyle.Grouped)
        {
            NavigationItem.Title = "Choose Account";

        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
            TableView.RowHeight = AccountPickerTableViewCell.PreferredHeight;
            TableView.RegisterClassForCellReuse (typeof(AccountPickerTableViewCell), AccountCellIdentifier);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return Accounts.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var account = Accounts [indexPath.Row];
            var cell = tableView.DequeueReusableCell (AccountCellIdentifier) as AccountPickerTableViewCell;
            cell.Account = account;
            if (account.Id == SelectedAccount.Id) {
                if (!(cell.AccessoryView is CheckmarkAccessoryView)) {
                    cell.AccessoryView = new CheckmarkAccessoryView ();
                }
            } else {
                cell.AccessoryView = null;
            }
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cellView = tableView.CellAt (indexPath) as AccountPickerTableViewCell;
            if (cellView != null) {
                if (cellView.AccessoryView == null) {
                    cellView.AccessoryView = new CheckmarkAccessoryView ();
                }
            }
            var row = 0;
            for (; row < Accounts.Count; ++row) {
                if (Accounts [row].Id == SelectedAccount.Id) {
                    break;
                }
            }
            cellView = tableView.CellAt (Foundation.NSIndexPath.FromRowSection(row, 0)) as AccountPickerTableViewCell;
            if (cellView != null) {
                cellView.AccessoryView = null;
            }
            SelectedAccount = Accounts [indexPath.Row];
            if (PickerDelegate != null) {
                PickerDelegate.AccountPickerDidPickAccount (this, SelectedAccount);
            }
        }

        private class CheckmarkAccessoryView : ImageAccessoryView
        {
            public CheckmarkAccessoryView () : base ("gen-checkbox-checked")
            {
            }
        }

    }

    public class AccountPickerTableViewCell : SwipeTableViewCell
    {

        McAccount _Account;
        public McAccount Account {
            get {
                return _Account;
            }
            set {
                _Account = value;
                Update ();
            }
        }

        UIImageView IconView;
        public static nfloat PreferredHeight = 64.0f;

        public AccountPickerTableViewCell (IntPtr handle) : base (handle)
        {
            IconView = new UIImageView (new CGRect(0.0f, 0.0f, 40.0f, 40.0f));
            DetailTextSpacing = 0.0f;
            IconView.ClipsToBounds = true;
            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            TextLabel.TextColor = A.Color_NachoGreen;
            DetailTextLabel.Font = A.Font_AvenirNextRegular14;
            DetailTextLabel.TextColor = A.Color_NachoTextGray;
            SeparatorInset = new UIEdgeInsets (0.0f, PreferredHeight, 0.0f, 0.0f);
            ContentView.AddSubview (IconView);
        }

        void Update ()
        {
            TextLabel.Text = Account.DisplayName;
            DetailTextLabel.Text = Account.EmailAddr;
            using (var image = Util.ImageForAccount (Account)) {
                IconView.Image = image;
            }
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            IconView.Center = new CGPoint (SeparatorInset.Left / 2.0f, ContentView.Bounds.Height / 2.0f);
            IconView.Layer.CornerRadius = IconView.Frame.Size.Width / 2.0f;
        }

    }
}


