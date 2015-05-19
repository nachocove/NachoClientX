//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using UIKit;
using Foundation;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public class EmailServiceTableViewSource : UITableViewSource
    {


        public delegate void ServiceSelected (McAccount.AccountServiceEnum service, bool willExpand);

        public ServiceSelected OnSelected;

        protected class ServiceInfo
        {
            public bool Selected;
            public String ServiceIconName;
            public McAccount.AccountServiceEnum EmailService;

            public ServiceInfo (bool selected, string iconName, McAccount.AccountServiceEnum service)
            {
                Selected = selected;
                ServiceIconName = iconName;
                EmailService = service;
            }
        }

        protected List<ServiceInfo> providers = new List<ServiceInfo> {
            new ServiceInfo (true, "", McAccount.AccountServiceEnum.None),
            new ServiceInfo (false, "login-mex@2x", McAccount.AccountServiceEnum.Exchange),
            new ServiceInfo (false, "login-gmail@2x", McAccount.AccountServiceEnum.GoogleDefault),
            new ServiceInfo (false, "login-hotmail@2x", McAccount.AccountServiceEnum.HotmailExchange),
            new ServiceInfo (false, "login-google@2x", McAccount.AccountServiceEnum.GoogleExchange),
            new ServiceInfo (false, "login-outlook@2x", McAccount.AccountServiceEnum.OutlookExchange),
            new ServiceInfo (false, "login-imap@2x", McAccount.AccountServiceEnum.IMAP_SMTP),
        };

        protected bool expanded;

        public EmailServiceTableViewSource ()
        {
        }

        public bool IsHotmailServiceSelected ()
        {
            var p = GetSelectedItem ();

            if (null == p) {
                return false;
            }
            if (McAccount.AccountServiceEnum.HotmailExchange == p.EmailService) {
                return true;
            }
            if (McAccount.AccountServiceEnum.OutlookExchange == p.EmailService) {
                return true;
            }
            return false;
        }

        public void SetSelectedItem (McAccount.AccountServiceEnum provider)
        {
            foreach (var p in providers) {
                p.Selected = false;
                if (p.EmailService == provider) {
                    p.Selected = true;
                }
            }
        }

        public nfloat GetTableHeight ()
        {
            return GetHeightForRow (null, null) * RowsInSection (null, 0);
        }

        protected ServiceInfo GetSelectedItem ()
        {
            foreach (var p in providers) {
                if (p.Selected) {
                    return p;
                }
            }
            NcAssert.CaseError ();
            return null;
        }

        /// <summary>
        /// If the index is zero, return the selected item.
        /// If the index is non-zero, return the Nth not hidden not select item.
        /// </summary>
        protected ServiceInfo GetProvider (int i)
        {
            if (expanded) {
                return providers [i];
            }
            foreach (var p in providers) {
                if (p.Selected) {
                    return p;
                }
            }
            NcAssert.CaseError ();
            return null;
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            return (expanded ? providers.Count : 1); 
        }

        const int DROPDOWN_IMAGEVIEW_TAG = 87;
        const int PROVIDER_IMAGEVIEW_TAG = 88;
        const string CellId = "cell";

        // If the row is zero and we are not expanded,
        // show either the prompt or the currently selected provider.
        // If we are expanded, show prompt plus the list.
        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (CellId);
            if (null == cell) {
                cell = CreateCell (tableView);
            }
            ConfigureCell (cell, indexPath);
            return cell;
        }

        protected UITableViewCell CreateCell (UITableView tableView)
        {
            var cell = new UITableViewCell (UITableViewCellStyle.Default, CellId);

            var width = tableView.Frame.Width - 24 - 15;

            var providerImageView = new UIImageView (new CGRect (0, 1, 284, 46));
            providerImageView.Tag = PROVIDER_IMAGEVIEW_TAG;
            cell.ContentView.AddSubview (providerImageView);

            var dropdownImageView = new UIImageView (new CGRect (width, 11, 24, 24));
            dropdownImageView.Tag = DROPDOWN_IMAGEVIEW_TAG;
            cell.ContentView.AddSubview (dropdownImageView);

            return cell;
        }

        protected void ConfigureCell (UITableViewCell cell, NSIndexPath indexPath)
        {
            var row = indexPath.Row;
            var provider = GetProvider (row);

            var providerImageView = (UIImageView)cell.ContentView.ViewWithTag (PROVIDER_IMAGEVIEW_TAG);
            var dropdownImageView = (UIImageView)cell.ContentView.ViewWithTag (DROPDOWN_IMAGEVIEW_TAG);

            cell.TextLabel.Text = "";
            cell.TextLabel.Font = A.Font_AvenirNextRegular14;
            providerImageView.Hidden = (McAccount.AccountServiceEnum.None == provider.EmailService);

            if (McAccount.AccountServiceEnum.None == provider.EmailService) {
                cell.TextLabel.Text = "Choose your email service";
            } else {
                cell.ContentView.AccessibilityLabel = McAccount.AccountServiceName (provider.EmailService);
                cell.ContentView.AccessibilityIdentifier = McAccount.AccountServiceName (provider.EmailService);
            }

            if (!providerImageView.Hidden) {
                using (var image = UIImage.FromBundle (provider.ServiceIconName)) {
                    providerImageView.Image = image;
                }
            }

            dropdownImageView.Hidden = (0 != row);

            if (!dropdownImageView.Hidden) {
                using (var image = UIImage.FromBundle ("login-dropdown")) {
                    dropdownImageView.Image = image;
                }
            }
        }

        public override nfloat GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 46;
        }

        protected void MaybeOnSelected (McAccount.AccountServiceEnum provider, bool willExpand)
        {
            if (null != OnSelected) {
                OnSelected (provider, willExpand);
            }
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            expanded = !expanded;

            // Clear the selection color
            var cell = tableView.CellAt (indexPath);
            cell.SetSelected (false, false);

            var row = indexPath.Row;
            var old = GetSelectedItem ();

            // If we select row zero, we are just toggling
            if (0 == row) {
                MaybeOnSelected (old.EmailService, expanded);
                return;
            }

            // Otherwise, update the selection
            old.Selected = false;
            var selected = providers [indexPath.Row];
            selected.Selected = true;
            MaybeOnSelected (selected.EmailService, expanded);
        }

        public void Shrink (UITableView tableView)
        {
            var deletePaths = new List<NSIndexPath> ();
            for (int i = 0; i < providers.Count; i++) {
                if (!providers [i].Selected) {
                    var path = NSIndexPath.FromItemSection (i, 0);
                    deletePaths.Add (path);
                }
            }
            tableView.BeginUpdates ();
            tableView.DeleteRows (deletePaths.ToArray (), UITableViewRowAnimation.Top);
            tableView.EndUpdates ();

            var ip = NSIndexPath.FromRowSection (0, 0);
            var cell = tableView.CellAt (ip);
            ConfigureCell (cell, ip);
        }

        public void Grow (UITableView tableView)
        {
            var reloadPaths = new List<NSIndexPath> ();
            reloadPaths.Add (NSIndexPath.FromItemSection (0, 0));

            var insertPaths = new List<NSIndexPath> ();
            for (int i = 1; i < providers.Count; i++) {
                var path = NSIndexPath.FromItemSection (i, 0);
                insertPaths.Add (path);
            }
            tableView.BeginUpdates ();
            tableView.ReloadRows (reloadPaths.ToArray (), UITableViewRowAnimation.None);
            tableView.InsertRows (insertPaths.ToArray (), UITableViewRowAnimation.Top);
            tableView.EndUpdates ();
        }
    }
}

