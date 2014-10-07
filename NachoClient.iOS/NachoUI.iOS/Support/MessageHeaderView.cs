//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Drawing;

using MonoTouch.UIKit;
using MonoTouch.Foundation;

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

        public MessageHeaderView (RectangleF rect) : base (rect)
        {
        }

        public EventHandler OnClick;

        const int FROM_TAG = 881;
        const int SUBJECT_TAG = 882;
        const int RECEIVED_DATE_TAG = 883;
        const int USER_CHILI_TAG = 884;
        const int ATTACHMENT_TAG = 885;

        const int CHILI_WIDTH = 20;
        const int CHILI_PADDING = 10;

        const int ATTACHMENT_WIDTH = 16;
        const int ATTACHMENT_PADDING = 10;

        public void CreateView ()
        {
            float leftMargin = 0;
            float rightMargin = 0;
            float parentWidth = this.Frame.Width;

            // From label shares a line with the chili
            var fromLabelWidth = parentWidth - CHILI_WIDTH - CHILI_PADDING - rightMargin;
            var fromLabelView = new UILabel (new RectangleF (leftMargin, 0, fromLabelWidth, 20));
            fromLabelView.Font = A.Font_AvenirNextDemiBold17;
            fromLabelView.TextColor = A.Color_0F424C;
            fromLabelView.Tag = FROM_TAG;
            this.AddSubview (fromLabelView);

            // Chili image view, to the far right of From
            var chiliX = parentWidth - rightMargin - CHILI_WIDTH;
            var chiliImageView = new UIImageView (new RectangleF (chiliX, 0, 20, 20));
            chiliImageView.Tag = USER_CHILI_TAG;
            this.AddSubview (chiliImageView);

            chiliImageView.UserInteractionEnabled = true;
            var chiliTapGestureRecognizer = new UITapGestureRecognizer (new NSAction (() => {
                OnClick(null, null);
            }));
            chiliTapGestureRecognizer.ShouldRecognizeSimultaneously = delegate {
                return true;
            };
            chiliImageView.AddGestureRecognizer (chiliTapGestureRecognizer);

            // Subject label view has a line to itself
            var subjectLabelView = new UILabel (new RectangleF (leftMargin, 20, parentWidth - leftMargin - rightMargin, 20));
            subjectLabelView.LineBreakMode = UILineBreakMode.TailTruncation;
            subjectLabelView.Font = A.Font_AvenirNextMedium14;
            subjectLabelView.TextColor = A.Color_0F424C;
            subjectLabelView.Tag = SUBJECT_TAG;
            this.AddSubview (subjectLabelView);

            // Received label view shares a line with the attachment clip
            var receivedLabelView = new UILabel (new RectangleF (leftMargin, 40, parentWidth - leftMargin - rightMargin, 20));
            receivedLabelView.Font = A.Font_AvenirNextRegular14;
            receivedLabelView.TextColor = A.Color_9B9B9B;
            receivedLabelView.Tag = RECEIVED_DATE_TAG;
            this.AddSubview (receivedLabelView);

            // Attachment image view goes near the received label
            var attachmentX = parentWidth - rightMargin - ATTACHMENT_WIDTH;
            var attachmentImageView = new UIImageView (new RectangleF (attachmentX, 42, 16, 16));
            attachmentImageView.Tag = ATTACHMENT_TAG;
            this.AddSubview (attachmentImageView);
        }

        public void ConfigureView (McEmailMessage message)
        {
            // From label view
            var fromLabelView = this.ViewWithTag (FROM_TAG) as UILabel;
            fromLabelView.Text = Pretty.SenderString (message.From);
            fromLabelView.Font = (message.IsRead ? A.Font_AvenirNextRegular17 : A.Font_AvenirNextDemiBold17);
            fromLabelView.Hidden = false;

            // Chili image view
            var chiliImageView = this.ViewWithTag (USER_CHILI_TAG) as UIImageView;
            var chiliImageIcon = (message.isHot () ? "email-hot" : "email-not-hot");
            using (var image = UIImage.FromBundle (chiliImageIcon)) {
                chiliImageView.Image = image;
            }
            chiliImageView.Hidden = false;

            // Subject label view
            var subjectLabelView = this.ViewWithTag (SUBJECT_TAG) as UILabel;
            subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            if (String.IsNullOrEmpty (message.Subject)) {
                subjectLabelView.Text = Pretty.NoSubjectString ();
                subjectLabelView.TextColor = A.Color_9B9B9B;
            } else {
                subjectLabelView.Text = Pretty.SubjectString (message.Subject);
            }
            subjectLabelView.Hidden = false;

            // Received label view
            var receivedLabelView = this.ViewWithTag (RECEIVED_DATE_TAG) as UILabel;
            receivedLabelView.Text = Pretty.FullDateTimeString (message.DateReceived);
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


    }
}

