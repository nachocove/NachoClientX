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

        bool showAccessory;
        INachoAccountsTableDelegate owner;

        nfloat ROW_HEIGHT;

        public void Setup (INachoAccountsTableDelegate owner, bool showAccessory)
        {
            this.owner = owner;
            this.showAccessory = showAccessory;
        }

        public AccountsTableViewSource ()
        {
            ROW_HEIGHT = 80 + A.Card_Vertical_Indent;
            accounts = McAccount.QueryByAccountType (McAccount.AccountTypeEnum.Exchange).ToList ();
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
            accountView.Configure (account, showAccessory);
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.CellAt (indexPath);
            cell.SetSelected (false, true);

            owner.AccountSelected (accounts [indexPath.Row]);
        }

        public override nfloat GetHeightForFooter (UITableView tableView, nint section)
        {
            if (!showAccessory) {
                return 0;
            }
            return 40;
        }

        public override UIView GetViewForFooter (UITableView tableView, nint section)
        {
            if (!showAccessory) {
                return new UIView ();
            }

            var newAccountView = new UIView (new CGRect (0, 0, tableView.Frame.Width, 40));
            newAccountView.BackgroundColor = A.Color_NachoBackgroundGray;

            var newAccountButton = UIButton.FromType (UIButtonType.System);
            newAccountButton.Layer.CornerRadius = A.Card_Corner_Radius;
            newAccountButton.Frame = ContentRectangle (tableView, 40);
            newAccountButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;

            newAccountButton.BackgroundColor = UIColor.White;
            newAccountButton.Font = A.Font_AvenirNextRegular14;
            newAccountButton.SetTitle ("Add Account", UIControlState.Normal);
            newAccountButton.SetTitleColor (A.Color_NachoBlack, UIControlState.Normal);

            Util.SetOriginalImagesForButton (newAccountButton, "email-add");
            newAccountButton.TitleEdgeInsets = new UIEdgeInsets (0, 12, 0, 36);
            newAccountButton.ImageEdgeInsets = new UIEdgeInsets (0, newAccountButton.Frame.Width - 36, 0, 0);
            newAccountButton.ContentEdgeInsets = new UIEdgeInsets ();

            newAccountButton.TouchUpInside += NewAccountButton_TouchUpInside;

            newAccountView.AddSubview (newAccountButton);
            return newAccountView;
        }

        void NewAccountButton_TouchUpInside (object sender, EventArgs e)
        {
            owner.AddAccount ();
        }
    }
}

