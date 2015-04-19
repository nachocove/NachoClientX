//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;
using System.Linq;
using System.Collections.Generic;

using UIKit;

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
        static readonly nfloat TOP_MARGIN = 5.0f;
        protected nfloat attachmentCellIndent = 0f;

        public AttachmentView.AttachmentErrorCallback OnAttachmentError;
        public AttachmentView.AttachmentSelectedCallback OnAttachmentSelected;

        protected List<AttachmentView> attachmentViews;
        protected UILabel attachmentLabel;
        // the string "Attachment"
        protected UILabel numberLabel;
        // # of attachment

        private nfloat CenterY;

        public AttachmentListView (CGRect frame) : base (frame, false)
        {
            CenterY = frame.Height / 2;
            BackgroundColor = UIColor.White;

            attachmentViews = new List<AttachmentView> ();
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
            var frame = new CGRect (attachmentCellIndent, ExpandedHeight + 1.0f, Frame.Width - attachmentCellIndent, AttachmentView.VIEW_HEIGHT);
            var attachmentView = new AttachmentView (frame, attachment);
            attachmentView.OnAttachmentSelected = OnAttachmentSelected;
            attachmentView.OnAttachmentError = OnAttachmentError;
            attachmentViews.Add (attachmentView);
            AddSubview (attachmentView);
            UpdateNumber ();
        }

        public void SetHeader (string text, UIFont font, UIColor textColor, UIImageView iconImage, UIFont numberFont, UIColor numberTextColor, UIColor numberBGColor, nfloat numberOffset)
        {
            attachmentLabel = new UILabel (new CGRect (0, 0, 1, 1));
            attachmentLabel.BackgroundColor = UIColor.White;
            attachmentLabel.Text = text;
            attachmentLabel.TextColor = textColor;
            attachmentLabel.Font = font;
            attachmentLabel.SizeToFit ();
            ViewFramer.Create (attachmentLabel).Y (CenterY - (attachmentLabel.Frame.Height / 2));
            AddSubview (attachmentLabel);
            if (null != iconImage) {
                ViewFramer.Create (attachmentLabel).X (42);
                iconImage.Frame = new CGRect (18, CenterY - 8, 16, 16);
                AddSubview (iconImage);
            } 

            numberLabel = new UILabel (new CGRect (attachmentLabel.Frame.X + attachmentLabel.Frame.Width + numberOffset, TOP_MARGIN - 1, 1, 1));
            numberLabel.BackgroundColor = numberBGColor;
            numberLabel.TextColor = numberTextColor;
            numberLabel.Font = numberFont;
            numberLabel.SizeToFit ();
            UpdateNumber ();
            AddSubview (numberLabel);
        }

        public void SetAttachmentCellIndent (nfloat indent)
        {
            this.attachmentCellIndent = indent;
        }

        public AttachmentView LastAttachmentView ()
        {
            return attachmentViews.Last ();
        }

        protected void UpdateNumber ()
        {
            nfloat cornerSize = 6.0f;
            numberLabel.Text = attachmentViews.Count.ToString ();
            numberLabel.TextAlignment = UITextAlignment.Center;
            numberLabel.SizeToFit ();
            ViewFramer.Create (numberLabel).Height (20).AdjustWidth (2 * cornerSize);
            ViewFramer.Create (numberLabel).Y (CenterY - (numberLabel.Frame.Height / 2));
            numberLabel.Layer.CornerRadius = cornerSize;
            numberLabel.ClipsToBounds = true;
            ExpandedHeight += AttachmentView.VIEW_HEIGHT;
        }

        protected new void Cleanup ()
        {
            base.Cleanup ();

            attachmentLabel = null;
            numberLabel = null;

            foreach (var av in attachmentViews) {
                av.Cleanup ();
            }
            attachmentViews.Clear ();
        }
    }
}

