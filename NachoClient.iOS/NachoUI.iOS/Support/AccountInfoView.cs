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
        protected const float LINE_HEIGHT = 20;

        protected const int NAME_LABEL_TAG = 100;
        protected const int EMAIL_ADDRESS_LABEL_TAG = 101;
        protected const int ACCOUNT_IMAGE_VIEW_TAG = 102;
        protected const int USER_LABEL_VIEW_TAG = 103;

        public AccountInfoView (CGRect frame) : base (frame)
        {
            var accountInfoView = this;

            accountInfoView.BackgroundColor = UIColor.White;
            accountInfoView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountInfoView.Layer.BorderColor = A.Card_Border_Color;
            accountInfoView.Layer.BorderWidth = A.Card_Border_Width;

            var accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Center = new CGPoint (accountImageView.Center.X, accountInfoView.Frame.Height / 2);
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.Hidden = true;
            accountImageView.Tag = ACCOUNT_IMAGE_VIEW_TAG;
            accountInfoView.AddSubview (accountImageView);

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

        public string GetImageName (McAccount.AccountServiceEnum service)
        {
            string imageName;

            // FIXME
            // imageName = "avatar-office365";

            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                imageName = "avatar-msexchange";
                break;
            case McAccount.AccountServiceEnum.GoogleDefault:
                imageName = "avatar-gmail";
                break;
            case McAccount.AccountServiceEnum.GoogleExchange:
                imageName = "avatar-googleapps";
                break;
            case McAccount.AccountServiceEnum.HotmailExchange:
                imageName = "avatar-hotmail";
                break;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                        // FIXME
                imageName = "avatar-yahoo";
                break;
            case McAccount.AccountServiceEnum.OutlookExchange:
                imageName = "avatar-outlook";
                break;
            default:
                imageName = String.Empty;
                NcAssert.CaseError ();
                break;
            }
            return imageName;
        }

        public void Configure (McAccount account)
        {
            var accountImageView = (UIImageView)this.ViewWithTag (ACCOUNT_IMAGE_VIEW_TAG);
            var userLabelView = (UILabel)this.ViewWithTag (USER_LABEL_VIEW_TAG);
            var nameLabel = (UILabel)this.ViewWithTag (NAME_LABEL_TAG);
            var emailLabel = (UILabel)this.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);

            userLabelView.Hidden = true;
            nameLabel.Hidden = (null == account);
            emailLabel.Hidden = (null == account);
            accountImageView.Hidden = (null == account);

            if (null != account) {
                nameLabel.Text = Pretty.AccountName (account);
                if (0 == account.DisplayPortraitId) {
                    using (var image = UIImage.FromBundle (GetImageName (account.AccountService))) {
                        accountImageView.Image = image;
                    }
                } else {
                    using (var image = Util.PortraitToImage (account.DisplayPortraitId)) {
                        accountImageView.Image = image;
                    }
                }
                emailLabel.Text = account.EmailAddr;
            }

        }

        public void Cleanup ()
        {
        }
    }
}

