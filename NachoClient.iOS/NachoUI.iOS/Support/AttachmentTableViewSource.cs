//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using MonoTouch.CoreGraphics;

namespace NachoClient.iOS
{
    public class AttachmentTableViewSource : UITableViewSource
    {
        List<McAttachment> AttachmentsList = new List<McAttachment> ();
        protected McAccount Account;
        protected bool editing = false;
        public IAttachmentTableViewSourceDelegate owner;

        protected const string UICellReuseIdentifier = "UICell";
        protected const string AttachmentCellReuseIdentifier = "AttachmentCell";

        public AttachmentTableViewSource ()
        {
            owner = null;
        }

        public void SetOwner (IAttachmentTableViewSourceDelegate owner)
        {
            this.owner = owner;
        }

        public void SetAttachmentList (List<McAttachment> attachments)
        {
            this.AttachmentsList = new List<McAttachment> ();
            foreach (var attachment in attachments) {
                this.AttachmentsList.Add (attachment);
            }
        }

        public void SetAccount (McAccount account)
        {
            this.Account = account;
        }

        public void SetEditing (bool editing)
        {
            this.editing = editing;
        }

        public List<McAttachment> GetAttachmentList ()
        {
            return this.AttachmentsList;
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

        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
        }

        UIView ViewWithLabel (string text)
        {
            var label = new UILabel ();
            label.Text = text;
            label.Font = A.Font_AvenirNextDemiBold14;
            label.TextColor = UIColor.White;
            label.SizeToFit ();
            var labelView = new UIView ();
            labelView.Frame = new RectangleF (30, 0, label.Frame.Width + 50, label.Frame.Height);
            labelView.Add (label);
            return labelView;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 44;
        }

        protected const int ATTACHMENT_DISPLAY_NAME_TAG = 333;
        protected const int ATTACHMENT_IMAGE_TAG = 334;

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            McAttachment attachment;
            attachment = AttachmentsList [indexPath.Row];

            var cell = CreateCell ();
            ConfigureCell (cell, attachment);

            return cell;
        }

        void ConfigureSwipes (MCSwipeTableViewCell cell, McAttachment attachment)
        {
            cell.FirstTrigger = 0.20f;
            cell.SecondTrigger = 0.50f;

            UIColor redColor = null;
            UIView deleteView = null;

            try { 
                deleteView = ViewWithLabel ("remove");
                redColor = new UIColor (A.Color_NachoRed.CGColor);

                cell.SetSwipeGestureWithView (deleteView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State3, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    RemoveAttachment (attachment);
                });
                cell.SetSwipeGestureWithView (deleteView, redColor, MCSwipeTableViewCellMode.Switch, MCSwipeTableViewCellState.State4, delegate(MCSwipeTableViewCell c, MCSwipeTableViewCellState state, MCSwipeTableViewCellMode mode) {
                    RemoveAttachment (attachment);
                });
            } finally {
                if (null != redColor) {
                    redColor.Dispose ();
                }
                if (null != deleteView) {
                    deleteView.Dispose ();
                }
            }
        }

        public MCSwipeTableViewCell CreateCell ()
        {
            var cell = new MCSwipeTableViewCell (UITableViewCellStyle.Subtitle, AttachmentCellReuseIdentifier);
            NcAssert.True (null != cell);

            if (null == cell.ViewWithTag (ATTACHMENT_DISPLAY_NAME_TAG)) {
                var userNameView = new UILabel (new RectangleF (40, 0, 320 - 40 - 15, 44));
                userNameView.LineBreakMode = UILineBreakMode.TailTruncation;
                userNameView.Tag = ATTACHMENT_DISPLAY_NAME_TAG;
                cell.ContentView.AddSubview (userNameView);

                UIImageView responseImageView = new UIImageView (new RectangleF (15, 22 - 7.5f, 15, 15));
                responseImageView.Image = UIImage.FromBundle ("icn-attach-files");
                responseImageView.Tag = ATTACHMENT_IMAGE_TAG;

                cell.ContentView.AddSubview (responseImageView);
            }
            return cell;
        }

        public void ConfigureCell (MCSwipeTableViewCell cell, McAttachment attachment)
        {
            var displayName = attachment.DisplayName;
            cell.TextLabel.Text = null;
            cell.DetailTextLabel.Text = null;

            ConfigureSwipes (cell, attachment);

            var TextLabel = cell.ViewWithTag (ATTACHMENT_DISPLAY_NAME_TAG) as UILabel;
            TextLabel.Text = displayName;
            TextLabel.TextColor = A.Color_0B3239;
            TextLabel.Font = A.Font_AvenirNextDemiBold17;

            if (!editing) {
                cell.UserInteractionEnabled = false;
            }
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            owner.RemoveAttachment (attachment);
        }

        public override void DraggingStarted (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DraggingStarted");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStarted),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DecelerationEnded (UIScrollView scrollView)
        {
            Log.Info (Log.LOG_UI, "DecelerationEnded");
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                Account = ConstMcAccount.NotAccountSpecific,
            });
        }

        public override void DraggingEnded (UIScrollView scrollView, bool willDecelerate)
        {
            if (!willDecelerate) {
                Log.Info (Log.LOG_UI, "DraggingEnded");
                NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                    Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_BackgroundAbateStopped),
                    Account = ConstMcAccount.NotAccountSpecific,
                });
            }
        }
        
    }
}