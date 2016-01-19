//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;

namespace NachoClient.iOS
{

    public interface AccountPickerViewControllerDelegate
    {
        void AccountPickerDidPickAccount (AccountPickerViewController vc, McAccount account);
    }

    public partial class AccountPickerViewController : NcUITableViewController
    {

        public McAccount SelectedAccount {
            get {
                return Source.SelectedAccount;
            }
            set {
                Source.SelectedAccount = value;
                if (IsViewLoaded) {
                    TableView.ReloadData ();
                }
            }
        }

        public List<McAccount> Accounts {
            get {
                return Source.Accounts;
            }
            set {
                Source.Accounts = value;
                if (IsViewLoaded) {
                    TableView.ReloadData ();
                }
            }
        }
        AccountPickerTableViewSource Source;
        public AccountPickerViewControllerDelegate PickerDelegate;

        public AccountPickerViewController () : base (UITableViewStyle.Grouped)
        {
            Source = new AccountPickerTableViewSource ();
            Source.ViewController = this;
            NavigationItem.Title = "Choose Account";

        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RowHeight = 54.0f;
            TableView.Source = Source;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            // Perform any additional setup after loading the view, typically from a nib.
        }

        public override void DidReceiveMemoryWarning ()
        {
            base.DidReceiveMemoryWarning ();
            // Release any cached data, images, etc that aren't in use.
        }
    }

    public class AccountPickerTableViewSource : UITableViewSource
    {

        public AccountPickerViewController ViewController;
        public McAccount SelectedAccount;
        public List<McAccount> Accounts;

        public AccountPickerTableViewSource ()
        {
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
            var cell = tableView.DequeueReusableCell ("account") as AccountPickerTableViewCell;
            if (cell == null) {
                cell = new AccountPickerTableViewCell ("account");
            }
            cell.Account = account;
            cell.Accessory = account.Id == SelectedAccount.Id ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cellView = tableView.CellAt (indexPath);
            if (cellView != null) {
                cellView.Accessory = UITableViewCellAccessory.Checkmark;
            }
            var row = 0;
            for (; row < Accounts.Count; ++row) {
                if (Accounts [row].Id == SelectedAccount.Id) {
                    break;
                }
            }
            cellView = tableView.CellAt (Foundation.NSIndexPath.FromRowSection(row, 0));
            if (cellView != null) {
                cellView.Accessory = UITableViewCellAccessory.None;
            }
            SelectedAccount = Accounts [indexPath.Row];
            if (ViewController.PickerDelegate != null) {
                ViewController.PickerDelegate.AccountPickerDidPickAccount (ViewController, SelectedAccount);
            }
        }
        
    }

    public class AccountPickerTableViewCell : UITableViewCell
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

        public AccountPickerTableViewCell (string reuseIdentifier) : base (UITableViewCellStyle.Subtitle, reuseIdentifier)
        {
            ImageView.ClipsToBounds = true;
            TextLabel.Font = A.Font_AvenirNextDemiBold17;
            DetailTextLabel.Font = A.Font_AvenirNextMedium14;
        }

        void Update ()
        {
            TextLabel.Text = Account.DisplayName;
            DetailTextLabel.Text = Account.EmailAddr;
            using (var image = Util.ImageForAccount (Account)) {
                ImageView.Image = image;
            }
        }

        public override void LayoutSubviews ()
        {
            var imageReduction = 10.0f;
            base.LayoutSubviews ();
            ImageView.Frame = new CoreGraphics.CGRect(
                ImageView.Frame.X + imageReduction / 2.0f,
                ImageView.Frame.Y + imageReduction / 2.0f,
                ImageView.Frame.Width - imageReduction,
                ImageView.Frame.Height - imageReduction
            );
            ImageView.Layer.CornerRadius = ImageView.Frame.Size.Width / 2.0f;
        }

    }
}


