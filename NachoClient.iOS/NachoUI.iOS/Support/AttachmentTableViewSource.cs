//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    public class AttachmentTableViewSource : UITableViewSource
    {
        List<McAttachment> AttachmentsList = new List<McAttachment> ();
        public UIViewController vc;

        protected const string AttachmentCellReuseIdentifier = "AttachmentCell";

        protected UIColor CELL_COMPONENT_BG_COLOR = UIColor.White;
        protected const int VIEW_TAG = 99100;

        protected static int ICON_TAG = 150;
        protected static int TEXT_LABEL_TAG = 300;
        protected static int DETAIL_TEXT_LABEL_TAG = 400;
        protected static int SEPARATOR_LINE_TAG = 500;

        public AttachmentTableViewSource ()
        {
        }

        public void SetVC (UIViewController vc)
        {
            this.vc = vc;
        }

        public void SetAttachmentList (List<McAttachment> attachments)
        {
            this.AttachmentsList = new List<McAttachment> ();
            foreach (var attachment in attachments) {
                this.AttachmentsList.Add (attachment);
            }
        }

        /// <summary>
        /// Tableview delegate
        /// </summary>
        public override int NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            return AttachmentsList.Count;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 60;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (AttachmentCellReuseIdentifier);
            if (cell == null) {
                cell = CreateCell (tableView, AttachmentCellReuseIdentifier);
            }
            NcAssert.True (null != cell);

            McAttachment attachment = AttachmentsList [indexPath.Row];
            ConfigureCell (tableView, cell, indexPath, attachment);

            return cell;
        }

        protected UITableViewCell CreateCell (UITableView tableView, string identifier)
        {
            var cell = tableView.DequeueReusableCell (identifier);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
            }
            if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            cell.ContentView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            var cellWidth = tableView.Frame.Width;

            var frame = new RectangleF (0, 0, tableView.Frame.Width, 60);
            var view = new UIView (frame);

            cell.AddSubview (view);
            view.Tag = VIEW_TAG;
            view.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            //Cell icon
            var cellIconImageView = new UIImageView (); 
            cellIconImageView.Tag = ICON_TAG;
            cellIconImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            cellIconImageView.Frame = new RectangleF (18, 28, 24, 24);
            view.AddSubview (cellIconImageView);

            //Text label
            var textLabel = new UILabel (); 
            textLabel.Tag = TEXT_LABEL_TAG;
            textLabel.Font = A.Font_AvenirNextDemiBold14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            textLabel.Frame = new RectangleF (60, 11, cell.Frame.Width - 60 - 52, 19.5f);
            view.AddSubview (textLabel);

            //Detail text label
            var detailTextlabel = new UILabel (); 
            detailTextlabel.Tag = DETAIL_TEXT_LABEL_TAG;
            detailTextlabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            detailTextlabel.Font = A.Font_AvenirNextRegular14;
            detailTextlabel.TextColor = A.Color_NachoTextGray;
            detailTextlabel.Frame = new RectangleF (60, 11 + 19.5f, cell.Frame.Width - 60 - 52, 19.5f);
            view.AddSubview (detailTextlabel);

            //Separator line
            var separatorLine = Util.AddHorizontalLineView (60, 60, cell.Frame.Width - 60, A.Color_NachoBorderGray);
            separatorLine.Tag = SEPARATOR_LINE_TAG;
            view.AddSubview (separatorLine);

            cell.AddSubview (view);
            return cell;
        }

        public void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath, McAttachment attachment)
        {
            float yOffset = 10.5f;
            float xOffset = 0f;

            var view = cell.ViewWithTag (VIEW_TAG);

            //Cell icon
            var cellIconImageView = view.ViewWithTag (ICON_TAG) as UIImageView;
            cellIconImageView.Frame = new RectangleF (xOffset + 18, 18, 24, 24);

            //Text label
            var textLabel = view.ViewWithTag (TEXT_LABEL_TAG) as UILabel; 
            textLabel.Frame = new RectangleF (xOffset + 60, yOffset, cell.Frame.Width - 112 - xOffset, 19.5f);
            yOffset += textLabel.Frame.Height;

            //Detail text label
            var detailTextlabel = view.ViewWithTag (DETAIL_TEXT_LABEL_TAG) as UILabel;  
            detailTextlabel.Frame = new RectangleF (xOffset + 60, yOffset, cell.Frame.Width - 112 - xOffset, 19.5f);
            yOffset += detailTextlabel.Frame.Height;

            //Separator line
            var separatorLine = view.ViewWithTag (SEPARATOR_LINE_TAG);
            var totalRow = tableView.NumberOfRowsInSection (indexPath.Section);
            if (totalRow - 1 == indexPath.Row) {
                separatorLine.Frame = new RectangleF (0, 59.5f, cell.Frame.Width, .5f);
            } else {
                separatorLine.Frame = new RectangleF (60 + xOffset, 59.5f, cell.Frame.Width - 60 - xOffset, .5f);
            }

            ConfigureAttachmentView (cell, attachment, cellIconImageView, textLabel, detailTextlabel);
        }

        public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            PlatformHelpers.DisplayAttachment (this.vc, AttachmentsList [indexPath.Row]);
            tableView.DeselectRow (indexPath, true);
        }

        protected void ConfigureAttachmentView (UITableViewCell cell, McAttachment attachment, UIImageView iconView, UILabel textLabel, UILabel detailTextLabel)
        {
            if (null != attachment) {

                textLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);

                var detailText = "";
                if (attachment.IsInline) {
                    detailText += "Inline ";
                }
                string extension = Path.GetExtension (attachment.DisplayName).ToUpper ();
                detailText += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
                detailText += "file";
                if (0 != attachment.FileSize) {
                    detailText += " - " + Pretty.PrettyFileSize (attachment.FileSize);
                } 
                detailTextLabel.Text = detailText;

                if (Pretty.TreatLikeAPhoto (attachment.DisplayName)) {
                    iconView.Image = UIImage.FromBundle ("email-att-photos");
                } else {
                    iconView.Image = UIImage.FromBundle ("email-att-files");
                }
            } else {
                textLabel.Text = "File no longer exists"; 
            }
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.HighPriority ("AttachmentTableViewSource DraggingStarted");
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            NachoCore.Utils.NcAbate.RegularPriority ("AttachmentTableViewSource DecelerationEnded");
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                NachoCore.Utils.NcAbate.RegularPriority ("AttachmentTableViewSource DraggingEnded");
            }
        }
        
    }
}