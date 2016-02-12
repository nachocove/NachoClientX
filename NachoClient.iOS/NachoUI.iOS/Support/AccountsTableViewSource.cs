//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using CoreFoundation;
using CoreGraphics;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using UIKit;

namespace NachoClient.iOS
{
    public class AccountsTableViewSource : UITableViewSource
    {
        List<McAccount> accounts;

        bool showAccessory;
        bool showUnreadCount;
        bool showUnified;
        INachoAccountsTableDelegate owner;

        nfloat ROW_HEIGHT;

        public void Setup (INachoAccountsTableDelegate owner, bool showAccessory, bool showUnreadCount, bool showUnified = true)
        {
            this.owner = owner;
            this.showAccessory = showAccessory;
            this.showUnreadCount = showUnreadCount;
            this.showUnified = showUnified;

            Refresh ();
        }

        public void Refresh ()
        {
            accounts = new List<McAccount> ();

            McAccount unifiedAccount = null;

            foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                if (account.AccountType == McAccount.AccountTypeEnum.Unified) {
                    unifiedAccount = account;
                } else if (McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress) {
                    accounts.Add (account);
                }
            }

            // Remove the device account (for now)
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null != deviceAccount) {
                accounts.RemoveAll ((McAccount account) => (account.Id == deviceAccount.Id));
            }

            if (showUnified && accounts.Count > 1) {
                if (unifiedAccount == null) {
                    unifiedAccount = McAccount.GetUnifiedAccount ();
                }
                accounts.Insert (0, unifiedAccount);
            }

            // Remove the current account from the switcher view.
            if (!showAccessory) {
                accounts.RemoveAll ((McAccount account) => (account.Id == NcApplication.Instance.Account.Id));
            }
        }

        public AccountsTableViewSource ()
        {
            ROW_HEIGHT = 80 + A.Card_Vertical_Indent;
        }

        CGRect ContentRectangle (UITableView tablView, nfloat height)
        {
            return new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, tablView.Frame.Width - 2 * A.Card_Horizontal_Indent, height);
        }

        public override nfloat GetHeightForRow (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            return ROW_HEIGHT;
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
            var accountView = new AccountInfoView (ContentRectangle (tableView, 80));
            cell.ContentView.AddSubview (accountView);

            var account = accounts [indexPath.Row];
            accountView.Configure (account, showAccessory, showUnreadCount);
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.CellAt (indexPath);
            cell.SetSelected (false, true);

            owner.AccountSelected (accounts [indexPath.Row]);
        }
    }
}

