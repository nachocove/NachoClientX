// This file has been autogenerated from a class added in the UI designer.

using System;

using Foundation;
using UIKit;

using System.IO;
using System.Linq;
using CoreGraphics;
using System.Collections.Generic;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class NotificationChooserViewController : NcUIViewControllerNoLeaks
    {
        UITableView tableView;
        NotificationChoicesSource source;

        UIBarButtonItem doneButton;
        UIBarButtonItem cancelButton;

        int accountId;
        OldAccountSettingsViewController owner;
        McAccount.NotificationConfigurationEnum value;

        public NotificationChooserViewController (IntPtr handle) : base (handle)
        {
        }

        public NotificationChooserViewController () : base ()
        {
        }

        public void Setup (OldAccountSettingsViewController owner, int accountId, McAccount.NotificationConfigurationEnum value)
        {
            this.owner = owner;
            this.value = value;
            this.accountId = accountId;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
        }

        protected override void CreateViewHierarchy ()
        {
            tableView = new UITableView (View.Frame, UITableViewStyle.Grouped);
            source = new NotificationChoicesSource (this);
            tableView.Source = source;
            tableView.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Notifications (setting title)", "Title for notification setting picker");

            View.Add (tableView);

            NavigationItem.Title = NSBundle.MainBundle.LocalizedString ("Notifications (setting title)", "Title for notification setting picker");
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            doneButton = new NcUIBarButtonItem ();
            cancelButton = new NcUIBarButtonItem ();

            doneButton.Title = NSBundle.MainBundle.LocalizedString ("Save", "");
            doneButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Save", "");

            Util.SetAutomaticImageForButton (cancelButton, "icn-close");
            cancelButton.AccessibilityLabel = NSBundle.MainBundle.LocalizedString ("Cancel", "");

            cancelButton.Clicked += (sender, e) => {
                NavigationController.PopViewController (true);
            };

            doneButton.Clicked += (sender, e) => {
                owner.UpdateNotificationConfiguration (accountId, value);
                NavigationController.PopViewController (true);
            };

            NavigationItem.LeftBarButtonItem = cancelButton;
            NavigationItem.RightBarButtonItem = doneButton;
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
            doneButton = null;
            cancelButton = null;
            tableView.Source = null;
            tableView.Dispose ();
            tableView = null;
            source = null;
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        public bool IsChoiceSet (McAccount.NotificationConfigurationEnum choice)
        {
            if (0 == choice) {
                return (0 == value);
            } else {
                return (choice == (value & choice));
            }
        }

        public void SetChoice (McAccount.NotificationConfigurationEnum choice)
        {
            if (0 == choice) {
                value = 0;
            } else {
                value = value ^ choice;
            }
        }

        protected class NotificationChoicesSource : UITableViewSource
        {
            NotificationChooserViewController owner;

            List<McAccount.NotificationConfigurationEnum> choices = new List<McAccount.NotificationConfigurationEnum> () {
                0,
                McAccount.NotificationConfigurationEnum.ALLOW_HOT_2,
                McAccount.NotificationConfigurationEnum.ALLOW_VIP_4,
                McAccount.NotificationConfigurationEnum.ALLOW_INBOX_64,
            };

            public NotificationChoicesSource (NotificationChooserViewController owner)
            {
                this.owner = owner;
            }

            public override nint NumberOfSections (UITableView tableView)
            {
                return 1;
            }

            public override nint RowsInSection (UITableView tableview, nint section)
            {
                return choices.Count;
            }

            public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
            {
                const string cellId = "Choice";

                var cell = tableView.DequeueReusableCell (cellId);
                if (null == cell) {
                    cell = new UITableViewCell (UITableViewCellStyle.Default, cellId);
                }
                var choice = choices [indexPath.Row];
                cell.TextLabel.TextColor = A.Color_NachoDarkText;
                cell.TextLabel.Font = A.Font_AvenirNextMedium14;
                cell.SelectionStyle = UITableViewCellSelectionStyle.None;
                cell.TextLabel.Text = Pretty.NotificationConfiguration (choice);
                ConfigureCell (cell, choice);
                return cell;
            }

            protected void ConfigureCell (UITableViewCell cell, McAccount.NotificationConfigurationEnum choice)
            {
                if (owner.IsChoiceSet (choice)) {
                    using (var image = UIImage.FromBundle ("gen-checkbox-checked")) {
                        cell.ImageView.Image = image;
                    }
                } else {
                    using (var image = UIImage.FromBundle ("gen-checkbox")) {
                        cell.ImageView.Image = image;
                    }
                }
            }

            protected void ConfigureCells (UITableView tableView)
            {
                foreach (var cell in tableView.VisibleCells) {
                    var indexPath = tableView.IndexPathForCell (cell);
                    var choice = choices [indexPath.Row];
                    ConfigureCell (cell, choice);
                }
            }

            public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
            {
                var choice = choices [indexPath.Row];
                owner.SetChoice (choice);
                ConfigureCells (tableView);
            }
        }
    }
}
