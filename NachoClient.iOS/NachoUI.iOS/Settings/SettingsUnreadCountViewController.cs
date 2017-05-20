//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using CoreGraphics;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class SettingsUnreadCountViewController : NachoTableViewController, ThemeAdopter
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
            TableView.RegisterClassForCellReuse (typeof (OptionCell), OptionCellIdentifier);
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (theme != adoptedTheme) {
                adoptedTheme = theme;
                TableView.BackgroundColor = theme.TableViewGroupedBackgroundColor;
                TableView.AdoptTheme (theme);
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            AdoptTheme (Theme.Active);
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
            var cell = tableView.DequeueReusableCell (OptionCellIdentifier) as OptionCell;
            var option = Options [indexPath.Row];
            cell.TextLabel.Text = option.Name;
            if (EmailHelper.HowToDisplayUnreadCount () == option.Option) {
                cell.AccessoryView = new CheckmarkAccessoryView ();
            } else {
                cell.AccessoryView = null;
            }
            return cell;
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, Foundation.NSIndexPath indexPath)
        {
            base.WillDisplay (tableView, cell, indexPath);
            var themed = cell as ThemeAdopter;
            if (themed != null && adoptedTheme != null) {
                themed.AdoptTheme (adoptedTheme);
            }
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var option = Options [indexPath.Row];
            EmailHelper.SetHowToDisplayUnreadCount (option.Option);
            UpdateCheckmark (indexPath);
            NavigationController.PopViewController (true);
            (UIApplication.SharedApplication.Delegate as AppDelegate).UpdateBadgeCount ();
        }

        void UpdateCheckmark (Foundation.NSIndexPath selectedIndexPath)
        {
            SwipeTableViewCell cell;
            foreach (var indexPath in TableView.IndexPathsForVisibleRows) {
                cell = TableView.CellAt (indexPath) as SwipeTableViewCell;
                if (indexPath.Equals (selectedIndexPath)) {
                    if (cell.AccessoryView == null) {
                        cell.AccessoryView = new CheckmarkAccessoryView ();
                    }
                } else {
                    if (cell.AccessoryView != null) {
                        cell.AccessoryView = null;
                    }
                }
            }
        }

        private class CheckmarkAccessoryView : ImageAccessoryView
        {
            public CheckmarkAccessoryView () : base ("checkmark-accessory")
            {
            }
        }

        private class OptionCell : SwipeTableViewCell, ThemeAdopter
        {

            public OptionCell (IntPtr ptr) : base(ptr)
            {
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (14.0f);
                TextLabel.TextColor = theme.TableViewCellMainLabelTextColor;
                SetNeedsLayout ();
            }
        }
    }
}

