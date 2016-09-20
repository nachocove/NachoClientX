// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using UIKit;

namespace NachoClient.iOS
{
    public partial class AboutUsViewController : NcUIViewControllerNoLeaks
    {
        public static string PRIVACY_POLICY_KEY = "PRIVACY_POLICY";
        public static string LICENSE_AGREEMENT_KEY = "LICENSE_AGREEMENT";

        protected static readonly int INDENT = 18;
        protected static readonly int CELL_HEIGHT = 44;

        protected nfloat yOffset;

        protected UIView contentView;
        protected UIScrollView scrollView;

        protected string url;
        protected string title;
        protected string key;
        protected bool loadFromWeb;

        UILabel versionLabel;
        UIButton showLicenseButton;
        UIButton showOpenSourceButton;
        UIButton showReleaseNotesButton;
        UIButton showPrivacyPolicyButton;

        public AboutUsViewController () : base ()
        {
        }

        protected override void CreateViewHierarchy ()
        {
            NavigationItem.Title = "About Apollo Mail";

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            scrollView = new UIScrollView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            scrollView.AutoresizingMask = UIViewAutoresizing.FlexibleDimensions;
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;
            View.AddSubview (scrollView);

            contentView = new UIView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height));
            contentView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.AddSubview (contentView);

            UIView aboutUsView = new UIView (new CGRect (A.Card_Horizontal_Indent, A.Card_Vertical_Indent, View.Frame.Width - A.Card_Horizontal_Indent * 2, 100));
            aboutUsView.BackgroundColor = UIColor.White;
            aboutUsView.Layer.CornerRadius = A.Card_Corner_Radius;
            aboutUsView.Layer.BorderColor = A.Card_Border_Color;
            aboutUsView.Layer.BorderWidth = A.Card_Border_Width;

            yOffset = A.Card_Vertical_Indent;

            UIImageView nachoLogoImageView;
            using (var nachoLogo = UIImage.FromBundle ("AboutLogo")) {
                nachoLogoImageView = new UIImageView (nachoLogo);
            }
            nachoLogoImageView.Frame = new CGRect (aboutUsView.Frame.Width / 2 - 40, yOffset, 80, 80);
            aboutUsView.Add (nachoLogoImageView);

            yOffset = nachoLogoImageView.Frame.Bottom + 15;

            UILabel aboutUsHeaderLabel = new UILabel (new CGRect (INDENT, yOffset, aboutUsView.Frame.Width - INDENT * 2, 100));
            aboutUsHeaderLabel.Font = A.Font_AvenirNextDemiBold17;
            aboutUsHeaderLabel.TextColor = A.Color_NachoGreen;
            aboutUsHeaderLabel.TextAlignment = UITextAlignment.Center;
            aboutUsHeaderLabel.Lines = 4;
            aboutUsHeaderLabel.LineBreakMode = UILineBreakMode.WordWrap;
            aboutUsHeaderLabel.Text = "Apollo Mail believes that productivity software is more than " +
            "just a great email app with contacts and calendar capability.";
            aboutUsView.AddSubview (aboutUsHeaderLabel);

            yOffset = aboutUsHeaderLabel.Frame.Bottom + 15;

            UILabel aboutUsDescriptionLabel = new UILabel (new CGRect (INDENT, yOffset, aboutUsView.Frame.Width - INDENT * 2, 100));
            aboutUsDescriptionLabel.Font = A.Font_AvenirNextRegular14;
            aboutUsDescriptionLabel.TextColor = A.Color_NachoBlack;
            aboutUsDescriptionLabel.TextAlignment = UITextAlignment.Center;
            aboutUsDescriptionLabel.Lines = 5;
            aboutUsDescriptionLabel.LineBreakMode = UILineBreakMode.WordWrap;
            aboutUsDescriptionLabel.Text = "In addition to being a great email " +
            "client, your PIM software should actively help you achieve your" +
            " goals, help you manage your time and reduce clutter that gets " +
            "in your way.";
            aboutUsView.AddSubview (aboutUsDescriptionLabel);

            Util.SetViewHeight (aboutUsView, aboutUsDescriptionLabel.Frame.Bottom + 15);

            contentView.AddSubview (aboutUsView);

            yOffset = aboutUsView.Frame.Bottom + A.Card_Vertical_Indent;

            versionLabel = new UILabel (new CGRect (0, yOffset, View.Frame.Width, 20));
            versionLabel.Font = A.Font_AvenirNextMedium14;
            versionLabel.TextColor = A.Color_NachoGreen;
            versionLabel.TextAlignment = UITextAlignment.Center;
            versionLabel.Text = "Apollo Mail version " + Util.GetVersionNumber ();//"Nacho Mail version 0.9";
            contentView.AddSubview (versionLabel);

            yOffset += versionLabel.Frame.Height + A.Card_Vertical_Indent;

            UIView buttonsView = new UIView (new CGRect (A.Card_Horizontal_Indent, yOffset, View.Frame.Width - (A.Card_Horizontal_Indent * 2), CELL_HEIGHT * 2));
            buttonsView.BackgroundColor = UIColor.White;
            buttonsView.Layer.CornerRadius = A.Card_Corner_Radius;
            buttonsView.Layer.BorderColor = A.Card_Border_Color;
            buttonsView.Layer.BorderWidth = A.Card_Border_Width;

            var bOffset = 0;

            showReleaseNotesButton = UIButton.FromType (UIButtonType.System);
            showReleaseNotesButton.Font = A.Font_AvenirNextMedium14;
            showReleaseNotesButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            showReleaseNotesButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            showReleaseNotesButton.SetTitle ("Release Notes", UIControlState.Normal);
            showReleaseNotesButton.SizeToFit ();
            ViewFramer.Create (showReleaseNotesButton).X (INDENT).CenterY (bOffset, CELL_HEIGHT);
            showReleaseNotesButton.TouchUpInside += ShowReleaseNotesButton_TouchUpInside;
            buttonsView.AddSubview (showReleaseNotesButton);
            bOffset += CELL_HEIGHT;

            Util.AddHorizontalLine (0, bOffset, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);
            bOffset += 1;

            showPrivacyPolicyButton = UIButton.FromType (UIButtonType.System);
            showPrivacyPolicyButton.Font = A.Font_AvenirNextMedium14;
            showPrivacyPolicyButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            showPrivacyPolicyButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            showPrivacyPolicyButton.SetTitle ("Privacy Policy", UIControlState.Normal);
            showPrivacyPolicyButton.SizeToFit ();
            ViewFramer.Create (showPrivacyPolicyButton).X (INDENT).CenterY (bOffset, CELL_HEIGHT);
            showPrivacyPolicyButton.TouchUpInside += ShowPrivacyPolicyButton_TouchUpInside;
            buttonsView.AddSubview (showPrivacyPolicyButton);
            bOffset += CELL_HEIGHT;

            Util.AddHorizontalLine (0, bOffset, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);
            bOffset += 1;

            showLicenseButton = UIButton.FromType (UIButtonType.System);
            showLicenseButton.Font = A.Font_AvenirNextMedium14;
            showLicenseButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            showLicenseButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            showLicenseButton.SetTitle ("Read License Agreement", UIControlState.Normal);
            showLicenseButton.SizeToFit ();
            ViewFramer.Create (showLicenseButton).X (INDENT).CenterY (bOffset, CELL_HEIGHT);
            showLicenseButton.TouchUpInside += ShowLicenseButton_TouchUpInside;
            buttonsView.AddSubview (showLicenseButton);
            bOffset += CELL_HEIGHT;

            Util.AddHorizontalLine (0, bOffset, buttonsView.Frame.Width, A.Color_NachoBorderGray, buttonsView);
            bOffset += 1;

            showOpenSourceButton = UIButton.FromType (UIButtonType.System);
            showOpenSourceButton.Font = A.Font_AvenirNextMedium14;
            showOpenSourceButton.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
            showOpenSourceButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            showOpenSourceButton.SetTitle ("View Open Source Contributions", UIControlState.Normal);
            showOpenSourceButton.SizeToFit ();
            ViewFramer.Create (showOpenSourceButton).X (INDENT).CenterY (bOffset, CELL_HEIGHT);
            showOpenSourceButton.TouchUpInside += ShowOpenSourceButton_TouchUpInside;
            buttonsView.AddSubview (showOpenSourceButton); 
            bOffset += CELL_HEIGHT;

            ViewFramer.Create (buttonsView).Height (bOffset);

            contentView.AddSubview (buttonsView);

            yOffset = buttonsView.Frame.Bottom + A.Card_Vertical_Indent;
        }

        void ShowPrivacyPolicyButton_TouchUpInside (object sender, EventArgs e)
        {
            url = "https://nachocove.com/privacy-policy-text/";
            title = "Privacy Policy";
            key = PRIVACY_POLICY_KEY;
            loadFromWeb = true;
            ShowLegal ();
        }

        void ShowReleaseNotesButton_TouchUpInside (object sender, EventArgs e)
        {
            url = NSBundle.MainBundle.PathForResource ("ReleaseNotes", "txt", "", "").ToString ();
            title = "Release Notes";
            loadFromWeb = false;
            ShowLegal ();
        }

        void ShowOpenSourceButton_TouchUpInside (object sender, EventArgs e)
        {
            url = NSBundle.MainBundle.PathForResource ("LegalInfo", "txt", "", "").ToString ();
            title = "Open Source Contributions";
            loadFromWeb = false;
            ShowLegal ();
        }

        void ShowLicenseButton_TouchUpInside (object sender, EventArgs e)
        {
            url = "https://nachocove.com/legal-text/";
            title = "License Agreement";
            key = LICENSE_AGREEMENT_KEY;
            loadFromWeb = true;
            ShowLegal ();
        }

        void ShowLegal ()
        {
            var vc = new SettingsLegalViewController ();
            vc.SetProperties (url, title, key, loadFromWeb);
            NavigationController.PushViewController (vc, true);
        }

        protected override void ConfigureAndLayout ()
        {
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            contentView.Frame = new CGRect (0, 0, View.Frame.Width, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;
        }

        protected override void Cleanup ()
        {
            showLicenseButton.TouchUpInside -= ShowLicenseButton_TouchUpInside;
            showOpenSourceButton.TouchUpInside -= ShowOpenSourceButton_TouchUpInside;
            showReleaseNotesButton.TouchUpInside -= ShowReleaseNotesButton_TouchUpInside;
            showPrivacyPolicyButton.TouchUpInside -= ShowPrivacyPolicyButton_TouchUpInside;

            showLicenseButton = null;
            showOpenSourceButton = null;
            showReleaseNotesButton = null;
            showPrivacyPolicyButton = null;

            versionLabel = null;
        }
    }
}
