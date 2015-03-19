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

        protected string certInfo;
        protected string certCommonName;
        protected string certOrganization;

        public CertificateView ()
        {

        }

        public void SetOwner (INachoCertificateResponderParent owner)
        {
            this.owner = owner;
        }

        public CertificateView (CGRect frame)
        {
            this.Frame = frame;
        }

        public CertificateView (IntPtr handle) : base (handle)
        {

        }

        const int GRAY_BACKGROUND_TAG = 20;
        const int CERTIFICATE_VIEW_TAG = 21;
        const int CERTIFICATE_INFORMATION_TEXT_TAG = 22;
        const int TRUST_CERTIFICATE_BUTTON_TAG = 23;
        const int CANCEL_CERTIFICATE_BUTTON_TAG = 24;
        const int PROBLEM_TAG = 25;
        const int CERTIFICATE_LINE_TAG = 26;

        public void CreateView ()
        {
            nfloat VIEW_HEIGHT;
            VIEW_HEIGHT = this.Frame.Height - 64f;

            //this.BackgroundColor = UIColor.LightGray.ColorWithAlpha (.4f);
            this.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (.7f);
            ;
            certificateView = new UIView (new CGRect (20, 64, Frame.Width - 40, VIEW_HEIGHT - 64));
            certificateView.Tag = CERTIFICATE_VIEW_TAG;
            certificateView.BackgroundColor = UIColor.White;
            certificateView.Layer.CornerRadius = 7.0f;
            certificateView.Alpha = 1.0f;
            certificateView.AccessibilityLabel = "Security Warning";
            certificateView.AccessibilityIdentifier = "SecurityWarning";

            UITextView certificateViewTitle = new UITextView (new CGRect (8, 2, certificateView.Frame.Width - 16, 40));
            certificateViewTitle.BackgroundColor = UIColor.White;
            certificateViewTitle.Alpha = 1.0f;
            certificateViewTitle.Font = UIFont.SystemFontOfSize (17);
            certificateViewTitle.TextColor = A.Color_SystemBlue;
            certificateViewTitle.Text = "Security Warning";
            certificateViewTitle.TextAlignment = UITextAlignment.Center;
            certificateViewTitle.Editable = false;
            certificateView.Add (certificateViewTitle);

            UILabel descriptionOfProblem = new UILabel (new CGRect (15, 47, certificateView.Frame.Width - 30, 230));

            descriptionOfProblem.TextColor = UIColor.Black;
            descriptionOfProblem.Font = A.Font_AvenirNextMedium12;
            descriptionOfProblem.Alpha = 1.0f;
            descriptionOfProblem.BackgroundColor = UIColor.White;
            descriptionOfProblem.LineBreakMode = UILineBreakMode.WordWrap;
            descriptionOfProblem.Lines = 0;
            descriptionOfProblem.Tag = PROBLEM_TAG;
            certificateView.Add (descriptionOfProblem);

            //Create certificate body: Main body of text giving all information about the certificate
            UITextView certificateInformation = new UITextView (new CGRect (15, 47 + 236, certificateView.Frame.Width - 30, certificateView.Frame.Height - 100 - 236));
            certificateInformation.BackgroundColor = UIColor.White;
            certificateInformation.TextColor = UIColor.Black;
            certificateInformation.Font = A.Font_AvenirNextRegular12;
            certificateInformation.Alpha = 1.0f;
            certificateInformation.TextAlignment = UITextAlignment.Left;
            certificateInformation.Tag = CERTIFICATE_INFORMATION_TEXT_TAG;
            certificateInformation.Editable = false;
            certificateView.Add (certificateInformation);

            //Create trust button: Button on bottom-left side of view that says "Trust"
            UIButton trustCertificateButton = new UIButton (new CGRect (0, certificateView.Frame.Height - 44, certificateView.Frame.Width / 2, 44));
            trustCertificateButton.Layer.CornerRadius = 10.0f;
            trustCertificateButton.BackgroundColor = UIColor.White;
            trustCertificateButton.Tag = TRUST_CERTIFICATE_BUTTON_TAG;
            trustCertificateButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            trustCertificateButton.SetTitle ("Allow", UIControlState.Normal);
            trustCertificateButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal); 
            trustCertificateButton.AccessibilityLabel = "Allow";
            trustCertificateButton.AccessibilityIdentifier = "Allow";
            trustCertificateButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissView ();
                owner.AcceptCertificate ();
            };
            certificateView.Add (trustCertificateButton);

            //Create cancel button: Button on bottom-right side of view that says "Cancel"
            UIButton dontTrustCertificateButton = new UIButton (new CGRect (certificateView.Frame.Width / 2, certificateView.Frame.Height - 44, certificateView.Frame.Width / 2, 44));
            dontTrustCertificateButton.Layer.CornerRadius = 10.0f;
            dontTrustCertificateButton.BackgroundColor = UIColor.White;
            dontTrustCertificateButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            dontTrustCertificateButton.SetTitle ("Cancel", UIControlState.Normal);
            dontTrustCertificateButton.AccessibilityLabel = "Cancel";
            dontTrustCertificateButton.AccessibilityIdentifier = "Cancel";
            dontTrustCertificateButton.Tag = CANCEL_CERTIFICATE_BUTTON_TAG;
            dontTrustCertificateButton.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            dontTrustCertificateButton.TouchUpInside += (object sender, EventArgs e) => {
                DismissView ();
                owner.DontAcceptCertificate ();
            };
            certificateView.Add (dontTrustCertificateButton);

            UIView horizontalLineAboveButtons = new UIView (new CGRect (0, certificateView.Frame.Height - 45, certificateView.Frame.Width, .5f));
            horizontalLineAboveButtons.BackgroundColor = UIColor.LightGray;
            certificateView.Add (horizontalLineAboveButtons);

            UIView verticalLineBetweenButtons = new UIView (new CGRect (certificateView.Frame.Width / 2, certificateView.Frame.Height - 45, .5f, 45));
            verticalLineBetweenButtons.BackgroundColor = UIColor.LightGray;
            certificateView.Add (verticalLineBetweenButtons);

            UIView horizontalLineAfterDescriptionOfProblem = new UIView (new CGRect (15, 47 + 236, certificateView.Frame.Width - 30, .5f));
            horizontalLineAfterDescriptionOfProblem.Tag = CERTIFICATE_LINE_TAG;
            horizontalLineAfterDescriptionOfProblem.BackgroundColor = UIColor.LightGray;
            certificateView.Add (horizontalLineAfterDescriptionOfProblem);

            this.Add (certificateView);
            this.Alpha = 0.0f;
        }

        private const string problemFormat = "Do you want to allow {0} from {1} to provide information about your account?\n\nYou should only allow sources you know and trust to configure your account.";

        public void ConfigureView ()
        {
            var descriptionOfProblem = (UILabel)certificateView.ViewWithTag (PROBLEM_TAG);
            descriptionOfProblem.Text = String.Format (problemFormat, certCommonName, certOrganization);
            descriptionOfProblem.SizeToFit ();

            var horizontalLineAfterDescriptionOfProblem = certificateView.ViewWithTag (CERTIFICATE_LINE_TAG);
            ViewFramer.Create (horizontalLineAfterDescriptionOfProblem).Y (descriptionOfProblem.Frame.Bottom + 5);

            var certY = horizontalLineAfterDescriptionOfProblem.Frame.Bottom + 5;

            var certInfoTextView = (UITextView)certificateView.ViewWithTag (CERTIFICATE_INFORMATION_TEXT_TAG);
            ViewFramer.Create (certInfoTextView).Y (certY);
            ViewFramer.Create (certInfoTextView).Height (certificateView.Frame.Height - 50 - certY);
            certInfoTextView.Text = certInfo;
        }

        public void SetCertificateInformation ()
        {
            var certToBeExamined = BackEnd.Instance.ServerCertToBeExamined (LoginHelpers.GetCurrentAccountId ());
            certInfo = CertificateHelper.FormatCertificateData (certToBeExamined);
            certCommonName = CertificateHelper.GetCommonName (certToBeExamined);
            certOrganization = CertificateHelper.GetOrganizationname (certToBeExamined);
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
