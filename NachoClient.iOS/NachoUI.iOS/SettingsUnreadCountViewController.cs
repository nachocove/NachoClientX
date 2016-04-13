//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class SettingsUnreadCountViewController : NachoTableViewController
    {

        const string OptionCellIdentifier = "OptionCellIdentifier";

        private class UnreadOption
        {
            public string Name;
            public EmailHelper.ShowUnreadEnum Option;

            public UnreadOption (string name, EmailHelper.ShowUnreadEnum option)
            {
                Name = name;
                Option = option;
            }
        }

        private List<UnreadOption> Options;

        public SettingsUnreadCountViewController () : base (UITableViewStyle.Grouped)
        {
            NavigationItem.Title = "Unread Count";

            Options = new List<UnreadOption> (new UnreadOption[] {
                new UnreadOption ("All Messages", EmailHelper.ShowUnreadEnum.AllMessages),
                new UnreadOption ("Recent Messages", EmailHelper.ShowUnreadEnum.RecentMessages),
                new UnreadOption ("Today's Messages", EmailHelper.ShowUnreadEnum.TodaysMessages),
            });
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof (SwipeTableViewCell), OptionCellIdentifier);
            TableView.BackgroundColor = A.Color_NachoBackgroundGray;
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        public override nint RowsInSection (UITableView tableView, nint section)
        {
            return Options.Count;
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (OptionCellIdentifier) as SwipeTableViewCell;
            cell.TextLabel.Font = A.Font_AvenirNextMedium14;
            cell.TextLabel.TextColor = A.Color_NachoGreen;
            var option = Options [indexPath.Row];
            cell.TextLabel.Text = option.Name;
            if (EmailHelper.HowToDisplayUnreadCount () == option.Option) {
                cell.AccessoryView = new CheckmarkAccessoryView ();
            } else {
                cell.AccessoryView = null;
            }
            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var option = Options [indexPath.Row];
            EmailHelper.SetHowToDisplayUnreadCount (option.Option);
            TableView.ReloadData ();
            NavigationController.PopViewController (true);
            (UIApplication.SharedApplication.Delegate as AppDelegate).UpdateBadgeCount ();
        }

        private class CheckmarkAccessoryView : ImageAccessoryView
        {
            public CheckmarkAccessoryView () : base ("gen-checkbox-checked")
            {
            }
        }
    }
}

