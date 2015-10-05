using System;
using CoreGraphics;
using System.Collections.Generic;
using Foundation;
using UIKit;
using UIImageEffectsBinding;
using NachoCore;
using NachoCore.Utils;


namespace NachoClient.iOS
{
    [Register ("CertificateView")]

    public class CertificateView: UIView
    {
        INachoCertificateResponderParent owner;

        UIView certificateView;
        UITextView certificateViewTitle;
        UILabel descriptionOfProblem;
        UITextView certificateInformation;
        UIButton trustCertificateButton;
        UIButton dontTrustCertificateButton;
        UIView horizontalLineAboveButtons;
        UIView verticalLineBetweenButtons;
        UIView horizontalLineAfterDescriptionOfProblem;

        int callbackAccountId;

        protected string certInfo;
        protected string certCommonName;
        protected string certOrganization;


        public CertificateView (IntPtr handle) : base (handle)
        {
        }

        public CertificateView (CGRect rect, INachoCertificateResponderParent owner) : base (rect)
        {
            this.owner = owner;
            this.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (.7f);

            certificateView = new UIView (new CGRect (20, 64, Bounds.Width - 40, Bounds.Height - 128));
            certificateView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            certificateView.BackgroundColor = UIColor.White;
            certificateView.Layer.CornerRadius = 7.0f;
            certificateView.Alpha = 1.0f;
            certificateView.AccessibilityLabel = "Security Warning";
            certificateView.AccessibilityIdentifier = "SecurityWarning";

            certificateViewTitle = new UITextView (new CGRect (8, 2, certificateView.Bounds.Width - 16, 40));
            certificateViewTitle.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            certificateViewTitle.BackgroundColor = UIColor.White;
            certificateViewTitle.Alpha = 1.0f;
            certificateViewTitle.Font = UIFont.SystemFontOfSize (17);
            certificateViewTitle.TextColor = A.Color_SystemBlue;
            certificateViewTitle.Text = "Security Warning";
            certificateViewTitle.TextAlignment = UITextAlignment.Center;
            certificateViewTitle.Editable = false;
            certificateView.Add (certificateViewTitle);

            descriptionOfProblem = new UILabel (new CGRect (15, 47, certificateView.Bounds.Width - 30, 230));
            descriptionOfProblem.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            descriptionOfProblem.TextColor = UIColor.Black;
            descriptionOfProblem.Font = A.Font_AvenirNextMedium12;
            descriptionOfProblem.Alpha = 1.0f;
            descriptionOfProblem.BackgroundColor = UIColor.White;
            descriptionOfProblem.LineBreakMode = UILineBreakMode.WordWrap;
            descriptionOfProblem.Lines = 0;
            certificateView.Add (descriptionOfProblem);

            //Create certificate body: Main body of text giving all information about the certificate
            certificateInformation = new UITextView (new CGRect (15, 47 + 236, certificateView.Bounds.Width - 30, certificateView.Bounds.Height - 100 - 236));
            certificateInformation.AutoresizingMask = UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleHeight;
            certificateInformation.BackgroundColor = UIColor.White;
            certificateInformation.TextColor = UIColor.Black;
            certificateInformation.Font = A.Font_AvenirNextRegular12;
            certificateInformation.Alpha = 1.0f;
            certificateInformation.TextAlignment = UITextAlignment.Left;
            certificateInformation.Editable = false;
            certificateView.Add (certificateInformation);

            //Create trust button: Button on bottom-left side of view that says "Trust"
            trustCertificateButton = new UIButton (new CGRect (0, certificateView.Bounds.Height - 44, certificateView.Bounds.Width / 2, 44));
            trustCertificateButton.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleRightMargin;
            trustCertificateButton.Layer.CornerRadius = 10.0f;
            trustCertificateButton.BackgroundColor = UIColor.White;
            trustCertificateButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            trustCertificateButton.SetTitle ("Allow", UIControlState.Normal);
            trustCertificateButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal); 
            trustCertificateButton.AccessibilityLabel = "Allow";
            trustCertificateButton.AccessibilityIdentifier = "Allow";
            trustCertificateButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissView ();
                this.owner.AcceptCertificate (callbackAccountId);
            };
            certificateView.Add (trustCertificateButton);

            //Create cancel button: Button on bottom-right side of view that says "Cancel"
            dontTrustCertificateButton = new UIButton (new CGRect (certificateView.Bounds.Width / 2, certificateView.Bounds.Height - 44, certificateView.Bounds.Width / 2, 44));
            dontTrustCertificateButton.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth | UIViewAutoresizing.FlexibleLeftMargin;
            dontTrustCertificateButton.Layer.CornerRadius = 10.0f;
            dontTrustCertificateButton.BackgroundColor = UIColor.White;
            dontTrustCertificateButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            dontTrustCertificateButton.SetTitle ("Cancel", UIControlState.Normal);
            dontTrustCertificateButton.AccessibilityLabel = "Cancel";
            dontTrustCertificateButton.AccessibilityIdentifier = "Cancel";
            dontTrustCertificateButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            dontTrustCertificateButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissView ();
                this.owner.DontAcceptCertificate (callbackAccountId);
            };
            certificateView.Add (dontTrustCertificateButton);

            horizontalLineAboveButtons = new UIView (new CGRect (0, certificateView.Bounds.Height - 45, certificateView.Bounds.Width, .5f));
            horizontalLineAboveButtons.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleWidth;
            horizontalLineAboveButtons.BackgroundColor = UIColor.LightGray;
            certificateView.Add (horizontalLineAboveButtons);

            verticalLineBetweenButtons = new UIView (new CGRect (certificateView.Bounds.Width / 2, certificateView.Bounds.Height - 45, .5f, 45));
            verticalLineBetweenButtons.AutoresizingMask = UIViewAutoresizing.FlexibleTopMargin | UIViewAutoresizing.FlexibleLeftMargin | UIViewAutoresizing.FlexibleRightMargin;
            verticalLineBetweenButtons.BackgroundColor = UIColor.LightGray;
            certificateView.Add (verticalLineBetweenButtons);

            horizontalLineAfterDescriptionOfProblem = new UIView (new CGRect (15, 47 + 236, certificateView.Frame.Width - 30, .5f));
            horizontalLineAfterDescriptionOfProblem.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            horizontalLineAfterDescriptionOfProblem.BackgroundColor = UIColor.LightGray;
            certificateView.Add (horizontalLineAfterDescriptionOfProblem);

            this.Add (certificateView);
            this.Alpha = 0.0f;
        }

        private const string problemFormat = "Do you want to allow {0} from {1} to provide information about your account?\n\nYou should only allow sources you know and trust to configure your account.";

        public void ConfigureView ()
        {
            descriptionOfProblem.Text = String.Format (problemFormat, certCommonName, certOrganization);
            certificateInformation.Text = certInfo;
            SetNeedsLayout ();
        }

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();

            var size = descriptionOfProblem.SizeThatFits (descriptionOfProblem.Frame.Size);
            var frame = descriptionOfProblem.Frame;
            frame.Height = size.Height;
            descriptionOfProblem.Frame = frame;

            frame = horizontalLineAfterDescriptionOfProblem.Frame;
            frame.Y = descriptionOfProblem.Frame.Bottom + 5;
            horizontalLineAfterDescriptionOfProblem.Frame = frame;

            frame = certificateInformation.Frame;
            frame.Y = horizontalLineAfterDescriptionOfProblem.Frame.Bottom + 5;
            frame.Height = certificateView.Bounds.Height - 50 - frame.Y;
            certificateInformation.Frame = frame;
        }

        public void SetCertificateInformation (int accountId, NachoCore.Model.McAccount.AccountCapabilityEnum capability)
        {
            callbackAccountId = accountId;
            var certToBeExamined = BackEnd.Instance.ServerCertToBeExamined (accountId, capability);
            if (null == certToBeExamined) {
                certInfo = "Unable to find certificate to be examined.";
                certCommonName = "error";
                certOrganization = "error";
            } else {
                certInfo = CertificateHelper.FormatCertificateData (certToBeExamined);
                certCommonName = CertificateHelper.GetCommonName (certToBeExamined);
                certOrganization = CertificateHelper.GetOrganizationname (certToBeExamined);
            }
            ConfigureView ();
        }

        public void ShowView ()
        {
            UIView.AnimateKeyframes (.5, 0, UIViewKeyframeAnimationOptions.OverrideInheritedDuration, () => {

                UIView.AddKeyframeWithRelativeStartTime (0, 1, () => {
                    this.Alpha = 1.0f;
                });

            }, ((bool finished) => {
            }));
        }

        public void DismissView ()
        {
            this.Alpha = 0.0f;
        }
    }
}
