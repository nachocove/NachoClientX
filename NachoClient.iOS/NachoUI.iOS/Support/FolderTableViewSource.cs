//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore.Model;
using NachoCore.Utils;
using CoreAnimation;
using CoreGraphics;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public class FolderTableViewSource : UITableViewSource
    {
        bool hideFakeFolders;
        FolderLists folderLists;

        public event EventHandler<int> OnToggleClick;
        public event EventHandler<McFolder> OnFolderSelected;
        public event EventHandler<McAccount> OnAccountSelected;

        const string FOLDER_ROW_TYPE = "FolderReuseIdentifer";
        const string HEADER_ROW_TYPE = "HeaderReuseIdentifier";
        const string ACCOUNT_ROW_TYPE = "AccountReuseIdentifier";

        List<int> sectionIndex;

        public FolderTableViewSource (int accountId, bool hideFakeFolders)
        {
            this.hideFakeFolders = hideFakeFolders;
            folderLists = new FolderLists (accountId, hideFakeFolders);
            CreateIndex ();
        }

        public override nint RowsInSection (UITableView tableview, nint section)
        {
            if (null == folderLists) {
                return 0;
            } else {
                int s = (int)section;
                return sectionIndex [s + 1] - sectionIndex [s];
            }
        }

        public override nint NumberOfSections (UITableView tableView)
        {
            return sectionIndex.Count - 1;
        }

        int GetDisplayElementIndex(Foundation.NSIndexPath indexPath)
        {
            return sectionIndex [indexPath.Section] + indexPath.Row;
        }

        FolderLists.DisplayElement GetDisplayElement (Foundation.NSIndexPath indexPath)
        {
            return folderLists.displayList [GetDisplayElementIndex(indexPath)];
        }

        public override UITableViewCell GetCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var d = GetDisplayElement (indexPath);

            if (null == d.node) {
                return GetHeaderCell (tableView, indexPath);
            }
            if (null == d.node.account) {
                return GetFolderCell (tableView, indexPath);
            } else {
                return GetAccountCell (tableView, indexPath);
            }
        }

        public UITableViewCell GetHeaderCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (HEADER_ROW_TYPE);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, HEADER_ROW_TYPE);
            }

            var d = GetDisplayElement (indexPath);

            switch (d.header) {
            case FolderLists.Header.None:
                cell.TextLabel.Text = "";
                break;
            case FolderLists.Header.Accounts:
                cell.TextLabel.Text = "Account";
                break;
            case FolderLists.Header.Default:
                cell.TextLabel.Text = "Default Folders";
                break;
            case FolderLists.Header.Folders:
                cell.TextLabel.Text = "Your Folders";
                break;
            case FolderLists.Header.Recents:
                cell.TextLabel.Text = "Recent Folders";
                break;
            }
            return cell;
        }

        public UITableViewCell GetFolderCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (FOLDER_ROW_TYPE);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, FOLDER_ROW_TYPE);
                using (var image = UIImage.FromBundle ("nav-folder")) {
                    cell.ImageView.Image = image;
                }
            }

            var d = GetDisplayElement (indexPath);
            var node = d.node;

            cell.TextLabel.Text = node.folder.DisplayName;
            cell.IndentationLevel = d.level;

            if (0 == node.children.Count) {
                cell.AccessoryView = null;
            } else {
                var button = new UIButton (UIButtonType.Custom);
                button.Frame = new CGRect (0, 0, 24, 24);
                if (folderLists.IsOpen(node)) {
                    using (var image = UIImage.FromBundle ("gen-readmore-active")) {
                        button.SetImage (image, UIControlState.Normal);
                    }
                } else {
                    using (var image = UIImage.FromBundle ("gen-readmore")) {
                        button.SetImage (image, UIControlState.Normal);
                    }
                }
                button.Tag = d.node.UniqueId;
                button.AccessibilityLabel = "FolderToggle";
                cell.AccessoryView = button;
                button.TouchUpInside += ToggleButton_TouchUpInside;
            }

            return cell;
        }

        public UITableViewCell GetAccountCell (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var cell = tableView.DequeueReusableCell (ACCOUNT_ROW_TYPE);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, ACCOUNT_ROW_TYPE);
            }

            var d = GetDisplayElement (indexPath);
            var node = d.node;

            cell.TextLabel.Text = node.account.EmailAddr;

            if (node.opened) {
                using (var image = UIImage.FromBundle ("gen-checkbox-checked")) {
                    cell.ImageView.Image = image;
                }
            } else {
                using (var image = UIImage.FromBundle ("gen-checkbox")) {
                    cell.ImageView.Image = image;
                }
            }

            return cell;
        }

        public override void RowSelected (UITableView tableView, Foundation.NSIndexPath indexPath)
        {
            var d = GetDisplayElement (indexPath);
            var node = d.node;

            if (null != node) {
                if (null != node.account) {
                    OnAccountSelected (this, node.account);
                } else if (null != OnFolderSelected) {
                    OnFolderSelected (this, node.folder);
                }
            }
        }

        void ToggleButton_TouchUpInside (object sender, EventArgs e)
        {
            int uniqueId = (int) ((UIView)sender).Tag;
            OnToggleClick (sender, uniqueId);
        }

        public void Refresh (int accountId)
        {
            folderLists.Create (accountId, hideFakeFolders);
            CreateIndex ();
        }

        public void Toggle (int uniqueId)
        {
            folderLists.ToggleById (uniqueId);
            CreateIndex ();
        }

        void CreateIndex ()
        {
            int i = 0;
            sectionIndex = new List<int> ();
            foreach (var d in folderLists.displayList) {
                if (FolderLists.Header.None != d.header) {
                    sectionIndex.Add (i);
                }
                i += 1;
            }
            sectionIndex [0] = 1; // Skip first header
            sectionIndex.Add (i);
        }

        public override void WillDisplay (UITableView tableView, UITableViewCell cell, Foundation.NSIndexPath indexPath)
        {
            float cornerRadius = 5;
            cell.BackgroundColor = UIColor.Clear;
            var layer = new CAShapeLayer ();
            var bounds = cell.Bounds.Inset (10, 0);

            var d = GetDisplayElement (indexPath);

            using (var pathRef = new CGPath ()) {
                if ((FolderLists.Header.None != d.header) || (0 == indexPath.Row && 0 == indexPath.Section)) {
                    pathRef.MoveToPoint (bounds.GetMinX (), bounds.GetMaxY ());
                    pathRef.AddArcToPoint (bounds.GetMinX (), bounds.GetMinY (), bounds.GetMidX (), bounds.GetMinY (), cornerRadius);
                    pathRef.AddArcToPoint (bounds.GetMaxX (), bounds.GetMinY (), bounds.GetMaxX (), bounds.GetMidY (), cornerRadius);
                    pathRef.AddLineToPoint (bounds.GetMaxX (), bounds.GetMaxY ());
                } else if (d.lastInSection) {
                    pathRef.MoveToPoint (bounds.GetMinX (), bounds.GetMinY ());
                    pathRef.AddArcToPoint (bounds.GetMinX (), bounds.GetMaxY (), bounds.GetMidX (), bounds.GetMaxY (), cornerRadius);
                    pathRef.AddArcToPoint (bounds.GetMaxX (), bounds.GetMaxY (), bounds.GetMaxX (), bounds.GetMidY (), cornerRadius);
                    pathRef.AddLineToPoint (bounds.GetMaxX (), bounds.GetMinY ());
                } else {
                    pathRef.AddRect (bounds);
                }

                layer.Path = pathRef;
            }

            ViewFramer.Create (cell.ImageView).X (10 * d.level);

            layer.FillColor = UIColor.FromWhiteAlpha (1, 0.8f).CGColor;
            var background = new UIView (bounds);
            background.Layer.InsertSublayer (layer, 0);
            background.BackgroundColor = UIColor.Clear;
            cell.BackgroundView = background;
        }
            
    }
}

