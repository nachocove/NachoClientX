// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using SWRevealViewControllerBinding;


namespace NachoClient.iOS
{
    public partial class GeneralSettingsViewController : NcUIViewController
    {
        protected static float CELL_HEIGHT = 44f;
        protected static float INSET = 15f;
        protected float TEXT_LINE_HEIGHT = 19.124f;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected float yOffset;
        protected string legalUrl;
        protected string legalTitle;
        protected bool isUrl;

        public GeneralSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            // Navigation
            NavigationItem.Title = "Settings";
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] {
                A.RevealButton (this),
                A.NachoNowButton (this),
            };

            CreateView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            ConfigureView ();
        }

        protected const int EMAIL_ADDRESS_LABEL_TAG = 100;

        protected void CreateView ()
        {
            View.BackgroundColor = A.Color_NachoNowBackground;
            contentView.BackgroundColor = A.Color_NachoNowBackground;

            yOffset = 20;

            UILabel accountSettingsLabel = new UILabel (new RectangleF (INSET, yOffset, contentView.Frame.Width, 20));
            accountSettingsLabel.Text = "ACCOUNT SETTINGS";
            accountSettingsLabel.Font = A.Font_AvenirNextRegular14;
            accountSettingsLabel.TextColor = A.Color_NachoBlack;
            contentView.AddSubview (accountSettingsLabel);

            yOffset = accountSettingsLabel.Frame.Bottom + 5;

            contentView.AddSubview (AddHorizontalLine (0, yOffset - .5f, View.Frame.Width, A.Color_NachoBorderGray));

            UIView accountSettingsCell = new UIView (new RectangleF (0, yOffset, contentView.Frame.Width, CELL_HEIGHT));
            accountSettingsCell.BackgroundColor = UIColor.White;

            var accountTap = new UITapGestureRecognizer ();
            accountTap.AddTarget (() => {
                View.EndEditing (true);
                PerformSegue ("GeneralSettingsToSettings", this);
            });
            accountSettingsCell.AddGestureRecognizer (accountTap);

            UIImageView mailIcon = new UIImageView (new RectangleF (15, 14.5f, 15, 15));
            mailIcon.Image = UIImage.FromBundle ("icn-inbox");
            accountSettingsCell.AddSubview (mailIcon);

            UILabel accountEmailAddress = new UILabel (new RectangleF (40, 12.438f, 200, TEXT_LINE_HEIGHT));
            accountEmailAddress.Tag = EMAIL_ADDRESS_LABEL_TAG;
            accountEmailAddress.Text = "";
            accountEmailAddress.Font = A.Font_AvenirNextRegular14;
            accountEmailAddress.TextColor = A.Color_NachoBlack;
            accountSettingsCell.AddSubview (accountEmailAddress);

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, accountSettingsCell);
            contentView.AddSubview (accountSettingsCell);

            yOffset = accountSettingsCell.Frame.Bottom;

            contentView.AddSubview (AddHorizontalLine (0, yOffset, View.Frame.Width, A.Color_NachoBorderGray));

            yOffset += 30;

            UIView privacyStatementCell = new UIView (new RectangleF (0, yOffset, contentView.Frame.Width, CELL_HEIGHT));
            privacyStatementCell.BackgroundColor = UIColor.White;

            var privacyTap = new UITapGestureRecognizer ();
            privacyTap.AddTarget (() => {
                legalUrl = "https://nachocove.com/privacy-policy-text/";
                legalTitle = "Privacy Policy";
                isUrl = true;
                PerformSegue ("GeneralSettingsToSettingsLegal", this);
                View.EndEditing (true);
            });
            privacyStatementCell.AddGestureRecognizer (privacyTap);

            UILabel privacyStatmentLabel = new UILabel (new RectangleF (INSET, 12.438f, 200, TEXT_LINE_HEIGHT));
            privacyStatmentLabel.Text = "Privacy Statement";
            privacyStatmentLabel.Font = A.Font_AvenirNextRegular14;
            privacyStatmentLabel.TextColor = A.Color_NachoBlack;
            privacyStatementCell.AddSubview (privacyStatmentLabel);

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, privacyStatementCell);
            contentView.AddSubview (privacyStatementCell);

            yOffset = privacyStatementCell.Frame.Bottom;

            UIView licenseAgreementCell = new UIView (new RectangleF (0, yOffset, contentView.Frame.Width, CELL_HEIGHT));
            licenseAgreementCell.BackgroundColor = UIColor.White;

            var licenseTap = new UITapGestureRecognizer ();
            licenseTap.AddTarget (() => {
                legalUrl = "https://nachocove.com/legal-text/";
                legalTitle = "License Agreement";
                isUrl = true;
                PerformSegue ("GeneralSettingsToSettingsLegal", this);
                View.EndEditing (true);
            });
            licenseAgreementCell.AddGestureRecognizer (licenseTap);

            UILabel licenseAgreementLabel = new UILabel (new RectangleF (INSET, 12.438f, 200, TEXT_LINE_HEIGHT));
            licenseAgreementLabel.Text = "License Agreement";
            licenseAgreementLabel.Font = A.Font_AvenirNextRegular14;
            licenseAgreementLabel.TextColor = A.Color_NachoBlack;
            licenseAgreementCell.AddSubview (licenseAgreementLabel);

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, licenseAgreementCell);
            contentView.AddSubview (licenseAgreementCell);

            yOffset = licenseAgreementCell.Frame.Bottom;

            UIView openSourceCell = new UIView (new RectangleF (0, yOffset, contentView.Frame.Width, CELL_HEIGHT));
            openSourceCell.BackgroundColor = UIColor.White;

            var openSourceTap = new UITapGestureRecognizer ();
            openSourceTap.AddTarget (() => {
                View.EndEditing (true);
            });
            openSourceCell.AddGestureRecognizer (openSourceTap);

            UILabel openSourceLabel = new UILabel (new RectangleF (INSET, 12.438f, 200, TEXT_LINE_HEIGHT));
            openSourceLabel.Text = "Open Source Attributions";
            openSourceLabel.Font = A.Font_AvenirNextRegular14;
            openSourceLabel.TextColor = A.Color_NachoBlack;
            openSourceCell.AddSubview (openSourceLabel);

            Util.AddArrowAccessory (SCREEN_WIDTH - 23, CELL_HEIGHT / 2 - 6, 12, openSourceCell);
            contentView.AddSubview (openSourceCell);

            yOffset = openSourceCell.Frame.Bottom;

            contentView.AddSubview (AddHorizontalLine (0, privacyStatementCell.Frame.Top, View.Frame.Width, A.Color_NachoBorderGray));
            contentView.AddSubview (AddHorizontalLine (INSET, privacyStatementCell.Frame.Bottom, View.Frame.Width, A.Color_NachoBorderGray));
            contentView.AddSubview (AddHorizontalLine (INSET, licenseAgreementCell.Frame.Bottom, View.Frame.Width, A.Color_NachoBorderGray));
            contentView.AddSubview (AddHorizontalLine (0, openSourceCell.Frame.Bottom, View.Frame.Width, A.Color_NachoBorderGray));
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
            var contentFrame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        protected void ConfigureView ()
        {
            var emailLabel = (UILabel)contentView.ViewWithTag (EMAIL_ADDRESS_LABEL_TAG);
            emailLabel.Text = GetEmailAddress ();
            LayoutView ();
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
                var settingsLegal = (SettingsLegalViewController)segue.DestinationViewController.ChildViewControllers [0];
                if (isUrl) {
                    settingsLegal.SetProperties (legalUrl, legalTitle, true);
                } else {
                    settingsLegal.SetProperties ("", legalTitle, false);
                }
                return;
            }
            if (segue.Identifier.Equals ("GeneralSettingsToSettings")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToNachoNow")) {
                // Nothing to do
                return;
            }
                
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }
    }
}
