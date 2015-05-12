//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using CoreGraphics;
using UIKit;
using Foundation;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class AccountInfoView : UIView
    {
        public delegate void AccountSelectedCallback (McAccount account);

        protected const float LINE_HEIGHT = 20;

        protected const int NAME_LABEL_TAG = 100;
        protected const int EMAIL_ADDRESS_LABEL_TAG = 101;
        protected const int USER_IMAGE_VIEW_TAG = 102;
        protected const int USER_LABEL_VIEW_TAG = 103;

        McAccount account;

        public AccountInfoView (CGRect frame) : base (frame)
        {
            var accountInfoView = this;

            accountInfoView.BackgroundColor = UIColor.White;
            accountInfoView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountInfoView.Layer.BorderColor = A.Card_Border_Color;
            accountInfoView.Layer.BorderWidth = A.Card_Border_Width;

            var userImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            userImageView.Center = new CGPoint (userImageView.Center.X, accountInfoView.Frame.Height / 2);
            userImageView.Layer.CornerRadius = 25;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Hidden = true;
            userImageView.Tag = USER_IMAGE_VIEW_TAG;
            accountInfoView.AddSubview (userImageView);

            var userLabelView = new UILabel (new CGRect (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Hidden = true;
            userLabelView.Tag = USER_LABEL_VIEW_TAG;
            accountInfoView.AddSubview (userLabelView);

            UILabel nameLabel = new UILabel (new CGRect (75, 20, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            nameLabel.Tag = NAME_LABEL_TAG;
            accountInfoView.AddSubview (nameLabel);

            UILabel accountEmailAddress = new UILabel (new CGRect (75, nameLabel.Frame.Bottom, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            accountEmailAddress.Tag = EMAIL_ADDRESS_LABEL_TAG;
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (accountEmailAddress);

            UIImageView accountSettingsIndicatorArrow;
            using (var disclosureIcon = UIImage.FromBundle ("gen-more-arrow")) {
                accountSettingsIndicatorArrow = new UIImageView (disclosureIcon);
            }
            accountSettingsIndicatorArrow.Frame = new CGRect (accountInfoView.Frame.Width - (accountSettingsIndicatorArrow.Frame.Width + 10), accountInfoView.Frame.Height / 2 - accountSettingsIndicatorArrow.Frame.Height / 2, accountSettingsIndicatorArrow.Frame.Width, accountSettingsIndicatorArrow.Frame.Height);
            accountInfoView.AddSubview (accountSettingsIndicatorArrow);
        }

        public void Configure (McAccount account)
        {
            this.account = account;

            var userImageView = (UIImageView)this.ViewWithTag (USER_IMAGE_VIEW_TAG);
            var userLabelView = (UILabel)this.ViewWithTag (USER_LABEL_VIEW_TAG);
            var nameLabel = (UILabel)this.ViewWithTag (NAME_LABEL_TAG);
            var emailLabel = (UILabel)this.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);

            userImageView.Hidden = true;
            userLabelView.Hidden = true;

            if (null == account) {
                nameLabel.Hidden = true;
                emailLabel.Hidden = true;
                return;
            }

            // Account name
            nameLabel.Text = Pretty.AccountName (account);

            // Email address
            var emailAddress = account.EmailAddr;
            emailLabel.Text = emailAddress;

            var userImage = Util.ImageOfSender (LoginHelpers.GetCurrentAccountId (), emailAddress);

            if (null != userImage) {
                userImageView.Image = userImage;
                userImageView.Hidden = false;
            } else {
                int ColorIndex;
                string Initials;
                Util.UserMessageField (emailAddress, LoginHelpers.GetCurrentAccountId (), out ColorIndex, out Initials);
                userLabelView.BackgroundColor = Util.ColorForUser (ColorIndex);
                userLabelView.Text = Initials;
                userLabelView.Hidden = false;
            }
        }

        public void Cleanup ()
        {
        }
    }
}

