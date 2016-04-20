//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using CoreGraphics;

using UIKit;
using Foundation;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class MessageHeaderView : UIView
    {
        /// <summary>
        /// Static message header that might be found on mesasge list or nacho now.
        /// The size of the content is about 60px.
        /// </summary>
        public MessageHeaderView ()
        {
        }

        public MessageHeaderView (CGRect rect) : base (rect)
        {
        }

        public EventHandler OnClickChili;

        const int FROM_TAG = 881;
        const int SUBJECT_TAG = 882;
        const int RECEIVED_DATE_TAG = 883;
        const int USER_CHILI_TAG = 884;
        const int ATTACHMENT_TAG = 885;

        const int CHILI_WIDTH = 24;
        const int CHILI_PADDING = 10;

        const int ATTACHMENT_WIDTH = 16;
        const int ATTACHMENT_PADDING = 10;

        public void CreateView ()
        {
            nfloat leftMargin = 0;
            nfloat rightMargin = 15;
            nfloat parentWidth = this.Frame.Width;

            nfloat yOffset = 15;

            // From label shares a line with the chili
            var fromLabelWidth = parentWidth - CHILI_WIDTH - CHILI_PADDING - rightMargin;
            var fromLabelView = new UILabel (new CGRect (leftMargin, yOffset, fromLabelWidth, 20));
            fromLabelView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = FROM_TAG;
            this.AddSubview (fromLabelView);

            // Chili image view, to the far right of From
            var chiliX = parentWidth - rightMargin - CHILI_WIDTH;
            var chiliImageView = new UIImageView (new CGRect (chiliX, yOffset, CHILI_WIDTH, CHILI_WIDTH));
            chiliImageView.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            chiliImageView.Tag = USER_CHILI_TAG;
            this.AddSubview (chiliImageView);

            chiliImageView.UserInteractionEnabled = true;
            var chiliTapGestureRecognizer = new UITapGestureRecognizer (new Action (() => {
                if (null != OnClickChili) {
                    OnClickChili (null, null);
                }
            }));
            chiliTapGestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                return false;
            };
            chiliTapGestureRecognizer.CancelsTouchesInView = true; // Prevents item from being selected

            // Make the chili touch area kind of biggish
            var chiliHitBox = new UIView (new CGRect (chiliX - 20, 0, parentWidth - chiliX + 20, chiliImageView.Frame.Bottom + 20));
            chiliHitBox.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            chiliHitBox.AddGestureRecognizer (chiliTapGestureRecognizer);
            chiliHitBox.BackgroundColor = UIColor.Clear;
            this.AddSubview (chiliHitBox);

            yOffset += 20;

            // Subject label view has a line to itself
            var subjectLabelView = new UILabel (new CGRect (leftMargin, yOffset, parentWidth - leftMargin - rightMargin, 20));
            subjectLabelView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            this.AddSubview (subjectLabelView);

            yOffset += 20;

            // Received label view shares a line with the attachment clip
            var receivedLabelView = new UILabel (new CGRect (leftMargin, yOffset, parentWidth - leftMargin - rightMargin, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.Tag = RECEIVED_DATE_TAG;
            this.AddSubview (receivedLabelView);

            // Attachment image view goes near the received label
            var attachmentX = parentWidth - rightMargin - ATTACHMENT_WIDTH;
            var attachmentImageView = new UIImageView (new CGRect (attachmentX, yOffset + 2, 16, 16));
            attachmentImageView.AutoresizingMask = UIViewAutoresizing.FlexibleLeftMargin;
            attachmentImageView.Tag = ATTACHMENT_TAG;
            this.AddSubview (attachmentImageView);

            this.BringSubviewToFront (chiliImageView);
            this.BringSubviewToFront (chiliHitBox);
        }

        public void ConfigureMessageView (McEmailMessageThread thread, McEmailMessage message)
        {
            // From label view
            var fromLabelView = this.ViewWithTag (FROM_TAG) as UILabel;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextRegular17 : A.Font_AvenirNextDemiBold17);
            fromLabelView.Hidden = false;

            // Chili image view
            string chiliImageIcon;
            if (thread.HasMultipleMessages ()) {
                chiliImageIcon = (message.isHot () ? "email-hotthread" : "email-nothothread");
            } else {
                chiliImageIcon = (message.isHot () ? "email-hot" : "email-not-hot");
            }
            var chiliImageView = this.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            using (var image = UIImage.FromBundle (chiliImageIcon)) {
                chiliImageView.Image = image;
            }
            chiliImageView.Hidden = false;

            // Subject label view
            var subjectLabelView = this.ViewWithTag (SUBJECT_TAG) as UILabel;
            string subject = EmailHelper.CreateSubjectWithIntent (message.Subject, message.Intent, message.IntentDateType, message.IntentDate);
            if (String.IsNullOrEmpty (subject)) {
                subjectLabelView.Text = Pretty.NoSubjectString ();
                subjectLabelView.TextColor = A.Color_9B9B9B;
                subjectLabelView.Font = A.Font_AvenirNextRegular17;
            } else {
                subjectLabelView.TextColor = A.Color_0F424C;
                subjectLabelView.Text = subject;
                subjectLabelView.Font = A.Font_AvenirNextRegular17;
            }
            subjectLabelView.Hidden = false;

            // Received label view
            var receivedLabelView = this.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.MediumFullDateTime (message.DateReceived);
            receivedLabelView.SizeToFit ();
            receivedLabelView.Hidden = false;

            // Attachment image view
            var attachmentImageView = this.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            if (message.cachedHasAttachments) {
                attachmentImageView.Hidden = false;
                using (var image = UIImage.FromBundle ("inbox-icn-attachment")) {
                    attachmentImageView.Image = image;
                }
            } else { 
                attachmentImageView.Hidden = true;
            }
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelView.Frame.Right + 10;
            attachmentImageView.Frame = attachmentImageRect;
        }

        public void ConfigureDraftView (McEmailMessageThread thread, McEmailMessage message)
        {
            // To label view
            var fromLabelView = this.ViewWithTag (FROM_TAG) as UILabel;
            fromLabelView.Text = Pretty.RecipientString (message.To);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextRegular17 : A.Font_AvenirNextDemiBold17);
            fromLabelView.Hidden = false;

            // Chili image view
            var chiliImageView = this.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            chiliImageView.Hidden = true;

            // Subject label view
            var subjectLabelView = this.ViewWithTag (SUBJECT_TAG) as UILabel;
            string subject = EmailHelper.CreateSubjectWithIntent (message.Subject, message.Intent, message.IntentDateType, message.IntentDate);
            if (String.IsNullOrEmpty (subject)) {
                subjectLabelView.Text = Pretty.NoSubjectString ();
                subjectLabelView.TextColor = A.Color_9B9B9B;
                subjectLabelView.Font = A.Font_AvenirNextRegular17;
            } else {
                subjectLabelView.TextColor = A.Color_0F424C;
                subjectLabelView.Text = subject;
                subjectLabelView.Font = A.Font_AvenirNextRegular17;
            }
            subjectLabelView.Hidden = false;

            // Received label view
            var receivedLabelView = this.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.MediumFullDateTime (message.DateReceived);
            receivedLabelView.SizeToFit ();
            receivedLabelView.Hidden = false;

            // Attachment image view
            var attachmentImageView = this.ViewWithTag (ATTACHMENT_TAG) as UIImageView;
            if (message.cachedHasAttachments) {
                attachmentImageView.Hidden = false;
                using (var image = UIImage.FromBundle ("inbox-icn-attachment")) {
                    attachmentImageView.Image = image;
                }
            } else { 
                attachmentImageView.Hidden = true;
            }
            var attachmentImageRect = attachmentImageView.Frame;
            attachmentImageRect.X = receivedLabelView.Frame.Right + 10;
            attachmentImageView.Frame = attachmentImageRect;
        }


        // Opaque background prevents blending penalty
        public void SetAllBackgroundColors (UIColor color)
        {
            this.BackgroundColor = color;
            this.ViewWithTag (FROM_TAG).BackgroundColor = color;
            this.ViewWithTag (SUBJECT_TAG).BackgroundColor = color;
            this.ViewWithTag (RECEIVED_DATE_TAG).BackgroundColor = color;
            this.ViewWithTag (USER_CHILI_TAG).BackgroundColor = color;
            this.ViewWithTag (ATTACHMENT_TAG).BackgroundColor = color;
        }
    }
}

