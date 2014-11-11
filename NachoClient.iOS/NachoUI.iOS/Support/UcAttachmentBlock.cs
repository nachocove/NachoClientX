//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

using MonoTouch.UIKit;

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

        public McAttachment attachment;

        public UcAttachmentCell (McAttachment attachment, float parentWidth, bool editable) : base (new RectangleF (0, 0, parentWidth, LINE_HEIGHT))
        {
            this.attachment = attachment;
            CreateView (parentWidth, editable);
        }

        public void CreateView (float parentWidth, bool editable)
        {
            var xOffset = 0;
            if (editable) {
                //Remove icon
                var removeButton = new UIButton ();
                removeButton.Tag = REMOVE_BUTTON_TAG;
                removeButton.SetImage (UIImage.FromBundle ("gen-delete-small"), UIControlState.Normal);
                removeButton.Frame = new RectangleF (9, (LINE_HEIGHT / 2) - 17, 34, 34);
                this.AddSubview (removeButton);
                xOffset += 34;
            }

            //Cell icon
            var cellIconImageView = new UIImageView (); 
            cellIconImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            cellIconImageView.Frame = new RectangleF (xOffset + 18, 18, 24, 24);
            this.AddSubview (cellIconImageView);

            //Text label
            var textLabel = new UILabel (); 
            textLabel.Font = A.Font_AvenirNextDemiBold14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            textLabel.Frame = new RectangleF (xOffset + 60, 11, parentWidth - 60 - 52, 19.5f);
            this.AddSubview (textLabel);

            //Detail text label
            var detailTextlabel = new UILabel (); 
            detailTextlabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            detailTextlabel.Font = A.Font_AvenirNextRegular14;
            detailTextlabel.TextColor = A.Color_NachoTextGray;
            detailTextlabel.Frame = new RectangleF (xOffset + 60, 11 + 19.5f, parentWidth - 60 - 52, 19.5f);
            this.AddSubview (detailTextlabel);

            //Separator line
            var separatorLine = Util.AddHorizontalLineView (xOffset + 60, 60, parentWidth - 60, A.Color_NachoBorderGray);
            this.AddSubview (separatorLine);

            ConfigureView (attachment, cellIconImageView, textLabel, detailTextlabel);
        }

        protected void ConfigureView (McAttachment attachment, UIImageView iconView, UILabel textLabel, UILabel detailTextLabel)
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

                if (detailText.Contains ("JPG") || detailText.Contains ("JPEG")
                    || detailText.Contains ("TIFF") || detailText.Contains ("PNG")
                    || detailText.Contains ("GIF") || detailText.Contains ("RAW")) {
                    iconView.Image = UIImage.FromBundle ("email-att-photos");
                } else {
                    iconView.Image = UIImage.FromBundle ("email-att-files");
                }
            } else {
                textLabel.Text = "File no longer exists"; 
            }
        }
    }

    public class UcAttachmentBlock : UIView
    {
        protected int accountId;
        protected IUcAttachmentBlockDelegate owner;
        protected float parentWidth;
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

        public UcAttachmentBlock (IUcAttachmentBlockDelegate owner, int accountId, float parentWidth, int cellHeight, bool editable)
        {
            this.owner = owner;
            this.accountId = accountId;
            this.parentWidth = parentWidth;
            this.BackgroundColor = UIColor.White;
            this.CELL_HEIGHT = cellHeight;
            this.editable = editable;

            this.AutoresizingMask = UIViewAutoresizing.None;
            this.AutosizesSubviews = false;

            CreateView ();
        }

        public void SetCompact (bool isCompact)
        {
            this.isCompact = isCompact;
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
                Util.SetOriginalImagesForButton (chooserButton, "email-add", "email-add-active");
                chooserButton.SizeToFit ();
                chooserButton.Frame = new RectangleF (parentWidth - 43, 0, 40, CELL_HEIGHT);
                chooserButton.TouchUpInside += (object sender, EventArgs e) => {
                    if (null != owner) {
                        owner.PerformSegueForAttachmentBlock ("SegueToAddAttachment", new SegueHolder (null));
                    }
                };
                contentView.AddSubview (chooserButton);
            }

            // Enabled & disable 'compact view' with a tap
            var tap = new UITapGestureRecognizer ();
            tap.AddTarget (() => {
                isCompact = !isCompact;
                ConfigureView ();
            });
            contentView.AddGestureRecognizer (tap);
        }

        public void Append (McAttachment attachment)
        {
            var c = new UcAttachmentCell (attachment, parentWidth, editable);
            contentView.AddSubview (c);
            list.Add (c);

            var tap = new UITapGestureRecognizer ();
            tap.AddTarget (() => {
                if (null != owner) {
                    owner.DisplayAttachmentForAttachmentBlock (c.attachment);
                }
            });
            c.AddGestureRecognizer (tap);

            if (editable) {
                var RemoveButton = c.ViewWithTag (REMOVE_BUTTON_TAG) as UIButton;
                RemoveButton.TouchUpInside += (object sender, EventArgs e) => {
                    Remove (c);
                };
            }

            Layout ();
            ConfigureView ();
        } 

        public void Remove (UcAttachmentCell c)
        {
            list.Remove (c);
            c.RemoveFromSuperview ();

            Layout ();
            ConfigureView ();
        }

        /// Adjusts x & y on the top line of a view
        protected void AdjustXY (UIView view, float X, float Y)
        {
            view.Center = new PointF (X + (view.Frame.Width / 2), CELL_HEIGHT / 2);
        }

        public void ConfigureView ()
        {
            mainLabel.Text = String.Format ("Attachments ({0})", list.Count);

            foreach (var c in list) {
                c.Hidden = isCompact;
            }

            if (null != owner) {
                owner.AttachmentBlockNeedsLayout (this);
            }
        }

        public void Layout ()
        {
            float yOffset = 0;

            var mainLabelSize = mainLabel.StringSize (mainLabel.Text, mainLabel.Font);
            mainLabel.Frame = new RectangleF (mainLabel.Frame.Location, mainLabelSize);
            AdjustXY (mainLabel, LEFT_INDENT, yOffset);

            yOffset += CELL_HEIGHT;

            foreach (var c in list) {
                if (!c.Hidden) {
                    c.Frame = new RectangleF (0, yOffset, c.Frame.Width, c.Frame.Height);
                    yOffset += LINE_HEIGHT;
                }
            }

            contentView.Frame = new RectangleF (0, 0, parentWidth, yOffset);
            this.Frame = new RectangleF (this.Frame.Location, contentView.Frame.Size);
        }
    }
}

