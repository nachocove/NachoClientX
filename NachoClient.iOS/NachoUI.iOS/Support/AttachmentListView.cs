//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;

using MonoTouch.UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public enum TagType {
        ATTACHMENT_VIEW_TAG = 301,
        ATTACHMENT_LABEL_TAG = 302,
        ATTACHMENT_NUMBER_TAG = 303,
        ATTACHMENT_IMAGE_TAG = AttachmentView.TagType.ATTACHMENT_IMAGE_TAG,
        ATTACHMENT_NAME_TAG = AttachmentView.TagType.ATTACHMENT_NAME_TAG,
    };

    public class AttachmentListView : ExpandableView
    {
        const float TOP_MARGIN = 5.0f;
        const float BOTTOM_MARGIN = 5.0f;
        const float LINE_HEIGHT = 20.0f;

        public AttachmentView.AttachmentSelectedCallback OnAttachmentSelected;

        protected List<AttachmentView> attachmentViews;
        protected UILabel attachmentLabel; // the string "Attachment"
        protected UILabel numberLabel; // # of attachment

        public AttachmentListView (RectangleF frame) : base (frame, false)
        {
            BackgroundColor = UIColor.White;

            attachmentViews = new List<AttachmentView> ();

            // Adjust down the expand button
            ViewFramer.Create (expandedButton).AdjustY (TOP_MARGIN);

            attachmentLabel = new UILabel (new RectangleF (0, TOP_MARGIN, 1, 1));
            attachmentLabel.BackgroundColor = UIColor.White;
            attachmentLabel.Text = "Attachments";
            attachmentLabel.TextColor = A.Color_NachoLightText;
            attachmentLabel.Font = A.Font_AvenirNextRegular17;
            attachmentLabel.SizeToFit ();
            AddSubview (attachmentLabel);

            numberLabel = new UILabel (new RectangleF (attachmentLabel.Frame.Width + 10, TOP_MARGIN, 1, 1));
            numberLabel.BackgroundColor = A.Color_NachoTeal;
            numberLabel.TextColor = UIColor.White;
            numberLabel.Font = A.Font_AvenirNextDemiBold17;
            numberLabel.SizeToFit ();
            UpdateNumber ();
            AddSubview (numberLabel);

            CollapsedHeight = TOP_MARGIN + LINE_HEIGHT + BOTTOM_MARGIN;
            Reset ();
        }

        public void Reset ()
        {
            foreach (var view in attachmentViews) {
                view.RemoveFromSuperview ();
            }
            attachmentViews.Clear ();
            ExpandedHeight = CollapsedHeight;
        }

        public void AddAttachment (McAttachment attachment)
        {
            var frame = new RectangleF (0, ExpandedHeight + 1.0f, Frame.Width, 20.0f);
            var attachmentView = new AttachmentView (frame, attachment);
            attachmentView.OnAttachmentSelected = OnAttachmentSelected;
            attachmentViews.Add (attachmentView);
            AddSubview (attachmentView);
            UpdateNumber ();
        }

        public AttachmentView LastAttachmentView ()
        {
            return attachmentViews.Last ();
        }

        protected void UpdateNumber ()
        {
            const float cornerSize = 7.0f;
            numberLabel.Text = attachmentViews.Count.ToString ();
            numberLabel.TextAlignment = UITextAlignment.Center;
            numberLabel.SizeToFit ();
            ViewFramer.Create (numberLabel).Height (20).AdjustWidth (2 * cornerSize);
            numberLabel.Layer.CornerRadius = cornerSize;
            numberLabel.ClipsToBounds = true;
            ExpandedHeight += AttachmentView.VIEW_HEIGHT;
        }
    }
}

