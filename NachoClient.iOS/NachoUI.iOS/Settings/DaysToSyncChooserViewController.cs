//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class DaysToSyncChooserViewController : NachoTableViewController, ThemeAdopter
    {

        McAccount _Account;
        public McAccount Account {
            get {
                return _Account;
            }
            set {
                _Account = value;
                OriginalSetting = _Account.DaysToSyncEmail;
            }
        }

        NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode OriginalSetting;

        const string OptionCellIdentifier = "OptionCellIdentifier";

        private class SyncOption
        {
            public string Name;
            public NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode Option;

            public SyncOption (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode option)
            {
                Name = Pretty.MaxAgeFilter (option);
                Option = option;
            }
        }

        private List<SyncOption> Options;

        public DaysToSyncChooserViewController () : base (UITableViewStyle.Grouped)
        {
            NavigationItem.Title = NSBundle.MainBundle.LocalizedString ("Days to Sync (setting title)", "");

            Options = new List<SyncOption> (new SyncOption [] {
                new SyncOption (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.OneMonth_5),
                new SyncOption (NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode.SyncAll_0),
            });
        }

        public override void LoadView ()
        {
            base.LoadView ();
            TableView.RegisterClassForCellReuse (typeof (OptionCell), OptionCellIdentifier);
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (IsMovingFromParentViewController) {
                if (OriginalSetting != Account.DaysToSyncEmail) {
                    NcApplication.Instance.InvokeStatusIndEventInfo (Account, NcResult.SubKindEnum.Info_DaysToSyncChanged);
                }
            }
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
            if (Account.DaysToSyncEmail == option.Option) {
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
            Account.DaysToSyncEmail = option.Option;
            UpdateCheckmark (indexPath);
            Account.UpdateWithOCApply<McAccount> ((record) => {
                var account = record as McAccount;
                account.DaysToSyncEmail = Account.DaysToSyncEmail;
                return true;
            });
            NavigationController.PopViewController (true);
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

            public OptionCell (IntPtr ptr) : base (ptr)
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

