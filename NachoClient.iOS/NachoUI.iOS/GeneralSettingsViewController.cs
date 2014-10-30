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
        protected static float CELL_HEIGHT = 44f;
        protected static float INSET = 15f;
        protected static float TEXT_LINE_HEIGHT = 19.124f;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;

        protected float yOffset;

        protected const int NAME_LABEL_TAG = 100;
        protected const int EMAIL_ADDRESS_LABEL_TAG = 101;
        protected const int USER_IMAGE_VIEW_TAG = 102;
        protected const int USER_LABEL_VIEW_TAG = 103;
        protected const int FIX_BE_BUTTON_TAG = 104;
        protected const int ACCOUNT_SETTINGS_CELL_TAG = 105;
        protected const int ABOUT_US_CELL_TAG = 106;
        protected const int PRIVACY_POLICY_CELL_TAG = 107;

        protected UITapGestureRecognizer accountSettingsTapGesture;
        protected UITapGestureRecognizer.Token accountSettingsTapGestureHandlerToken;

        protected UITapGestureRecognizer aboutUsTapGesture;
        protected UITapGestureRecognizer.Token aboutUsTapGestureHandlerToken;

        protected UITapGestureRecognizer privacyPolicyTapGesture;
        protected UITapGestureRecognizer.Token privacyPolicyTapGestureHandlerToken;

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
            NavigationController.NavigationBar.Translucent = false;
            NavigationController.NavigationBar.TintColor = A.Color_NachoBlue;

            UIButton DirtyBackEnd = new UIButton (new RectangleF (View.Frame.Width / 2 - 40, View.Frame.Bottom - 100, 80, 30));
            DirtyBackEnd.Layer.CornerRadius = 2.0f;
            DirtyBackEnd.BackgroundColor = A.Color_NachoRed;
            DirtyBackEnd.Font = A.Font_AvenirNextRegular14;
            DirtyBackEnd.SetTitle ("Fix Account", UIControlState.Normal);
            DirtyBackEnd.SetTitleColor (UIColor.White, UIControlState.Normal);
            DirtyBackEnd.TouchUpInside += FixBackEndButtonClicked; 
            DirtyBackEnd.Tag = FIX_BE_BUTTON_TAG;
            DirtyBackEnd.Hidden = true;
            View.Add(DirtyBackEnd);

            yOffset = INSET;

            UIView accountSettingsCell = new UIView (new RectangleF (INSET, yOffset, contentView.Frame.Width - (INSET * 2), 80));
            accountSettingsCell.BackgroundColor = UIColor.White;
            accountSettingsCell.Layer.CornerRadius = 4f;
            accountSettingsCell.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            accountSettingsCell.Layer.BorderWidth = .5f;
            accountSettingsCell.Tag = ACCOUNT_SETTINGS_CELL_TAG;
            accountSettingsTapGesture = new UITapGestureRecognizer ();
            accountSettingsTapGestureHandlerToken = accountSettingsTapGesture.AddTarget (AccountSettingsTapHandler);
            accountSettingsCell.AddGestureRecognizer (accountSettingsTapGesture);

            var userImageView = new UIImageView (new RectangleF (12, 15, 50, 50));
            userImageView.Center = new PointF (userImageView.Center.X, accountSettingsCell.Frame.Height / 2);
            userImageView.Layer.CornerRadius = 25;
            userImageView.Hidden = true;
            userImageView.Tag = USER_IMAGE_VIEW_TAG;
            accountSettingsCell.AddSubview (userImageView);

            var userLabelView = new UILabel (new RectangleF (12, 15, 50, 50));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 25;
            userLabelView.Layer.MasksToBounds = true;
            userLabelView.Hidden = true;
            userLabelView.Tag = USER_LABEL_VIEW_TAG;
            accountSettingsCell.AddSubview (userLabelView);

            McAccount userAccount = McAccount.QueryById <McAccount>(LoginHelpers.GetCurrentAccountId ());
            McContact userContact = McContact.QueryByAccountId <McContact> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault();

            var userImage = Util.ImageOfSender (LoginHelpers.GetCurrentAccountId(), userAccount.EmailAddr);

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                int ColorIndex;
                string Initials;
                Util.UserMessageField (userContact.DisplayName, LoginHelpers.GetCurrentAccountId(), out ColorIndex, out Initials);
                userLabelView.Text = Initials;
                userLabelView.BackgroundColor = Util.ColorForUser (ColorIndex);
            }

            UILabel nameLabel = new UILabel (new RectangleF (75, 20, 100, TEXT_LINE_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextDemiBold14;
            nameLabel.TextColor = A.Color_NachoBlack;
            nameLabel.Tag = NAME_LABEL_TAG;
            accountSettingsCell.AddSubview (nameLabel);

            UILabel accountEmailAddress = new UILabel (new RectangleF (75, nameLabel.Frame.Bottom , 170, TEXT_LINE_HEIGHT));
            accountEmailAddress.Tag = EMAIL_ADDRESS_LABEL_TAG;
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountSettingsCell.AddSubview (accountEmailAddress);

            UIImageView accountSettingsIndicatorArrow;
            using (var disclosureIcon = UIImage.FromBundle ("gen-more-arrow")) {
                accountSettingsIndicatorArrow = new UIImageView (disclosureIcon);
            }
            accountSettingsIndicatorArrow.Frame = new RectangleF (accountEmailAddress.Frame.Right + 10, accountSettingsCell.Frame.Height / 2 - accountSettingsIndicatorArrow.Frame.Height / 2, accountSettingsIndicatorArrow.Frame.Width, accountSettingsIndicatorArrow.Frame.Height);
            accountSettingsCell.AddSubview (accountSettingsIndicatorArrow);
            contentView.AddSubview (accountSettingsCell);

            yOffset = accountSettingsCell.Frame.Bottom + 30;

            UIView buttonsCell = new UIView (new RectangleF(INSET, yOffset, View.Frame.Width - (INSET * 2), CELL_HEIGHT * 2));
            buttonsCell.BackgroundColor = UIColor.White;
            buttonsCell.Layer.CornerRadius = 4f;
            buttonsCell.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            buttonsCell.Layer.BorderWidth = .5f;

            UILabel aboutUsLabel = new UILabel (new RectangleF (INSET, 12, 200, 20));
            aboutUsLabel.Font = A.Font_AvenirNextDemiBold14;
            aboutUsLabel.TextColor = A.Color_NachoGreen;
            aboutUsLabel.Text = "About Us";
            buttonsCell.AddSubview (aboutUsLabel);

            UIView aboutUsCell = new UIView (new RectangleF (0, 0, buttonsCell.Frame.Width, CELL_HEIGHT));
            aboutUsCell.BackgroundColor = UIColor.Clear;
            aboutUsCell.UserInteractionEnabled = true;
            aboutUsCell.Tag = ABOUT_US_CELL_TAG;
            aboutUsTapGesture = new UITapGestureRecognizer ();
            aboutUsTapGestureHandlerToken = aboutUsTapGesture.AddTarget (AboutUsTapHandler);
            aboutUsCell.AddGestureRecognizer (aboutUsTapGesture);
            buttonsCell.AddSubview (aboutUsCell);

            Util.AddHorizontalLine (0, CELL_HEIGHT, buttonsCell.Frame.Width, A.Color_NachoBorderGray, buttonsCell);

            UILabel privacyPolicyLabel = new UILabel (new RectangleF (INSET, CELL_HEIGHT + 11, 200, 20));
            privacyPolicyLabel.Font = A.Font_AvenirNextDemiBold14;
            privacyPolicyLabel.TextColor = A.Color_NachoGreen;
            privacyPolicyLabel.Text = "Privacy Policy";
            buttonsCell.AddSubview (privacyPolicyLabel);

            UIView privacyPolicyCell = new UIView (new RectangleF (0, CELL_HEIGHT, buttonsCell.Frame.Width, CELL_HEIGHT));
            privacyPolicyCell.BackgroundColor = UIColor.Clear;
            privacyPolicyCell.UserInteractionEnabled = true;
            privacyPolicyCell.Tag = PRIVACY_POLICY_CELL_TAG;

            privacyPolicyTapGesture = new UITapGestureRecognizer ();
            privacyPolicyTapGestureHandlerToken = privacyPolicyTapGesture.AddTarget (PrivacyPolicyTapHandler);
            privacyPolicyCell.AddGestureRecognizer (privacyPolicyTapGesture);
            buttonsCell.AddSubview (privacyPolicyCell);

            View.AddSubview (buttonsCell);
        }

        protected override void ConfigureAndLayout ()
        {
            var nameLabel = (UILabel)contentView.ViewWithTag (NAME_LABEL_TAG);

            McAccount userAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McContact userContact = McContact.QueryByEmailAddress (LoginHelpers.GetCurrentAccountId (), userAccount.EmailAddr).FirstOrDefault();
            nameLabel.Text = userContact.FileAs;

            var emailLabel = (UILabel)contentView.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);
            emailLabel.Text = GetEmailAddress ();

            UIButton FixButton = (UIButton)View.ViewWithTag (FIX_BE_BUTTON_TAG);
            FixButton.Hidden = !LoginHelpers.DoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId ());

            LayoutView ();
        }

        protected override void Cleanup ()
        {
            UIButton FixButton = (UIButton)View.ViewWithTag (FIX_BE_BUTTON_TAG);
            FixButton.TouchUpInside -= FixBackEndButtonClicked;
            FixButton = null;

            accountSettingsTapGesture.RemoveTarget (accountSettingsTapGestureHandlerToken);
            var accountSettings = (UIView)View.ViewWithTag (ACCOUNT_SETTINGS_CELL_TAG);
            if (null != accountSettings){
                accountSettings.RemoveGestureRecognizer (accountSettingsTapGesture);
            }

            aboutUsTapGesture.RemoveTarget (aboutUsTapGestureHandlerToken);
            var aboutUs = (UIView)View.ViewWithTag (ABOUT_US_CELL_TAG);
            if (null != aboutUs){
                aboutUs.RemoveGestureRecognizer (aboutUsTapGesture);
            }

            privacyPolicyTapGesture.RemoveTarget (privacyPolicyTapGestureHandlerToken);
            var privacyPolicy = (UIView)View.ViewWithTag (PRIVACY_POLICY_CELL_TAG);
            if (null != privacyPolicy){
                privacyPolicy.RemoveGestureRecognizer (privacyPolicyTapGesture);
            }
        }

        protected void AccountSettingsTapHandler (NSObject sender)
        {
            PerformSegue ("SegueToAccountSettings", this);
            View.EndEditing (true);
        }

        protected void PrivacyPolicyTapHandler (NSObject sender)
        {
            PerformSegue ("GeneralSettingsToSettingsLegal", this);
            View.EndEditing (true);
        }

        protected void AboutUsTapHandler (NSObject sender)
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
                    cvc.SetTabBarController((NachoTabBarController)this.TabBarController);
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

        protected UIView AddHorizontalLine (float offset, float yVal, float width, UIColor color)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            return lineUIView;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("GeneralSettingsToSettingsLegal")) {
                var x = segue.DestinationViewController;
                var settingsLegal = (SettingsLegalViewController)segue.DestinationViewController.ChildViewControllers[0];
                settingsLegal.SetProperties ("https://nachocove.com/privacy-policy-text/", "Privacy Policy", true);
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
