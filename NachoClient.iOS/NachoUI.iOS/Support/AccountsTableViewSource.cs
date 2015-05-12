//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using CoreFoundation;
using CoreGraphics;
using NachoCore.Model;
using UIKit;

namespace NachoClient.iOS
{
    public class AccountsTableViewSource : UITableViewSource
    {
        List<McAccount> accounts;

        public GeneralSettingsViewController owner;

        public AccountsTableViewSource ()
        {
            accounts = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).ToList ();
        }

        public override nfloat GetHeightForRow (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            return 80;
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return accounts.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            const string cellIdentifier = "id";

            var cell = tableView.DequeueReusableCell (cellIdentifier);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, cellIdentifier);
                cell.BackgroundColor = A.Color_NachoBackgroundGray;
                cell.ContentView.BackgroundColor = A.Color_NachoBackgroundGray;
            }
            var accountView = new AccountInfoView(new CGRect (0, 0, cell.Frame.Width, 80));
            cell.ContentView.AddSubview (accountView);

            var account = accounts [indexPath.Row];
            accountView.Configure (account);
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            owner.ShowAccount (accounts [indexPath.Row]);
        }
    }
}

