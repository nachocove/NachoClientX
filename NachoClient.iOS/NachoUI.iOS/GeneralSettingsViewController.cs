// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;
using System.Linq;

namespace NachoClient.iOS
{
    public partial class GeneralSettingsViewController : NcUIViewControllerNoLeaks
    {
        public static string PRIVACY_POLICY_KEY = "PRIVACY_POLICY";
        public static string LICENSE_AGREEMENT_KEY = "LICENSE_AGREEMENT";

        protected const float CELL_HEIGHT = 44f;
        protected const float TEXT_LINE_HEIGHT = 19.124f;

        protected float yOffset;

        protected const int NAME_LABEL_TAG = 100;
        protected const int EMAIL_ADDRESS_LABEL_TAG = 101;
        protected const int USER_IMAGE_VIEW_TAG = 102;
        protected const int USER_LABEL_VIEW_TAG = 103;
        protected const int FIX_BE_BUTTON_TAG = 104;
        protected const int ACCOUNT_SETTINGS_VIEW_TAG = 105;
        protected const int ABOUT_US_VIEW_TAG = 106;
        protected const int PRIVACY_POLICY_VIEW_TAG = 107;
        protected const int FIX_BE_LABEL_TAG = 108;

        protected UITapGestureRecognizer accountSettingsTapGesture;
        protected UITapGestureRecognizer.Token accountSettingsTapGestureHandlerToken;

        protected UIButton aboutUsButton;
        protected UIButton privacyPolicyButton;

        public GeneralSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            if (this.NavigationController.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                this.NavigationController.InteractivePopGestureRecognizer.Enabled = false;
            }
            NavigationItem.Title = "Settings";
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoNowBackground;
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            if (null != NavigationItem) {
                NavigationItem.SetHidesBackButton (true, false);
            }

            yOffset = A.Card_Vertical_Indent;

            UIView accountSettingsView = new UIView (new RectangleF (A.Card_Horizontal_Indent, yOffset, contentView.Frame.Width - (A.Card_Horizontal_Indent * 2), 80));
            accountSettingsView.BackgroundColor = UIColor.White;
            accountSettingsView.Layer.CornerRadius = A.Card_Corner_Radius;
            accountSettingsView.Layer.BorderColor = A.Card_Border_Color;
            accountSettingsView.Layer.BorderWidth = A.Card_Border_Width;
            accountSettingsView.Tag = ACCOUNT_SETTINGS_VIEW_TAG;
            accountSettingsTapGesture = new UITapGestureRecognizer ();
            accountSettingsTapGestureHandlerToken = accountSettingsTapGesture.AddTarget (AccountSettingsTapHandler);
            accountSettingsView.AddGestureRecognizer (accountSettingsTapGesture);

            var userImageView = new UIImageView (new RectangleF (12, 15, 50, 50));
            userImageView.Center = new PointF (userImageView.Center.X, accountSettingsView.Frame.Height / 2);
            userImageView.Layer.CornerRadius = 25;
            userImageView.Layer.MasksToBounds = true;
            userImageView.Hidden = true;
            userImageView.Tag = USER_IMAGE_VIEW_TAG;
            accountSettingsView.AddSubview (userImageView);

            var userLabelView = new UILabel (new RectangleF (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Hidden = true;
            userLabelView.Tag = USER_LABEL_VIEW_TAG;
            accountSettingsView.AddSubview (userLabelView);

            McAccount userAccount = McAccount.QueryById <McAccount> (LoginHelpers.GetCurrentAccountId ());
            McContact userContact = McContact.QueryByEmailAddress (LoginHelpers.GetCurrentAccountId (), userAccount.EmailAddr).FirstOrDefault ();

            var userImage = Util.ImageOfSender (LoginHelpers.GetCurrentAccountId (), userAccount.EmailAddr);

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

            UILabel nameLabel = new UILabel (new RectangleF (75, 20, 100, TEXT_LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            nameLabel.Tag = NAME_LABEL_TAG;
            accountSettingsView.AddSubview (nameLabel);

            UILabel accountEmailAddress = new UILabel (new RectangleF (75, nameLabel.Frame.Bottom, 170, TEXT_LINE_HEIGHT));
            accountEmailAddress.Tag = EMAIL_ADDRESS_LABEL_TAG;
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountSettingsView.AddSubview (accountEmailAddress);

            UIImageView accountSettingsIndicatorArrow;
            using (var disclosureIcon = UIImage.FromBundle ("gen-more-arrow")) {
                accountSettingsIndicatorArrow = new UIImageView (disclosureIcon);
            }
            accountSettingsIndicatorArrow.Frame = new RectangleF (accountEmailAddress.Frame.Right + 10, accountSettingsView.Frame.Height / 2 - accountSettingsIndicatorArrow.Frame.Height / 2, accountSettingsIndicatorArrow.Frame.Width, accountSettingsIndicatorArrow.Frame.Height);
            accountSettingsView.AddSubview (accountSettingsIndicatorArrow);
            contentView.AddSubview (accountSettingsView);

            yOffset = accountSettingsView.Frame.Bottom + 30;

            var buttonViewWidth = View.Frame.Width - (A.Card_Horizontal_Indent * 2);

            UIView buttonsView = new UIView (new RectangleF (A.Card_Horizontal_Indent, yOffset, buttonViewWidth, CELL_HEIGHT * 2));
            buttonsView.BackgroundColor = UIColor.White;
            buttonsView.Layer.CornerRadius = A.Card_Corner_Radius;
            buttonsView.Layer.BorderColor = A.Card_Border_Color;
            buttonsView.Layer.BorderWidth = A.Card_Border_Width;

            aboutUsButton = UIButton.FromType (UIButtonType.System);
            aboutUsButton.Frame = new RectangleF (A.Card_Horizontal_Indent, 0, buttonViewWidth - (2 * A.Card_Horizontal_Indent), CELL_HEIGHT);
            aboutUsButton.SetTitle ("About Us", UIControlState.Normal);
            aboutUsButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            aboutUsButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            aboutUsButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            aboutUsButton.TouchUpInside += AboutUsTapHandler;

            buttonsView.AddSubview (aboutUsButton);

            Util.AddHorizontalLine (0, CELL_HEIGHT, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);

            privacyPolicyButton = UIButton.FromType (UIButtonType.System);
            privacyPolicyButton.Frame = new RectangleF (A.Card_Horizontal_Indent, CELL_HEIGHT + 1, buttonViewWidth - (2 * A.Card_Horizontal_Indent), CELL_HEIGHT);
            privacyPolicyButton.SetTitle ("Privacy Policy", UIControlState.Normal);
            privacyPolicyButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            privacyPolicyButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            privacyPolicyButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            privacyPolicyButton.TouchUpInside += PrivacyPolicyTapHandler;

            buttonsView.AddSubview (privacyPolicyButton);

            contentView.AddSubview (buttonsView);

            yOffset = buttonsView.Frame.Bottom + 30f;

            UILabel dirtyBackEndLabel = new UILabel (new RectangleF (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT));
            dirtyBackEndLabel.Text = "There is an issue with your account that is preventing you from sending or receiving messages.";
            dirtyBackEndLabel.Font = A.Font_AvenirNextRegular12;
            dirtyBackEndLabel.TextAlignment = UITextAlignment.Center;
            dirtyBackEndLabel.BackgroundColor = UIColor.Clear;
            dirtyBackEndLabel.TextColor = A.Color_NachoGreen;
            dirtyBackEndLabel.Lines = 2;
            dirtyBackEndLabel.LineBreakMode = UILineBreakMode.WordWrap;
            dirtyBackEndLabel.Tag = FIX_BE_LABEL_TAG;
            dirtyBackEndLabel.Hidden = true;
            contentView.AddSubview (dirtyBackEndLabel);

            yOffset = dirtyBackEndLabel.Frame.Bottom + 5;

            UIButton DirtyBackEnd = new UIButton (new RectangleF (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT));
            DirtyBackEnd.Layer.CornerRadius = 4.0f;
            DirtyBackEnd.BackgroundColor = A.Color_NachoRed;
            DirtyBackEnd.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            DirtyBackEnd.SetTitle ("Fix Account", UIControlState.Normal);
            DirtyBackEnd.SetTitleColor (UIColor.White, UIControlState.Normal);
            DirtyBackEnd.TouchUpInside += FixBackEndButtonClicked; 
            DirtyBackEnd.Tag = FIX_BE_BUTTON_TAG;
            DirtyBackEnd.Hidden = true;
            contentView.AddSubview (DirtyBackEnd);

            yOffset = DirtyBackEnd.Frame.Bottom + 5;

            UILabel versionLabel = new UILabel (new RectangleF (View.Frame.Width / 2 - 75, yOffset, 150, 20));
            versionLabel.Font = A.Font_AvenirNextRegular10;
            versionLabel.TextColor = A.Color_NachoBlack;
            versionLabel.TextAlignment = UITextAlignment.Center;
            versionLabel.Text = "NachoMail version " + Util.GetVersionNumber ();//"NachoMail version 0.9";
            contentView.AddSubview (versionLabel);

            yOffset = versionLabel.Frame.Bottom + 5;

//            // Test sending events
//            var testEmailNotificationButton = new UIButton (UIButtonType.RoundedRect);
//            testEmailNotificationButton.SetTitle ("Test local email notification", UIControlState.Normal);
//            testEmailNotificationButton.BackgroundColor = UIColor.Red;
//            testEmailNotificationButton.Frame = new RectangleF (33, yOffset + 12, 284, 30);
//            contentView.AddSubview (testEmailNotificationButton);
//            testEmailNotificationButton.TouchUpInside += (object sender, EventArgs e) => {
//                AppDelegate.TestScheduleEmailNotification ();
//            };
//            yOffset = testEmailNotificationButton.Frame.Bottom;
//
//            var testCalendarNotificationButton = new UIButton (UIButtonType.RoundedRect);
//            testCalendarNotificationButton.SetTitle ("Test local event notification", UIControlState.Normal);
//            testCalendarNotificationButton.BackgroundColor = UIColor.Red;
//            testCalendarNotificationButton.Frame = new RectangleF (33, yOffset + 12, 284, 30);
//            contentView.AddSubview (testCalendarNotificationButton);
//            testCalendarNotificationButton.TouchUpInside += (object sender, EventArgs e) => {
//                AppDelegate.TestScheduleCalendarNotification ();
//            };
//            yOffset = testCalendarNotificationButton.Frame.Bottom;
        }

        protected override void ConfigureAndLayout ()
        {
            var nameLabel = (UILabel)contentView.ViewWithTag (NAME_LABEL_TAG);

            McAccount userAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McContact userContact = McContact.QueryByEmailAddress (LoginHelpers.GetCurrentAccountId (), userAccount.EmailAddr).FirstOrDefault ();
            nameLabel.Text = userContact.FileAs;

            var emailLabel = (UILabel)contentView.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);
            emailLabel.Text = GetEmailAddress ();

            UIButton FixButton = (UIButton)View.ViewWithTag (FIX_BE_BUTTON_TAG);
            FixButton.Hidden = !LoginHelpers.DoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId ());

            UILabel FixLabel = (UILabel)View.ViewWithTag (FIX_BE_LABEL_TAG);
            FixLabel.Hidden = !LoginHelpers.DoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId ());

            LayoutView ();
        }

        protected override void Cleanup ()
        {
            UIButton FixButton = (UIButton)View.ViewWithTag (FIX_BE_BUTTON_TAG);
            FixButton.TouchUpInside -= FixBackEndButtonClicked;
            FixButton = null;

            accountSettingsTapGesture.RemoveTarget (accountSettingsTapGestureHandlerToken);
            var accountSettingsView = (UIView)View.ViewWithTag (ACCOUNT_SETTINGS_VIEW_TAG);
            if (null != accountSettingsView) {
                accountSettingsView.RemoveGestureRecognizer (accountSettingsTapGesture);
            }

            aboutUsButton.TouchUpInside -= AboutUsTapHandler;
            aboutUsButton = null;

            privacyPolicyButton.TouchUpInside -= PrivacyPolicyTapHandler;
            privacyPolicyButton = null;
        }

        protected void AccountSettingsTapHandler (NSObject sender)
        {
            PerformSegue ("SegueToAccountSettings", this);
            View.EndEditing (true);
        }

        protected void PrivacyPolicyTapHandler (object sender, EventArgs e)
        {
            PerformSegue ("GeneralSettingsToSettingsLegal", this);
            View.EndEditing (true);
        }

        protected void AboutUsTapHandler (object sender, EventArgs e)
        {
            PerformSegue ("SegueToAboutUs", this);
            View.EndEditing (true);
        }

        protected void FixBackEndButtonClicked (object sender, EventArgs e)
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {

                BackEndAutoDStateEnum backEndState = BackEnd.Instance.AutoDState (LoginHelpers.GetCurrentAccountId ());

                if (BackEndAutoDStateEnum.CredWait == backEndState || BackEndAutoDStateEnum.CertAskWait == backEndState) {
                    UIStoryboard x = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                    CredentialsAskViewController cvc = (CredentialsAskViewController)x.InstantiateViewController ("CredentialsAskViewController");
                    cvc.SetTabBarController ((NachoTabBarController)this.TabBarController);
                    this.PresentViewController (cvc, true, null);
                }

                if (BackEndAutoDStateEnum.ServerConfWait == backEndState) {
                    var x = (AppDelegate)UIApplication.SharedApplication.Delegate;
                    x.ServConfReqCallback (LoginHelpers.GetCurrentAccountId ());
                }
            }
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
            var contentFrame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
            ViewHelper.DumpViewHierarchy (View);
        }

        protected string GetEmailAddress ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                McAccount Account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                return Account.EmailAddr;
            } else {
                return "";
            }
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("GeneralSettingsToSettingsLegal")) {
                var x = segue.DestinationViewController;
                var settingsLegal = (SettingsLegalViewController)segue.DestinationViewController.ChildViewControllers [0];
                settingsLegal.SetProperties ("https://nachocove.com/privacy-policy-text/", "Privacy Policy", PRIVACY_POLICY_KEY, true);
                return;
            }
            if (segue.Identifier.Equals ("SegueToAccountSettings")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToAboutUs")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToNachoNow")) {
                return;
            }
                
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }
    }
}
