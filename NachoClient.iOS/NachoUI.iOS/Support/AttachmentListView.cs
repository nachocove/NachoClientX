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
    public enum TagType
    {
        ATTACHMENT_VIEW_TAG = 301,
        ATTACHMENT_LABEL_TAG = 302,
        ATTACHMENT_NUMBER_TAG = 303,
        // prevent duplicate tag #s with AttachmentView
        ATTACHMENT_IMAGE_TAG = AttachmentView.TagType.ATTACHMENT_IMAGE_TAG,
        ATTACHMENT_NAME_TAG = AttachmentView.TagType.ATTACHMENT_NAME_TAG,
    };

    public class AttachmentListView : ExpandableView
    {
        const float TOP_MARGIN = 5.0f;
        const float BOTTOM_MARGIN = 5.0f;

        public AttachmentView.AttachmentSelectedCallback OnAttachmentSelected;

        protected List<AttachmentView> attachmentViews;
        protected UILabel attachmentLabel;
        // the string "Attachment"
        protected UILabel numberLabel;
        // # of attachment

        private float CenterY;

        public AttachmentListView (RectangleF frame) : base (frame, false)
        {
            CenterY = frame.Height / 2;
            BackgroundColor = UIColor.White;

            attachmentViews = new List<AttachmentView> ();

            attachmentLabel = new UILabel (new RectangleF (0, 0, 1, 1));
            attachmentLabel.BackgroundColor = UIColor.White;
            attachmentLabel.Text = "Attachments";
            attachmentLabel.TextColor = A.Color_NachoTextGray;
            attachmentLabel.Font = A.Font_AvenirNextRegular17;
            attachmentLabel.SizeToFit ();
            ViewFramer.Create (attachmentLabel).Y (CenterY - (attachmentLabel.Frame.Height / 2));
            AddSubview (attachmentLabel);

            numberLabel = new UILabel (new RectangleF (attachmentLabel.Frame.Width + 10, TOP_MARGIN, 1, 1));
            numberLabel.BackgroundColor = A.Color_909090;
            numberLabel.TextColor = UIColor.White;
            numberLabel.Font = A.Font_AvenirNextDemiBold14;
            numberLabel.SizeToFit ();
            UpdateNumber ();
            AddSubview (numberLabel);

            CollapsedHeight = frame.Height;
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
            const float cornerSize = 6.0f;
            numberLabel.Text = attachmentViews.Count.ToString ();
            numberLabel.TextAlignment = UITextAlignment.Center;
            numberLabel.SizeToFit ();
            ViewFramer.Create (numberLabel).Height (20).AdjustWidth (2 * cornerSize);
            ViewFramer.Create (numberLabel).Y (CenterY- (numberLabel.Frame.Height / 2));
            numberLabel.Layer.CornerRadius = cornerSize;
            numberLabel.ClipsToBounds = true;
            ExpandedHeight += AttachmentView.VIEW_HEIGHT;
        }
    }
}

