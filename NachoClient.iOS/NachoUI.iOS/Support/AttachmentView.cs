﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using MonoTouch.Foundation;


using MonoTouch.UIKit;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class AttachmentView : UIView
    {
        public delegate void AttachmentSelectedCallback (McAttachment attachment);

        protected McAttachment attachment;
        protected UIImageView imageView;
        protected UILabel filenameView;
        protected UILabel detailView;
        protected UIView separatorView;
        protected UIImageView downloadImageView;
        protected UITapGestureRecognizer singleTapGesture;
        protected UITapGestureRecognizer.Token singleTapGestureHandlerToken;

        private bool animationIsRunning = false;
        private bool statusIndicatorIsRegistered = false;

        public AttachmentSelectedCallback OnAttachmentSelected;

        const float ICON_SIZE = 24.0f;
        const float ICON_GAP = 10.0f;
        const float LINE_HEIGHT = 20.0f;
        const float TOP_MARGIN = 5.0f;
        const float BOTTOM_MARGIN = 2.0f;
        const float SEPARATOR_HEIGHT = 1.0f;
        const float DOWNLOAD_ICON_SIZE = 16.0f;
        public const float VIEW_HEIGHT = (2 * LINE_HEIGHT) + TOP_MARGIN + BOTTOM_MARGIN + SEPARATOR_HEIGHT;

        string DownloadArrow = "email-att-download-arrow";
        string DownloadLine = "email-att-download-vline";
        string DownloadCircle = "email-att-download-circle";

        public enum TagType
        {
            ATTACHMENT_IMAGE_TAG = 310,
            ATTACHMENT_NAME_TAG = 311,
            ATTACHMENT_SIZE_TAG = 312,
            ATTACHMENT_SEPARATOR_TAG = 313,
            DOWNLOAD_IMAGEVIEW_TAG = 314,
        }

        public AttachmentView (RectangleF frame, McAttachment anAttachment) : base (frame)
        {
            attachment = anAttachment;

            var imageY = (VIEW_HEIGHT / 2) - (ICON_SIZE / 2);
            imageView = new UIImageView (new RectangleF (0, imageY, ICON_SIZE, ICON_SIZE));
            if (Pretty.TreatLikeAPhoto (attachment.DisplayName)) {
                imageView.Image = UIImage.FromBundle ("email-att-photos");
            } else {
                imageView.Image = UIImage.FromBundle ("email-att-files");
            }
            imageView.Tag = (int)TagType.ATTACHMENT_IMAGE_TAG;
            imageView.UserInteractionEnabled = false;
            AddSubview (imageView);

            var leftMargin = ICON_SIZE + ICON_GAP;
            var lineLength = frame.Width - leftMargin - DOWNLOAD_ICON_SIZE - 8;
            filenameView = new UILabel (new RectangleF (leftMargin, TOP_MARGIN, lineLength, LINE_HEIGHT));
            filenameView.TextColor = A.Color_NachoDarkText;
            filenameView.Font = A.Font_AvenirNextDemiBold14;
            filenameView.Tag = (int)TagType.ATTACHMENT_NAME_TAG;
            filenameView.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);
            filenameView.UserInteractionEnabled = false;
            ViewFramer.Create (filenameView).Height (LINE_HEIGHT);
            AddSubview (filenameView);

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

            detailView = new UILabel (new RectangleF (leftMargin, TOP_MARGIN + LINE_HEIGHT, lineLength, LINE_HEIGHT));
            detailView.Text = detailText;
            detailView.TextColor = A.Color_NachoTextGray;
            detailView.Font = A.Font_AvenirNextRegular14;
            detailView.Tag = (int)TagType.ATTACHMENT_SIZE_TAG;
            detailView.UserInteractionEnabled = false;
            detailView.SizeToFit ();
            ViewFramer.Create (detailView).Height (LINE_HEIGHT);
            AddSubview (detailView);

            //Download image view
            downloadImageView = new UIImageView (new RectangleF (frame.Width - 18 - 16, (frame.Height / 2) - 8, 16, 16)); 
            downloadImageView.Tag = (int)TagType.DOWNLOAD_IMAGEVIEW_TAG;
            downloadImageView.Image = UIImage.FromBundle ("email-att-download");

            AddSubview (downloadImageView);

            separatorView = new UIView (new RectangleF (ICON_SIZE + ICON_GAP, VIEW_HEIGHT - 1.0f,
                Frame.Width - ICON_SIZE, SEPARATOR_HEIGHT));
            separatorView.BackgroundColor = A.Color_NachoLightBorderGray;
            separatorView.Tag = (int)TagType.ATTACHMENT_SEPARATOR_TAG;
            AddSubview (separatorView);
            HideSeparator ();

            // Add a gesture recognizer for tapping
            singleTapGesture = new UITapGestureRecognizer ();
            singleTapGesture.NumberOfTouchesRequired = 1;
            singleTapGesture.NumberOfTapsRequired = 1;
            singleTapGestureHandlerToken = singleTapGesture.AddTarget (SingleTapHandler);
            singleTapGesture.Enabled = true;
            AddGestureRecognizer (singleTapGesture);
           
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
                downloadImageView.Hidden = false;
                UserInteractionEnabled = true;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Error:
                downloadImageView.Hidden = false;
                UserInteractionEnabled = true;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                downloadImageView.Hidden = false;
                UserInteractionEnabled = false;
                StartArrowAnimation ();
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                downloadImageView.Hidden = true;
                UserInteractionEnabled = true;
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
        }

        protected void MaybeStartAnimation ()
        {
            if (!animationIsRunning) {
                StartDownloadingAnimation ();
                animationIsRunning = true;
            }
        }

        protected void MaybeStopAnimation ()
        {
            if (animationIsRunning) {
                StopAnimations ();
                animationIsRunning = false;
            }
        }

        protected void MaybeRegisterStatusInd ()
        {
            if (!statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                statusIndicatorIsRegistered = true;
            }
        }

        protected void MaybeUnregisterStatusInd ()
        {
            if (statusIndicatorIsRegistered) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                statusIndicatorIsRegistered = false;
            }
        }

        private void SingleTapHandler (NSObject sender)
        {
            if (null == attachment) {
                return;
            }
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
            case McAbstrFileDesc.FilePresenceEnum.Error:
                StartDownload ();
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                if (null != OnAttachmentSelected) {
                    OnAttachmentSelected (attachment);
                }
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
        }

        private void StartDownload ()
        {
            MaybeRegisterStatusInd ();
            var downloadToken = PlatformHelpers.DownloadAttachment (attachment);
            if (null == downloadToken) {
                attachment = McAttachment.QueryById<McAttachment> (attachment.Id);
                RefreshStatus ();
                return;
            }
            UserInteractionEnabled = false;
            MaybeStartAnimation ();
        }

        private void ShowErrorMessage ()
        {
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var statusEvent = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_AttDownloadUpdate == statusEvent.Status.SubKind || NcResult.SubKindEnum.Error_AttDownloadFailed == statusEvent.Status.SubKind) {
                RefreshStatus ();
            }
        }

        protected void RefreshStatus ()
        {
            if (null == attachment) {
                return;
            }
            attachment = McAttachment.QueryById<McAttachment> (attachment.Id);
            if (null == attachment) {
                return;
            }
            switch (attachment.FilePresence) {
            case McAbstrFileDesc.FilePresenceEnum.None:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Partial:
                break;
            case McAbstrFileDesc.FilePresenceEnum.Error:
                MaybeStopAnimation ();
                MaybeUnregisterStatusInd ();
                UserInteractionEnabled = true;
                break;
            case McAbstrFileDesc.FilePresenceEnum.Complete:
                MaybeStopAnimation ();
                MaybeUnregisterStatusInd ();
                UserInteractionEnabled = true;
                downloadImageView.Hidden = true;
                break;
            default:
                NachoCore.Utils.NcAssert.CaseError ();
                break;
            }
        }

        public void ShowSeparator ()
        {
            separatorView.Hidden = false;
        }

        public void HideSeparator ()
        {
            separatorView.Hidden = true;
        }

        // Do arrow with line animation followed by repeating arrow-only animations
        public void StartDownloadingAnimation ()
        {
            var iv = this.ViewWithTag ((int)TagType.DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            iv.Image = UIImage.FromBundle (DownloadCircle);
            UIImageView line = new UIImageView (UIImage.FromBundle (DownloadLine));
            UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
            iv.AddSubview (line);
            iv.AddSubview (arrow);

            PointF center = line.Center;
            UIView.Animate (
                duration: 0.4, 
                delay: 0, 
                options: UIViewAnimationOptions.CurveEaseIn,
                animation: () => {
                    line.Center = new PointF (center.X, iv.Image.Size.Height * 3 / 4);
                    arrow.Center = new PointF (center.X, iv.Image.Size.Height * 3 / 4);
                    line.Alpha = 0.0f;
                    arrow.Alpha = 0.4f;
                },
                completion: () => {
                    arrow.Center = new PointF (center.X, 2);
                    arrow.Alpha = 1.0f;
                    ArrowAnimation (arrow, center);
                }
            );
        }

        // Start only the arrow animation
        public void StartArrowAnimation ()
        {
            var iv = this.ViewWithTag ((int)TagType.DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            iv.Image = UIImage.FromBundle (DownloadCircle);
            UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
            iv.AddSubview (arrow);

            ArrowAnimation (arrow, arrow.Center);
        }

        private void ArrowAnimation (UIImageView arrow, PointF center)
        {
            var iv = this.ViewWithTag ((int)TagType.DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            UIView.Animate (0.4, 0, (UIViewAnimationOptions.Repeat | UIViewAnimationOptions.OverrideInheritedDuration | UIViewAnimationOptions.OverrideInheritedOptions | UIViewAnimationOptions.OverrideInheritedCurve | UIViewAnimationOptions.CurveLinear), () => {
                arrow.Center = new PointF (center.X, iv.Frame.Size.Height * 3 / 4);
                arrow.Alpha = 0.4f;
            }, (() => { 
            }));
        }

        public void StopAnimations ()
        {
            var iv = this.ViewWithTag ((int)TagType.DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            foreach (UIView subview in iv) {
                subview.Layer.RemoveAllAnimations ();
                subview.RemoveFromSuperview ();
            }
        }

        protected override void Dispose (bool disposing)
        {
            Cleanup ();
            base.Dispose (disposing);
        }

        public void Cleanup ()
        {
            MaybeStopAnimation ();
            MaybeUnregisterStatusInd ();

            // Clean up gesture recognizers.
            singleTapGesture.RemoveTarget (singleTapGestureHandlerToken);
            RemoveGestureRecognizer (singleTapGesture);
        }

    }
}

