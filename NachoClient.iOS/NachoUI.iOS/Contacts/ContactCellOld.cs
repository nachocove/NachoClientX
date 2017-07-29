//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Foundation;
using CoreGraphics;
using UIKit;

using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public static class ContactCellOld
    {

        private const float HORIZONTAL_INDENT = 65;

        private const int CALL_SWIPE_TAG = 100;
        private const int EMAIL_SWIPE_TAG = 101;
        private const int SWIPE_VIEW_TAG = 102;

        private const int TITLE_LABEL_TAG = 333;
        private const int USER_LABEL_TAG = 334;
        private const int SUBTITLE1_LABEL_TAG = 335;
        private const int SUBTITLE2_LABEL_TAG = 336;
        private const int SET_VIP_TAG = 337;
        private const int USER_PORTRAIT_TAG = 338;

        public const float ROW_HEIGHT = 80;

        private static SwipeActionDescriptor CALL_BUTTON =
            new SwipeActionDescriptor (CALL_SWIPE_TAG, 0.5f, UIImage.FromBundle ("contacts-call-swipe"),
                "Dial (verb)", A.Color_NachoSwipeActionOrange);
        private static SwipeActionDescriptor EMAIL_BUTTON =
            new SwipeActionDescriptor (EMAIL_SWIPE_TAG, 0.5f, UIImage.FromBundle ("contacts-email-swipe"),
                "Email (verb)", A.Color_NachoSwipeActionMatteBlack);

        private const string ContactCellReuseIdentifier = "ContactCell";

        public static UITableViewCell CreateCell (UITableView tableView, EventHandler e)
        {
            var cell = new UITableViewCell (UITableViewCellStyle.Subtitle, ContactCellReuseIdentifier);

            cell.Layer.CornerRadius = 15;
            cell.Layer.MasksToBounds = true;
            cell.SelectionStyle = UITableViewCellSelectionStyle.Default;

            var view = new SwipeActionView (new CGRect (0, 0, tableView.Frame.Width, ROW_HEIGHT));
            view.BackgroundColor = UIColor.White;
            view.SetAction (CALL_BUTTON, SwipeSide.LEFT);
            view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);
            view.Tag = SWIPE_VIEW_TAG;

            cell.ContentView.AddSubview (view);

            UIButton toggleVipButton = new UIButton (new CGRect (tableView.Frame.Right - 30, 10, 20, 20));
            toggleVipButton.AccessibilityLabel = "VIP";
            toggleVipButton.Tag = SET_VIP_TAG;
            toggleVipButton.TouchUpInside += e;
            view.AddSubview (toggleVipButton);

            var titleLabel = new UILabel (new CGRect (HORIZONTAL_INDENT, 10, tableView.Frame.Width - 15 - HORIZONTAL_INDENT - toggleVipButton.Frame.Width - 8, 20));
            titleLabel.TextColor = A.Color_NachoGreen;
            titleLabel.Font = A.Font_AvenirNextDemiBold17;
            titleLabel.LineBreakMode = UILineBreakMode.TailTruncation;
            titleLabel.Tag = TITLE_LABEL_TAG;
            view.AddSubview (titleLabel);

            var subtitle1Label = new UILabel (new CGRect (HORIZONTAL_INDENT, 35, titleLabel.Frame.Width, 20));
            subtitle1Label.LineBreakMode = UILineBreakMode.TailTruncation;
            subtitle1Label.Font = A.Font_AvenirNextRegular14;
            subtitle1Label.Tag = SUBTITLE1_LABEL_TAG;
            view.AddSubview (subtitle1Label);

            var subtitle2Label = new UILabel (new CGRect (HORIZONTAL_INDENT, 55, titleLabel.Frame.Width, 20));
            subtitle2Label.LineBreakMode = UILineBreakMode.TailTruncation;
            subtitle2Label.Font = A.Font_AvenirNextRegular14;
            subtitle2Label.Tag = SUBTITLE2_LABEL_TAG;
            view.AddSubview (subtitle2Label);

            // User userLabelView view, if no image
            var userLabelView = new UILabel (new CGRect (15, 10, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Tag = USER_LABEL_TAG;
            view.AddSubview (userLabelView);

            var userImageView = new UIImageView (new CGRect (15, 10, 40, 40));
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Tag = USER_PORTRAIT_TAG;
            view.AddSubview (userImageView);

            return cell;
        }

        public static void ConfigureCell (UITableView tableView, UITableViewCell cell, McContact contact, bool allowSwiping, string alternateEmailAddress = null)
        {
            var titleLabel = (UILabel)cell.ViewWithTag (TITLE_LABEL_TAG);
            var subtitle1Label = (UILabel)cell.ViewWithTag (SUBTITLE1_LABEL_TAG);
            var subtitle2Label = (UILabel)cell.ViewWithTag (SUBTITLE2_LABEL_TAG);
            var labelView = (UILabel)cell.ViewWithTag (USER_LABEL_TAG);
            var portraitView = (UIImageView)cell.ViewWithTag (USER_PORTRAIT_TAG);

            labelView.Hidden = true;
            portraitView.Hidden = true;

            var view = (SwipeActionView)cell.ViewWithTag (SWIPE_VIEW_TAG);
            view.EnableSwipe (null != contact && allowSwiping);

            if (view.IsSwipeEnabled ()) {
                view.ClearActions (SwipeSide.LEFT);
                view.ClearActions (SwipeSide.RIGHT);

                if (contact.CanUserEdit () || 0 < contact.PhoneNumbers.Count) {
                    view.SetAction (CALL_BUTTON, SwipeSide.LEFT);
                }
                if (contact.CanUserEdit () || 0 < contact.EmailAddresses.Count) {
                    view.SetAction (EMAIL_BUTTON, SwipeSide.RIGHT);
                }
            }

            if (null == contact) {
                titleLabel.Text = NSBundle.MainBundle.LocalizedString ("This contact is unavailable", "Fallback text for deleted contact");
                titleLabel.TextColor = UIColor.LightGray;
                titleLabel.Font = A.Font_AvenirNextRegular14;
                subtitle1Label.Text = "";
                subtitle2Label.Text = "";
                view.OnSwipe = null;
                view.OnClick = null;
                return;
            }

            var displayTitle = contact.GetDisplayName ();
            var displayTitleColor = A.Color_NachoDarkText;

            var displaySubtitle1 = (null == alternateEmailAddress ? contact.GetPrimaryCanonicalEmailAddress () : alternateEmailAddress);
            var displaySubtitle1Color = A.Color_NachoDarkText;

            var displaySubtitle2 = contact.GetPrimaryPhoneNumber ();
            var displaySubtitle2Color = A.Color_NachoDarkText;

            var contactColor = Util.GetContactColor (contact);

            if (String.IsNullOrEmpty (displayTitle) && !String.IsNullOrEmpty (displaySubtitle1)) {
                displayTitle = displaySubtitle1;
                displaySubtitle1 = NSBundle.MainBundle.LocalizedString ("No name for this contact", "Fallback text for unnamed contact");
                displaySubtitle1Color = A.Color_NachoTextGray;
            }

            if (String.IsNullOrEmpty (displayTitle)) {
                displayTitle = NSBundle.MainBundle.LocalizedString ("No name for this contact", "Fallback text for unnamed contact");
                displayTitleColor = A.Color_NachoLightText;
            }

            if (String.IsNullOrEmpty (displaySubtitle1)) {
                displaySubtitle1 = NSBundle.MainBundle.LocalizedString ("No email address for this contact", "Text for contact without email address");
                displaySubtitle1Color = A.Color_NachoLightText;
            }

            if (String.IsNullOrEmpty (displaySubtitle2)) {
                displaySubtitle2 = NSBundle.MainBundle.LocalizedString ("No phone number for this contact", "Text for contact without phone number");
                displaySubtitle2Color = A.Color_NachoLightText;
            }

            titleLabel.Text = displayTitle;
            titleLabel.TextColor = displayTitleColor;

            subtitle1Label.Text = displaySubtitle1;
            subtitle1Label.TextColor = displaySubtitle1Color;

            subtitle2Label.Text = displaySubtitle2;
            subtitle2Label.TextColor = displaySubtitle2Color;

            if (0 == contact.PortraitId) {
                ConfigureLabelView (labelView, contact, contactColor);
                labelView.Hidden = false;
            } else {
                portraitView.Image = Util.ContactToPortraitImage (contact);
                portraitView.Hidden = false;
            }

            view.OnSwipe = (SwipeActionView activeView, SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    cell.Layer.CornerRadius = 0;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    cell.Layer.CornerRadius = 15;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            var toggleVipButton = (UIButton)view.ViewWithTag (SET_VIP_TAG);
            using (var image = UIImage.FromBundle (contact.IsVip ? "contacts-vip-checked" : "contacts-vip")) {
                toggleVipButton.SetImage (image, UIControlState.Normal);
            }
        }

        private static void ConfigureLabelView (UILabel labelView, McContact contact, UIColor contactColor)
        {
            labelView.Hidden = false;
            labelView.Text = NachoCore.Utils.ContactsHelper.GetInitials (contact);
            labelView.BackgroundColor = contactColor;
        }
    }
}

