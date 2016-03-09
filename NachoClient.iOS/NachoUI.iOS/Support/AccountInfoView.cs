//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using CoreGraphics;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.iOS
{
    public class AccountInfoView : UIView
    {
        protected const float LINE_HEIGHT = 20;

        UIImageView accountImageView;
        UILabel userLabelView;
        UILabel nameLabel;
        UILabel accountEmailAddress;
        UIImageView accountSettingsIndicatorArrow;
        UILabel unreadCountLabel;
        static nfloat UNREAD_COUNT_HEIGHT = 26.0f;
        static nfloat UNREAD_COUNT_PADDING = 4.0f;
        static nfloat UNREAD_COUNT_MARGIN_RIGHT = 15.0f;
        static nfloat UNREAD_COUNT_MARGIN_LEFT = 5.0f;

        public AccountInfoView (CGRect frame) : base (frame)
        {
            var accountInfoView = this;

            BackgroundColor = A.Color_NachoBackgroundGray;

            accountInfoView.BackgroundColor = UIColor.White;
            accountInfoView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountInfoView.Layer.BorderColor = A.Card_Border_Color;
            accountInfoView.Layer.BorderWidth = A.Card_Border_Width;

            accountImageView = new UIImageView (new CGRect (12, 15, 50, 50));
            accountImageView.Center = new CGPoint (accountImageView.Center.X, accountInfoView.Frame.Height / 2);
            accountImageView.Layer.CornerRadius = 25;
            accountImageView.Layer.MasksToBounds = true;
            accountImageView.ContentMode = UIViewContentMode.ScaleAspectFill;
            accountImageView.Hidden = true;
            accountInfoView.AddSubview (accountImageView);

            userLabelView = new UILabel (new CGRect (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Hidden = true;
            accountInfoView.AddSubview (userLabelView);

            nameLabel = new UILabel (new CGRect (75, 20, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (nameLabel);

            accountEmailAddress = new UILabel (new CGRect (75, nameLabel.Frame.Bottom, accountInfoView.Frame.Width - 120, LINE_HEIGHT));
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountInfoView.AddSubview (accountEmailAddress);

            using (var disclosureIcon = UIImage.FromBundle ("gen-more-arrow")) {
                accountSettingsIndicatorArrow = new UIImageView (disclosureIcon);
            }
            accountSettingsIndicatorArrow.Frame = new CGRect (accountInfoView.Frame.Width - (accountSettingsIndicatorArrow.Frame.Width + 10), accountInfoView.Frame.Height / 2 - accountSettingsIndicatorArrow.Frame.Height / 2, accountSettingsIndicatorArrow.Frame.Width, accountSettingsIndicatorArrow.Frame.Height);
            accountInfoView.AddSubview (accountSettingsIndicatorArrow);

            unreadCountLabel = new UILabel (new CGRect (frame.Width - UNREAD_COUNT_HEIGHT - UNREAD_COUNT_MARGIN_RIGHT, (frame.Height - UNREAD_COUNT_HEIGHT) / 2.0f, UNREAD_COUNT_HEIGHT, UNREAD_COUNT_HEIGHT));
            unreadCountLabel.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleBottomMargin | UIViewAutoresizing.FlexibleLeftMargin;
            unreadCountLabel.Font = A.Font_AvenirNextDemiBold14;
            unreadCountLabel.TextColor = UIColor.White;
            unreadCountLabel.TextAlignment = UITextAlignment.Center;
            unreadCountLabel.LineBreakMode = UILineBreakMode.Clip;
            unreadCountLabel.BackgroundColor = UIColor.Red;
            unreadCountLabel.Layer.MasksToBounds = true;
            accountInfoView.AddSubview (unreadCountLabel);
        }

        public void Configure (McAccount account, bool showArrow, bool showUnreadCount)
        {
            userLabelView.Hidden = true;
            accountSettingsIndicatorArrow.Hidden = !showArrow;
            nameLabel.Hidden = (null == account);
            accountEmailAddress.Hidden = (null == account);
            accountImageView.Hidden = (null == account);

            if (null != account) {
                nameLabel.Text = Pretty.AccountName (account);
                using (var image = Util.ImageForAccount (account)) {
                    accountImageView.Image = image;
                }
                accountEmailAddress.Text = account.EmailAddr;

                if (showUnreadCount) {
                    UpdateUnreadMessageCount (account);
                } else if (LoginHelpers.ShouldAlertUser (account.Id)) {
                    HighlightError ();
                } else {
                    unreadCountLabel.Hidden = !showUnreadCount;
                }
            }
        }

        void UpdateUnreadMessageCount (McAccount account)
        {
            unreadCountLabel.Hidden = false;
            unreadCountLabel.Layer.CornerRadius = 4;
            unreadCountLabel.Text = "";
            SetUnreadCountWidth (UNREAD_COUNT_HEIGHT);

            NcTask.Run (() => {
                int unreadMessageCount;
                EmailHelper.GetUnreadMessageCount (account, out unreadMessageCount, EmailHelper.GetNewSincePreference ());
                InvokeOnUIThread.Instance.Invoke (() => {
                    if (100000 > unreadMessageCount) {
                        unreadCountLabel.Text = String.Format ("{0:N0}", unreadMessageCount);
                    } else {
                        unreadCountLabel.Text = "> 100K";
                    }
                    unreadCountLabel.SizeToFit ();
                    SetUnreadCountWidth ((nfloat)Math.Max ((double)UNREAD_COUNT_HEIGHT, (double)(unreadCountLabel.Frame.Width + 2.0f * UNREAD_COUNT_PADDING)));
                });
            }, "UpdateUnreadMessageCount");
        }

        void SetUnreadCountWidth (nfloat width)
        {
            Log.Info (Log.LOG_UI, "SetUnreadCountWidth {0}", width);
            unreadCountLabel.Frame = new CGRect (
                Frame.Width - width - UNREAD_COUNT_MARGIN_RIGHT,
                unreadCountLabel.Frame.Top,
                width,
                UNREAD_COUNT_HEIGHT
            );

            nameLabel.Frame = new CGRect (
                nameLabel.Frame.Left,
                nameLabel.Frame.Top,
                unreadCountLabel.Frame.Left - nameLabel.Frame.Left - UNREAD_COUNT_MARGIN_LEFT,
                nameLabel.Frame.Height
            );

            accountEmailAddress.Frame = new CGRect (
                accountEmailAddress.Frame.Left,
                accountEmailAddress.Frame.Top,
                unreadCountLabel.Frame.Left - accountEmailAddress.Frame.Left - UNREAD_COUNT_MARGIN_LEFT,
                accountEmailAddress.Frame.Height
            );
        }

        void HighlightError ()
        {
            unreadCountLabel.Hidden = false;
            unreadCountLabel.Text = "!";
            unreadCountLabel.SizeToFit ();
            ViewFramer.Create (unreadCountLabel).Square ();
            unreadCountLabel.Layer.CornerRadius = unreadCountLabel.Frame.Height / 2;
            var unreadCountLabelX = this.Frame.Width - unreadCountLabel.Frame.Width - 30;
            ViewFramer.Create (unreadCountLabel).CenterY (0, this.Frame.Height).X (unreadCountLabelX);
        }

        public void Cleanup ()
        {
            accountImageView = null;
            userLabelView = null;
            nameLabel = null;
            accountEmailAddress = null;
            accountSettingsIndicatorArrow = null;
            unreadCountLabel = null;
        }

    }
}

