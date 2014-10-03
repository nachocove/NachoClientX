//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.IO;

using MonoTouch.UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class AttachmentView : UIView
    {
        public delegate void AttachmentSelectedCallback (McAttachment attachment);

        protected McAttachment attachment;
        protected UIImageView imageView;
        protected UILabel labelView;
        protected UILabel sizeView;
        protected UIView separatorView;

        public AttachmentSelectedCallback OnAttachmentSelected;

        const float ICON_SIZE = 20.0f;
        const float ICON_GAP = 10.0f;
        const float LINE_HEIGHT = 20.0f;
        const float TOP_MARGIN = 5.0f;
        const float BOTTOM_MARGIN = 2.0f;
        const float SEPARATOR_HEIGHT = 1.0f;
        public const float VIEW_HEIGHT = (2 * LINE_HEIGHT) + TOP_MARGIN + BOTTOM_MARGIN + SEPARATOR_HEIGHT;

        public enum TagType
        {
            ATTACHMENT_IMAGE_TAG = 310,
            ATTACHMENT_NAME_TAG = 311,
            ATTACHMENT_SIZE_TAG = 312,
            ATTAcHMENT_SEPARATOR_TAG = 313
        }

        public AttachmentView (RectangleF frame, McAttachment anAttachment) : base (frame)
        {
            attachment = anAttachment;

            imageView = new UIImageView (new RectangleF (0, TOP_MARGIN, ICON_SIZE, ICON_SIZE));
            imageView.Image = UIImage.FromBundle ("icn-attach-files");
            imageView.Tag = (int)TagType.ATTACHMENT_IMAGE_TAG;
            imageView.UserInteractionEnabled = false;
            AddSubview (imageView);

            labelView = new UILabel (new RectangleF (ICON_SIZE + ICON_GAP, TOP_MARGIN, 1, LINE_HEIGHT));
            labelView.TextColor = A.Color_NachoGreen;
            labelView.Font = A.Font_AvenirNextMedium14;
            labelView.Tag = (int)TagType.ATTACHMENT_NAME_TAG;
            labelView.Text = attachment.DisplayName;
            labelView.UserInteractionEnabled = false;
            labelView.SizeToFit ();
            ViewFramer.Create (labelView).Height (LINE_HEIGHT);
            AddSubview (labelView);

            sizeView = new UILabel (new RectangleF (ICON_SIZE + ICON_GAP, TOP_MARGIN + LINE_HEIGHT, 1, LINE_HEIGHT));
            sizeView.Text = GetAttachmentSizeString ();
            sizeView.TextColor = A.Color_NachoLightText;
            sizeView.Font = A.Font_AvenirNextMedium12;
            sizeView.Tag = (int)TagType.ATTACHMENT_SIZE_TAG;
            sizeView.UserInteractionEnabled = false;
            sizeView.SizeToFit ();
            ViewFramer.Create (sizeView).Height (LINE_HEIGHT);
            AddSubview (sizeView);

            separatorView = new UIView (new RectangleF (ICON_SIZE + ICON_GAP, VIEW_HEIGHT - 1.0f,
                Frame.Width - ICON_SIZE, SEPARATOR_HEIGHT));
            separatorView.BackgroundColor = A.Color_NachoLightBorderGray;
            separatorView.Tag = (int)TagType.ATTAcHMENT_SEPARATOR_TAG;
            AddSubview (separatorView);
            HideSeparator ();

            // Add a gesture recognizer for tapping
            var tapGestureRecognizer = new UITapGestureRecognizer ();
            tapGestureRecognizer.NumberOfTouchesRequired = 1;
            tapGestureRecognizer.NumberOfTapsRequired = 1;
            tapGestureRecognizer.AddTarget (this, new MonoTouch.ObjCRuntime.Selector ("TapSelector:"));
            tapGestureRecognizer.Enabled = true;
            AddGestureRecognizer (tapGestureRecognizer);
        }

        protected string GetAttachmentSizeString ()
        {
            string s;
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                try {
                    var fileInfo = new FileInfo (attachment.GetFilePath ());
                    s = Pretty.PrettyFileSize (fileInfo.Length);
                }
                catch (Exception e) {
                    Log.Warn (Log.LOG_UI, "fail to read file ({0})", e.Message);
                    s = "0B";
                }
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                s = "Loading...";
                break;
            default:
                s = "[Tap to download]";
                break;
            }
            return s;
        }

        [MonoTouch.Foundation.Export ("TapSelector:")]
        protected void TapSelector (UIGestureRecognizer sender)
        {
            OnAttachmentSelected (attachment);
        }

        public void ShowSeparator ()
        {
            separatorView.Hidden = false;
        }

        public void HideSeparator ()
        {
            separatorView.Hidden = true;
        }
    }
}

