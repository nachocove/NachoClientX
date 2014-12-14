﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Drawing;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Model;

namespace NachoClient.iOS
{
    public class AccountInfoView : UIView
    {
        public delegate void AccountSelectedCallback (McAccount account);

        public AccountSelectedCallback OnAccountSelected;

        protected UITapGestureRecognizer accountSettingsTapGesture;
        protected UITapGestureRecognizer.Token accountSettingsTapGestureHandlerToken;

        protected const float LINE_HEIGHT = 20;

        protected const int NAME_LABEL_TAG = 100;
        protected const int EMAIL_ADDRESS_LABEL_TAG = 101;
        protected const int USER_IMAGE_VIEW_TAG = 102;
        protected const int USER_LABEL_VIEW_TAG = 103;

        McAccount account;

        public AccountInfoView (RectangleF frame) : base (frame)
        {
            var accountInfoView = this;

            accountInfoView.BackgroundColor = UIColor.White;
            accountInfoView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountInfoView.Layer.BorderColor = A.Card_Border_Color;
            accountInfoView.Layer.BorderWidth = A.Card_Border_Width;
            accountSettingsTapGesture = new UITapGestureRecognizer ();
            accountSettingsTapGestureHandlerToken = accountSettingsTapGesture.AddTarget (AccountSettingsTapHandler);
            accountInfoView.AddGestureRecognizer (accountSettingsTapGesture);

            var userImageView = new UIImageView (new RectangleF (12, 15, 50, 50));
            userImageView.Center = new PointF (userImageView.Center.X, accountInfoView.Frame.Height / 2);
            userImageView.Layer.CornerRadius = 25;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Hidden = true;
            userImageView.Tag = USER_IMAGE_VIEW_TAG;
            accountInfoView.AddSubview (userImageView);

            var userLabelView = new UILabel (new RectangleF (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Hidden = true;
            userLabelView.Tag = USER_LABEL_VIEW_TAG;
            accountInfoView.AddSubview (userLabelView);

            UILabel nameLabel = new UILabel (new RectangleF (75, 20, 100, LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            nameLabel.Tag = NAME_LABEL_TAG;
            accountInfoView.AddSubview (nameLabel);

            UILabel accountEmailAddress = new UILabel (new RectangleF (75, nameLabel.Frame.Bottom, 170, LINE_HEIGHT));
            accountEmailAddress.Tag = EMAIL_ADDRESS_LABEL_TAG;
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (accountEmailAddress);

            UIImageView accountSettingsIndicatorArrow;
            using (var disclosureIcon = UIImage.FromBundle ("gen-more-arrow")) {
                accountSettingsIndicatorArrow = new UIImageView (disclosureIcon);
            }
            accountSettingsIndicatorArrow.Frame = new RectangleF (accountEmailAddress.Frame.Right + 10, accountInfoView.Frame.Height / 2 - accountSettingsIndicatorArrow.Frame.Height / 2, accountSettingsIndicatorArrow.Frame.Width, accountSettingsIndicatorArrow.Frame.Height);
            accountInfoView.AddSubview (accountSettingsIndicatorArrow);
        }

        public void Configure (McAccount account)
        {
            this.account = account;

            var userImageView = (UIImageView)this.ViewWithTag (USER_IMAGE_VIEW_TAG);
            var userLabelView = (UILabel)this.ViewWithTag (USER_LABEL_VIEW_TAG);
            var nameLabel = (UILabel)this.ViewWithTag (NAME_LABEL_TAG);
            var emailLabel = (UILabel)this.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);

            if (null == account) {
                userImageView.Hidden = true;
                userLabelView.Hidden = true;
                nameLabel.Hidden = true;
                emailLabel.Hidden = true;
                return;
            }

            McContact userContact = McContact.QueryByEmailAddress (LoginHelpers.GetCurrentAccountId (), account.EmailAddr).FirstOrDefault ();

            var userImage = Util.ImageOfSender (LoginHelpers.GetCurrentAccountId (), account.EmailAddr);

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                int ColorIndex;
                string Initials;
                Util.UserMessageField (userContact.GetEmailAddress (), LoginHelpers.GetCurrentAccountId (), out ColorIndex, out Initials);
                userLabelView.Text = NachoCore.Utils.ContactsHelper.GetInitials (userContact);
                userLabelView.BackgroundColor = Util.ColorForUser (ColorIndex);
            }

            nameLabel.Text = userContact.FileAs;
            emailLabel.Text = userContact.GetEmailAddress ();
        }

        protected void AccountSettingsTapHandler (NSObject sender)
        {
            EndEditing (true);
            if (null != OnAccountSelected) {
                OnAccountSelected (this.account);
            }
        }

        public void Cleanup ()
        {
            accountSettingsTapGesture.RemoveTarget (accountSettingsTapGestureHandlerToken);
            RemoveGestureRecognizer (accountSettingsTapGesture);
        }
    }
}

