//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public class DraftsTableViewSource : UITableViewSource
    {
        DraftsViewController owner;

        protected List<McAbstrItem> drafts;
        protected DraftsHelper.DraftType draftType;
        protected const string DraftCell = "draftCell";
        protected const string EmptyDraftsCell = "emptyDraftsCell";

        protected const float NORMAL_ROW_HEIGHT = 126.0f;

        protected const int SUBJECT_LABEL_TAG = 100;
        protected const int RECIPIENTS_LABEL_TAG = 101;
        protected const int DATE_LABEL_TAG = 102;
        protected const int BODY_LABEL_TAG = 103;

        public DraftsTableViewSource ()
        {
        }

        public void SetDraftsList (List<McAbstrItem> drafts)
        {
            this.drafts = drafts;
        }

        public void SetDraftType (DraftsHelper.DraftType draftType)
        {
            this.draftType = draftType;
        }

        public void SetOwner (DraftsViewController owner)
        {
            this.owner = owner;
        }

        public float GetTableHeight ()
        {
            return GetHeightForRow (null, null) * RowsInSection (null, 0);
        }

        public override int RowsInSection (UITableView tableview, int section)
        {
            return drafts.Count > 0 ? drafts.Count : 1;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            string cellIdentifier = (drafts.Count > 0 ? DraftCell : EmptyDraftsCell);
            var cell = tableView.DequeueReusableCell (cellIdentifier);

            if (null == cell) {
                cell = CreateCell (tableView, cellIdentifier);
            }

            cell.Layer.CornerRadius = 15;
            cell.Layer.MasksToBounds = true;
            cell.SelectionStyle = UITableViewCellSelectionStyle.None;

            ConfigureCell (cell, indexPath);
            return cell;
        }

        protected UITableViewCell CreateCell (UITableView tableView, string identifier)
        {
            if (identifier.Equals (EmptyDraftsCell)) {
                var emptyCell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
                emptyCell.TextLabel.TextAlignment = UITextAlignment.Center;
                emptyCell.TextLabel.TextColor = UIColor.FromRGB (0x0f, 0x42, 0x4c);
                emptyCell.TextLabel.Font = A.Font_AvenirNextDemiBold17;
                emptyCell.SelectionStyle = UITableViewCellSelectionStyle.None;
                emptyCell.ContentView.BackgroundColor = UIColor.White;
                return emptyCell;
            }

            UITableViewCell cell = new UITableViewCell (UITableViewCellStyle.Default, DraftCell);

            float width = tableView.Frame.Width;
            float yOffset = 10;

            UILabel subjectLabel = new UILabel (new RectangleF (10, yOffset, width - 50, 18));
            subjectLabel.TextColor = A.Color_0F424C;
            subjectLabel.Font = A.Font_AvenirNextDemiBold17;
            subjectLabel.TextAlignment = UITextAlignment.Left;
            subjectLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabel.Lines = 1;
            subjectLabel.Tag = SUBJECT_LABEL_TAG;
            cell.ContentView.AddSubview (subjectLabel);

            yOffset = subjectLabel.Frame.Bottom + 5;

            UILabel recipientsLabel = new UILabel (new RectangleF (10, yOffset, width - 50, 18));
            recipientsLabel.TextColor = A.Color_0F424C;
            recipientsLabel.Font = A.Font_AvenirNextRegular17;
            recipientsLabel.TextAlignment = UITextAlignment.Left;
            recipientsLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            recipientsLabel.Lines = 1;
            recipientsLabel.Tag = RECIPIENTS_LABEL_TAG;
            cell.ContentView.AddSubview (recipientsLabel);

            yOffset = recipientsLabel.Frame.Bottom + 5;

            UILabel dateLabel = new UILabel (new RectangleF (10, yOffset, width - 50, 16));
            dateLabel.TextColor = A.Color_9B9B9B;
            dateLabel.Font = A.Font_AvenirNextRegular14;
            dateLabel.TextAlignment = UITextAlignment.Left;
            dateLabel.Lines = 1;
            dateLabel.Tag = DATE_LABEL_TAG;
            cell.ContentView.AddSubview (dateLabel);

            yOffset = dateLabel.Frame.Bottom + 10;

            UILabel bodyLabel = new UILabel (new RectangleF (10, yOffset, width - 50, 40));
            bodyLabel.Font = A.Font_AvenirNextRegular14;
            bodyLabel.TextColor = A.Color_NachoDarkText;
            bodyLabel.Lines = 2;
            bodyLabel.Tag = BODY_LABEL_TAG;
            cell.ContentView.AddSubview (bodyLabel);

            return cell;
        }

        protected void ConfigureCell (UITableViewCell cell, NSIndexPath indexPath)
        {
            if (cell.ReuseIdentifier.Equals (EmptyDraftsCell)) {
                cell.TextLabel.Text = "No Drafts";
                return;
            }

            int row = indexPath.Row;
            DraftsHelper.DraftInfo draftInfo = DraftsHelper.DraftToDraftInfo (drafts [row], draftType);

            UILabel subjectLabel = (UILabel)cell.ContentView.ViewWithTag (SUBJECT_LABEL_TAG);
            UILabel recipientsLabel = (UILabel)cell.ContentView.ViewWithTag (RECIPIENTS_LABEL_TAG);
            UILabel dateLabel = (UILabel)cell.ContentView.ViewWithTag (DATE_LABEL_TAG);
            UILabel bodyLabel = (UILabel)cell.ContentView.ViewWithTag (BODY_LABEL_TAG);

            subjectLabel.Text = draftInfo.subject;
            bodyLabel.Text = draftInfo.body;

            switch (draftType) {
            case DraftsHelper.DraftType.Calendar:
                recipientsLabel.Text = draftInfo.date;
                dateLabel.Text = draftInfo.recipients;
                break;
            case DraftsHelper.DraftType.Email:
                recipientsLabel.Text = draftInfo.recipients;
                dateLabel.Text = draftInfo.date;
                break;
            }
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return NORMAL_ROW_HEIGHT;
        }

        public override void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            if (1 > drafts.Count) {
                return;
            } else {
                int row = indexPath.Row;
                owner.DraftItemSelected (draftType, drafts [row]);
            }
        }
    }
}



