//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using CoreGraphics;
using System.Collections.Generic;

using UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public class UcAttachmentCell : UIView
    {
        protected const float LINE_HEIGHT = 60;
        protected const float LEFT_INDENT = 15;
        protected const float LEFT_ADDRESS_INDENT = 57;
        protected UIColor CELL_COMPONENT_BG_COLOR = UIColor.White;
        protected const int REMOVE_BUTTON_TAG = 1100;

        public delegate void UcAttachmentCellAction (UcAttachmentCell cell);

        protected UcAttachmentCellAction tapAction;
        protected UcAttachmentCellAction removeAction;

        protected UITapGestureRecognizer viewTapped;
        protected UITapGestureRecognizer.Token viewTappedToken;

        UIButton removeButton;
        UIImageView cellIconImageView;
        UILabel textLabel;
        UILabel detailTextlabel;
        UIView separatorLine;

        public McAttachment attachment;

        public UcAttachmentCell (McAttachment attachment, nfloat parentWidth, bool editable, UcAttachmentCellAction tapAction, UcAttachmentCellAction removeAction)
            : base (new CGRect (0, 0, parentWidth, LINE_HEIGHT))
        {
            this.attachment = attachment;
            this.tapAction = tapAction;
            this.removeAction = editable ? removeAction : null;
            this.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            CreateView (editable);
        }

        public void CreateView (bool editable)
        {
            nfloat xOffset = 0;
            if (editable) {
                //Remove icon
                removeButton = new UIButton ();
                removeButton.AccessibilityLabel = "Remove";
                removeButton.Tag = REMOVE_BUTTON_TAG;
                removeButton.SetImage (UIImage.FromBundle ("gen-delete-small"), UIControlState.Normal);
                removeButton.Frame = new CGRect (9, (LINE_HEIGHT / 2) - 17, 34, 34);
                if (null != removeAction) {
                    removeButton.TouchUpInside += RemoveClicked;
                }
                this.AddSubview (removeButton);
                xOffset += 34;
            }

            //Cell icon
            cellIconImageView = new UIImageView (); 
            cellIconImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            cellIconImageView.Frame = new CGRect (xOffset + 18, 18, 24, 24);
            this.AddSubview (cellIconImageView);

            //Text label
            textLabel = new UILabel (); 
            textLabel.Font = A.Font_AvenirNextDemiBold14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            textLabel.Frame = new CGRect (xOffset + 60, 11, Bounds.Width - 60 - 52, 19.5f);
            textLabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            this.AddSubview (textLabel);

            //Detail text label
            detailTextlabel = new UILabel (); 
            detailTextlabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            detailTextlabel.Font = A.Font_AvenirNextRegular14;
            detailTextlabel.TextColor = A.Color_NachoTextGray;
            detailTextlabel.Frame = new CGRect (xOffset + 60, 11 + 19.5f, Bounds.Width - 60 - 52, 19.5f);
            detailTextlabel.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            this.AddSubview (detailTextlabel);

            //Separator line
            separatorLine = Util.AddHorizontalLine (xOffset + 60, 60, Bounds.Width - 60, A.Color_NachoBorderGray);
            separatorLine.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            this.AddSubview (separatorLine);

            viewTapped = new UITapGestureRecognizer ();
            viewTappedToken = viewTapped.AddTarget (ViewTapped);
            this.AddGestureRecognizer (viewTapped);

            ConfigureView ();
        }

        public void ConfigureView ()
        {
            if (null != attachment) {

                textLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);

                var detailText = "";
                if (attachment.IsInline) {
                    detailText += "Inline ";
                }
                string extension = Pretty.GetExtension (attachment.DisplayName);
                detailText += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
                detailText += "file";
                if (0 != attachment.FileSize) {
                    detailText += " - " + Pretty.PrettyFileSize (attachment.FileSize);
                } 
                detailTextlabel.Text = detailText;

                if (Pretty.TreatLikeAPhoto(attachment.DisplayName)) {
                    cellIconImageView.Image = UIImage.FromBundle ("email-att-photos");
                } else {
                    cellIconImageView.Image = UIImage.FromBundle ("email-att-files");
                }
            } else {
                textLabel.Text = "File no longer exists";
            }
        }

        private void RemoveClicked (object sender, EventArgs e)
        {
            removeAction (this);
        }

        private void ViewTapped ()
        {
            if (null != tapAction) {
                tapAction (this);
            }
        }

        protected override void Dispose (bool disposing)
        {
            if (null != removeAction) {
                ((UIButton)this.ViewWithTag (REMOVE_BUTTON_TAG)).TouchUpInside -= RemoveClicked;
            }
            viewTapped.RemoveTarget (viewTappedToken);
            this.RemoveGestureRecognizer (viewTapped);
            base.Dispose (disposing);
        }
    }

    public class UcAttachmentBlock : UIView
    {
        protected IUcAttachmentBlockDelegate owner;
        protected List<UcAttachmentCell> list = new List<UcAttachmentCell> ();

        protected int CELL_HEIGHT;
        protected const int LINE_HEIGHT = 60;
        protected const int LEFT_INDENT = 15;
        protected const int RIGHT_INDENT = 15;

        protected const int REMOVE_BUTTON_TAG = 1100;

        bool isCompact;
        bool editable;
        UIView contentView;
        UILabel mainLabel;
        UIButton chooserButton;

        protected UITapGestureRecognizer toggleCompactTapped;
        protected UITapGestureRecognizer.Token toggleCompactTappedToken;

        public UcAttachmentBlock (IUcAttachmentBlockDelegate owner, int cellHeight, bool editable)
        {
            this.owner = owner;
            this.BackgroundColor = UIColor.White;
            this.CELL_HEIGHT = cellHeight;
            this.editable = editable;

            this.AutosizesSubviews = false;

            CreateView ();
        }

        public void SetCompact (bool isCompact)
        {
            this.isCompact = isCompact;
        }

        public void ToggleCompact ()
        {
            isCompact = !isCompact;
            ConfigureView ();
            SetNeedsLayout ();
        }

        public List<McAttachment> AttachmentList {
            get {
                var l = new List<McAttachment> ();
                foreach (var a in list) {
                    l.Add (a.attachment);
                }
                return l;
            }
        }

        public void ReplaceAttachment(McAttachment original, McAttachment replacement)
        {
            foreach(var a in list) {
                if (original == a.attachment) {
                    a.attachment = replacement;
                    original.Delete ();
                }
            }
        }

        public void UpdateAttachment (McAttachment attachment)
        {
            foreach (var a in list) {
                if (a.attachment.Id == attachment.Id) {
                    a.attachment = attachment;
                    a.ConfigureView ();
                }
            }
        }

        public void CreateView ()
        {
            contentView = new UIView ();
            this.AddSubview (contentView);

            mainLabel = new UILabel ();
            AdjustXY (mainLabel, LEFT_INDENT, 0);
            mainLabel.Text = "Attachments";
            mainLabel.Font = A.Font_AvenirNextMedium14;
            mainLabel.TextColor = A.Color_NachoDarkText;
            contentView.AddSubview (mainLabel);

            if (editable) {
                chooserButton = UIButton.FromType (UIButtonType.System);
                chooserButton.AccessibilityLabel = "Add attachment";
                Util.SetOriginalImagesForButton (chooserButton, "email-add", "email-add-active");
                chooserButton.SizeToFit ();
                chooserButton.Frame = new CGRect (Bounds.Width - 43, 0, 40, CELL_HEIGHT);
                chooserButton.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
                chooserButton.TouchUpInside += ChooserButtonClicked;
                contentView.AddSubview (chooserButton);
            }

            // Tapping on the view toggles between compact and regular size
            toggleCompactTapped = new UITapGestureRecognizer ();
            toggleCompactTappedToken = toggleCompactTapped.AddTarget (ShowChooserOrToggleCompactness);
            contentView.AddGestureRecognizer (toggleCompactTapped);
        }

        public void Append (McAttachment attachment)
        {
            var c = new UcAttachmentCell (attachment, Bounds.Width, editable, (UcAttachmentCell cell) => {
                if (null != owner) {
                    owner.DisplayAttachmentForAttachmentBlock (cell.attachment);
                }
            }, (UcAttachmentCell cell) => {
                Remove (cell);
            });
            contentView.AddSubview (c);
            list.Add (c);

            Layout ();
            ConfigureView ();
        } 

        public void Remove (UcAttachmentCell c)
        {
            var attachment = c.attachment;
            list.Remove (c);
            c.RemoveFromSuperview ();
            c.Dispose ();

            SetNeedsLayout ();
            ConfigureView ();
            if (null != owner) {
                owner.RemoveAttachmentForAttachmentBlock (attachment);
            }
        }

        public void Clear ()
        {
            foreach (var cell in list) {
                cell.RemoveFromSuperview ();
                cell.Dispose ();
            }
            list.Clear ();
            SetNeedsLayout ();
            ConfigureView ();
        }

        /// Adjusts x & y on the top line of a view
        protected void AdjustXY (UIView view, nfloat X, nfloat Y)
        {
            view.Center = new CGPoint (X + (view.Frame.Width / 2), CELL_HEIGHT / 2);
        }

        public void ConfigureView ()
        {
            if (0 == list.Count) {
                mainLabel.Text = String.Format ("Attachments:", list.Count);
            } else {
                mainLabel.Text = String.Format ("Attachments ({0})", list.Count);
            }

            foreach (var c in list) {
                c.Hidden = isCompact;
            }

            if (null != owner) {
                owner.AttachmentBlockNeedsLayout (this);
            }
        }

        public override void LayoutSubviews ()
        {
            Layout ();
        }

        public void Layout ()
        {
            nfloat yOffset = 0;

            var mainLabelSize = mainLabel.Text.StringSize (mainLabel.Font);
            mainLabel.Frame = new CGRect (mainLabel.Frame.Location, mainLabelSize);
            AdjustXY (mainLabel, LEFT_INDENT, yOffset);

            yOffset += CELL_HEIGHT;

            foreach (var c in list) {
                if (!c.Hidden) {
                    c.Frame = new CGRect (0, yOffset, c.Frame.Width, c.Frame.Height);
                    yOffset += LINE_HEIGHT;
                }
            }

            contentView.Frame = new CGRect (0, 0, Bounds.Width, yOffset);
            this.Frame = new CGRect (this.Frame.Location, contentView.Frame.Size);
        }

        protected override void Dispose (bool disposing)
        {
            if (null != chooserButton) {
                chooserButton.TouchUpInside -= ChooserButtonClicked;
            }
            toggleCompactTapped.RemoveTarget (toggleCompactTappedToken);
            contentView.RemoveGestureRecognizer (toggleCompactTapped);
            owner = null;
            base.Dispose (disposing);
        }

        private void ChooserButtonClicked (object sender, EventArgs e)
        {
            ShowChooser ();
        }

        private void ShowChooser()
        {
            if (null != owner) {
                owner.ShowChooserForAttachmentBlock ();
            }
        }

        private void ShowChooserOrToggleCompactness()
        {
            if (0 == list.Count) {
                ShowChooser ();
            } else {
                ToggleCompactness ();
            }
        }

        private void ToggleCompactness ()
        {
            if (null != owner) {
                owner.ToggleCompactForAttachmentBlock ();
            }
        }
    }
}

