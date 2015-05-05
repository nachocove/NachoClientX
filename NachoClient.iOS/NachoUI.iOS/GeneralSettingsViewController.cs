// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using UIKit;
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

        protected static readonly nfloat CELL_HEIGHT = 44f;

        protected nfloat yOffset;

        protected const int ACCOUNT_INFO_VIEW_TAG = 105;

        protected UIView buttonsView;
        protected UILabel versionLabel;
        protected UIButton aboutUsButton;
        protected UIButton releaseNotesButton;
        protected UIButton privacyPolicyButton;
        protected UILabel passwordExpiryLabel;
        protected UIButton dirtyBackEndButton;
        protected UILabel dirtyBackEndLabel;

        public GeneralSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            NavigationItem.Title = "Settings";
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (this.NavigationController.RespondsToSelector (new ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                this.NavigationController.InteractivePopGestureRecognizer.Enabled = true;
                this.NavigationController.InteractivePopGestureRecognizer.Delegate = null;
            }

        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            View.BackgroundColor = A.Color_NachoBackgroundGray;
            contentView.BackgroundColor = A.Color_NachoBackgroundGray;

            // Uncomment to hide <More
            // if (null != NavigationItem) {
            //     NavigationItem.SetHidesBackButton (true, false);
            // }
            Util.ConfigureNavBar (false, this.NavigationController);

            yOffset = A.Card_Vertical_Indent;

            var accountSettingsView = new AccountInfoView (new CGRect (A.Card_Horizontal_Indent, yOffset, contentView.Frame.Width - (A.Card_Horizontal_Indent * 2), 80));
            accountSettingsView.OnAccountSelected = AccountSettingsTapHandler;
            accountSettingsView.Tag = ACCOUNT_INFO_VIEW_TAG;
            contentView.AddSubview (accountSettingsView);

            yOffset = accountSettingsView.Frame.Bottom + 30;

            var buttonViewWidth = View.Frame.Width - (A.Card_Horizontal_Indent * 2);

            buttonsView = new UIView (new CGRect (A.Card_Horizontal_Indent, yOffset, buttonViewWidth, (CELL_HEIGHT * 3) + 2));
            buttonsView.BackgroundColor = UIColor.White;
            buttonsView.Layer.CornerRadius = A.Card_Corner_Radius;
            buttonsView.Layer.BorderColor = A.Card_Border_Color;
            buttonsView.Layer.BorderWidth = A.Card_Border_Width;

            nfloat buttonY = 0;

            aboutUsButton = UIButton.FromType (UIButtonType.System);
            aboutUsButton.Frame = new CGRect (A.Card_Horizontal_Indent, buttonY, buttonViewWidth - (2 * A.Card_Horizontal_Indent), CELL_HEIGHT);
            aboutUsButton.SetTitle ("About Us", UIControlState.Normal);
            aboutUsButton.AccessibilityLabel = "About Us";
            aboutUsButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            aboutUsButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            aboutUsButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            aboutUsButton.TouchUpInside += AboutUsTapHandler;
            buttonsView.AddSubview (aboutUsButton);
            buttonY += CELL_HEIGHT;

            Util.AddHorizontalLine (0, buttonY, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);
            buttonY += 1;

            releaseNotesButton = UIButton.FromType (UIButtonType.System);
            releaseNotesButton.Frame = new CGRect (A.Card_Horizontal_Indent, buttonY, buttonViewWidth - (2 * A.Card_Horizontal_Indent), CELL_HEIGHT);
            releaseNotesButton.SetTitle ("Release Notes", UIControlState.Normal);
            releaseNotesButton.AccessibilityLabel = "Release Notes";
            releaseNotesButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            releaseNotesButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            releaseNotesButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            releaseNotesButton.TouchUpInside += ReleaseNotesTapHandler;
            buttonsView.AddSubview (releaseNotesButton);
            buttonY += CELL_HEIGHT;

            Util.AddHorizontalLine (0, buttonY, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);
            buttonY += 1;

            privacyPolicyButton = UIButton.FromType (UIButtonType.System);
            privacyPolicyButton.Frame = new CGRect (A.Card_Horizontal_Indent, buttonY, buttonViewWidth - (2 * A.Card_Horizontal_Indent), CELL_HEIGHT);
            privacyPolicyButton.SetTitle ("Privacy Policy", UIControlState.Normal);
            privacyPolicyButton.AccessibilityLabel = "Privacy Policy";
            privacyPolicyButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            privacyPolicyButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            privacyPolicyButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            privacyPolicyButton.TouchUpInside += PrivacyPolicyTapHandler;
            buttonsView.AddSubview (privacyPolicyButton);
            buttonY += CELL_HEIGHT;

            contentView.AddSubview (buttonsView);

            yOffset = buttonsView.Frame.Bottom + 30f;

            passwordExpiryLabel = new UILabel (new CGRect (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT));

            passwordExpiryLabel.Font = A.Font_AvenirNextRegular12;
            passwordExpiryLabel.TextAlignment = UITextAlignment.Left;
            passwordExpiryLabel.BackgroundColor = UIColor.Clear;
            passwordExpiryLabel.TextColor = A.Color_NachoGreen;
            passwordExpiryLabel.Lines = 0;
            passwordExpiryLabel.LineBreakMode = UILineBreakMode.WordWrap;
            passwordExpiryLabel.Hidden = true;
            contentView.AddSubview (passwordExpiryLabel);

            yOffset = buttonsView.Frame.Bottom + 30f;

            dirtyBackEndLabel = new UILabel (new CGRect (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT));
            dirtyBackEndLabel.Text = "There is an issue with your account that is preventing you from sending or receiving messages.";
            dirtyBackEndLabel.Font = A.Font_AvenirNextRegular12;
            dirtyBackEndLabel.TextAlignment = UITextAlignment.Center;
            dirtyBackEndLabel.BackgroundColor = UIColor.Clear;
            dirtyBackEndLabel.TextColor = A.Color_NachoGreen;
            dirtyBackEndLabel.Lines = 2;
            dirtyBackEndLabel.LineBreakMode = UILineBreakMode.WordWrap;
            dirtyBackEndLabel.Hidden = true;
            contentView.AddSubview (dirtyBackEndLabel);

            yOffset = dirtyBackEndLabel.Frame.Bottom + 5;

            dirtyBackEndButton = new UIButton (new CGRect (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT));
            dirtyBackEndButton.Layer.CornerRadius = 4.0f;
            dirtyBackEndButton.BackgroundColor = A.Color_NachoRed;
            dirtyBackEndButton.TitleLabel.Font = A.Font_AvenirNextDemiBold14;
            dirtyBackEndButton.SetTitle ("Fix Account", UIControlState.Normal);
            dirtyBackEndButton.AccessibilityLabel = "Fix account";
            dirtyBackEndButton.SetTitleColor (UIColor.White, UIControlState.Normal);
            dirtyBackEndButton.TouchUpInside += FixBackEndButtonClicked; 
            dirtyBackEndButton.Hidden = true;
            contentView.AddSubview (dirtyBackEndButton);

            yOffset = dirtyBackEndButton.Frame.Bottom + 5;

            versionLabel = new UILabel (new CGRect (0, View.Frame.Height - 44, View.Frame.Width, 20));
            versionLabel.Font = A.Font_AvenirNextRegular10;
            versionLabel.TextColor = A.Color_NachoBlack;
            versionLabel.TextAlignment = UITextAlignment.Center;
            versionLabel.Text = "Nacho Mail version " + Util.GetVersionNumber ();//"Nacho Mail version 0.9";
            contentView.AddSubview (versionLabel);

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
//
//            var testUserNotificationButton = new UIButton (UIButtonType.RoundedRect);
//            testUserNotificationButton.SetTitle ("Test user error notification", UIControlState.Normal);
//            testUserNotificationButton.BackgroundColor = UIColor.Red;
//            testUserNotificationButton.Frame = new CGRect (33, yOffset + 12, 284, 30);
//            contentView.AddSubview (testUserNotificationButton);
//            testUserNotificationButton.TouchUpInside += (object sender, EventArgs e) => {
//                var flip = !LoginHelpers.DoesBackEndHaveIssues (NcApplication.Instance.Account.Id);
//                LoginHelpers.SetDoesBackEndHaveIssues (NcApplication.Instance.Account.Id, flip);
//            };
//            yOffset = testUserNotificationButton.Frame.Bottom;
        }

        protected override void ConfigureAndLayout ()
        {
            McAccount userAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            var accountInfoView = (AccountInfoView)contentView.ViewWithTag (ACCOUNT_INFO_VIEW_TAG);
            accountInfoView.Configure (userAccount);

            yOffset = buttonsView.Frame.Bottom + 30;

            DateTime expiry;
            string rectificationUrl;
            if (LoginHelpers.PasswordWillExpire (LoginHelpers.GetCurrentAccountId (), out expiry, out rectificationUrl)) {
                passwordExpiryLabel.Hidden = false;
                passwordExpiryLabel.Text = String.Format ("Password will expire on {0}.\n{1}", Pretty.ReminderDate (expiry), rectificationUrl ?? "");
                passwordExpiryLabel.SizeToFit ();
                ViewFramer.Create (passwordExpiryLabel).Y (yOffset);
                yOffset += passwordExpiryLabel.Frame.Height + 10;
            } else {
                passwordExpiryLabel.Hidden = true;
            }

            if(LoginHelpers.DoesBackEndHaveIssues (LoginHelpers.GetCurrentAccountId ())) {
                dirtyBackEndLabel.Hidden = false;
                dirtyBackEndButton.Hidden = false;
                dirtyBackEndLabel.SizeToFit ();
                ViewFramer.Create (dirtyBackEndLabel).Y (yOffset);
                yOffset += dirtyBackEndLabel.Frame.Height + 10;
                ViewFramer.Create (dirtyBackEndButton).Y (yOffset);
                yOffset = yOffset + dirtyBackEndButton.Frame.Height + 10;
            } else {
                dirtyBackEndLabel.Hidden = true;
                dirtyBackEndButton.Hidden = true;
            }
                
            var versionY = NMath.Max (View.Frame.Height - 30, yOffset);
            ViewFramer.Create (versionLabel).Y (versionY);
            versionY = versionLabel.Frame.Bottom + 20;

            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            var contentFrame = new CGRect (0, 0, View.Frame.Width, versionY);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected override void Cleanup ()
        {
            dirtyBackEndButton.TouchUpInside -= FixBackEndButtonClicked;
            dirtyBackEndButton = null;

            var accountInfoView = (AccountInfoView)contentView.ViewWithTag (ACCOUNT_INFO_VIEW_TAG);
            accountInfoView.OnAccountSelected = null;
            accountInfoView.Cleanup ();

            aboutUsButton.TouchUpInside -= AboutUsTapHandler;
            aboutUsButton = null;

            privacyPolicyButton.TouchUpInside -= PrivacyPolicyTapHandler;
            privacyPolicyButton = null;

            releaseNotesButton.TouchUpInside -= ReleaseNotesTapHandler;
            releaseNotesButton = null;
        }

        protected void AccountSettingsTapHandler (McAccount account)
        {
            View.EndEditing (true);
            PerformSegue ("SegueToAccountSettings", new SegueHolder (account));
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

        protected void ReleaseNotesTapHandler (object sender, EventArgs e)
        {
            PerformSegue ("SegueToReleaseNotes", this);
            View.EndEditing (true);
        }

        protected void FixBackEndButtonClicked (object sender, EventArgs e)
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {

                BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (LoginHelpers.GetCurrentAccountId ());

                if (BackEndStateEnum.CredWait == backEndState || BackEndStateEnum.CertAskWait == backEndState) {
                    UIStoryboard x = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
                    CredentialsAskViewController cvc = (CredentialsAskViewController)x.InstantiateViewController ("CredentialsAskViewController");
                    cvc.SetTabBarController ((NachoTabBarController)this.TabBarController);
                    this.PresentViewController (cvc, true, null);
                }

                int accountId = LoginHelpers.GetCurrentAccountId ();
                if (BackEndStateEnum.ServerConfWait == backEndState) {
                    var x = (AppDelegate)UIApplication.SharedApplication.Delegate;
                    x.ServConfReqCallback (accountId);
                }
            }
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
            if (segue.Identifier.Equals ("SegueToReleaseNotes")) {
                var settingsLegal = (SettingsLegalViewController)segue.DestinationViewController.ChildViewControllers [0];
                var url = NSBundle.MainBundle.PathForResource ("ReleaseNotes", "txt", "", "").ToString ();
                settingsLegal.SetProperties (url, "Release Notes", null, false);
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
