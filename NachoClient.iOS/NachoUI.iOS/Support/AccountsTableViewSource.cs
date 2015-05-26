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
        INachoAccountsTableDelegate owner;

        nfloat ROW_HEIGHT;
        protected const float LINE_HEIGHT = 20;

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
            owner.AddAccountSelected ();
        }

        public override nfloat GetHeightForHeader (UITableView tableView, nint section)
        {
            if (showAccessory) {
                return 0;
            }
            return 240;
        }

        public override UIView GetViewForHeader (UITableView tableView, nint section)
        {
            if (showAccessory) {
                return new UIView ();
            }

            var headerView = new UIView (new CGRect (0, 0, tableView.Frame.Width, 0));
            headerView.BackgroundColor = UIColor.White;

            var accountInfoView = new UIView (new CGRect (A.Card_Horizontal_Indent, 0, tableView.Frame.Width - A.Card_Horizontal_Indent, 0));
            accountInfoView.BackgroundColor = UIColor.White;

            headerView.AddSubview (accountInfoView);

            // Create Views

            var accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
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

            var settingsButton = new UIButton (new CGRect (75, 70, 0, LINE_HEIGHT));
            settingsButton.BackgroundColor = A.Color_NachoSubmitButton;
            settingsButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            settingsButton.SetTitle ("Account Settings", UIControlState.Normal);
            settingsButton.TitleLabel.TextColor = UIColor.White;
            settingsButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            settingsButton.Layer.CornerRadius = 4f;
            settingsButton.Layer.MasksToBounds = true;
            settingsButton.TouchUpInside += SettingsButtonTouchUpInside;
            settingsButton.AccessibilityLabel = "Account Settings";
            settingsButton.SizeToFit ();
            ViewFramer.Create (settingsButton).AdjustWidth (A.Card_Horizontal_Indent);
            accountInfoView.AddSubview (settingsButton);

            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 120, tableView.Frame.Width, A.Color_NachoBorderGray, accountInfoView);
            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 160, tableView.Frame.Width, A.Color_NachoBorderGray, accountInfoView);
            Util.AddHorizontalLine (-A.Card_Horizontal_Indent, 200, tableView.Frame.Width, A.Color_NachoBorderGray, accountInfoView);

            var unreadMessages = new UcIconLabelPair (new CGRect (0, 120, tableView.Frame.Width, 40), "gen-inbox", 0, 15, null);
            var deadlineMessages = new UcIconLabelPair (new CGRect (0, 160, tableView.Frame.Width, 40), "gen-deadline", 0, 15, null);
            var deferredMessages = new UcIconLabelPair (new CGRect (0, 200, tableView.Frame.Width, 40), "gen-deferred-msgs", 0, 15, null);

            accountInfoView.AddSubviews (new UIView[] { unreadMessages, deferredMessages, deadlineMessages, });

            var yOffset = 240;

            ViewFramer.Create (headerView).Height (yOffset);
            ViewFramer.Create (accountInfoView).Height (yOffset);

            // Fill in views
            var account = NcApplication.Instance.Account;

            nameLabel.Text = Pretty.AccountName (account);
            using (var image = Util.ImageForAccount (account)) {
                accountImageView.Image = image;
            }
            accountEmailAddress.Text = account.EmailAddr;

            var inboxFolder = NcEmailManager.InboxFolder (account.Id);
            var unreadInboxMessagesCount = 0;
            if (null != inboxFolder) {
                unreadInboxMessagesCount = McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id);
            }
            unreadMessages.SetValue (Pretty.MessageCount ("unread message", unreadInboxMessagesCount));

            var deadlineMessageCount = 0;
            if (null != inboxFolder) {
                deadlineMessageCount = McEmailMessage.QueryDueDateMessageItems (inboxFolder.AccountId).Count;
            }
            deadlineMessages.SetValue (Pretty.MessageCount ("deadline", deadlineMessageCount));

            var deferredMessageCount = 0;
            if (null != inboxFolder) {
                deferredMessageCount = new NachoDeferredEmailMessages (inboxFolder.AccountId).Count ();
            }
            deferredMessages.SetValue (Pretty.MessageCount ("deferred message", deferredMessageCount));

            return headerView;
        }

        void SettingsButtonTouchUpInside (object sender, EventArgs e)
        {
            owner.SettingsSelected (NcApplication.Instance.Account);
        }
    }
}

