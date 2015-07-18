// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreGraphics;
using NachoCore.Model;
using NachoCore.Utils;
using Foundation;
using UIKit;
using NachoPlatform;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class CredentialsAskViewController : NcUIViewController, INachoCertificateResponderParent
    {
        protected nfloat yOffset = 0;
        protected const int EMAIL_FIELD_TAG = 100;
        protected const int PASSWORD_FIELD_TAG = 101;
        protected const int SUBMIT_BUTTON_TAG = 102;
        CertificateView certificateView;

        int theAccountId;

        public CredentialsAskViewController (IntPtr handle) : base (handle)
        {
        }

        public void SetAccountId (int accountId)
        {
            theAccountId = accountId;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateView ();
            LayoutView ();
            ConfigureView ();

            certificateView = new CertificateView (View.Frame, this);
            View.Add (certificateView);
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            NSNotificationCenter.DefaultCenter.AddObserver (UITextField.TextFieldTextDidChangeNotification, OnTextFieldChanged);

            // FIXME STEVE
            BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccountId, McAccount.AccountCapabilityEnum.EmailSender);
            if (BackEndStateEnum.CertAskWait == backEndState) {
                certificateView.SetCertificateInformation (theAccountId, McAccount.AccountCapabilityEnum.EmailSender);
                certificateView.ShowView ();
            }
        }

        protected void CreateView ()
        {
            scrollView.BackgroundColor = A.Color_NachoGreen;
            View.Add (scrollView);
            contentView.BackgroundColor = A.Color_NachoGreen;

            yOffset = 40;

            UIButton escape = new UIButton (new CGRect (20, yOffset, 20, 20));
            escape.AccessibilityLabel = "Close";
            escape.SetImage (UIImage.FromBundle ("navbar-icn-close"), UIControlState.Normal);
            escape.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                DismissViewController (true, null);
            };
            contentView.Add (escape);

            yOffset = escape.Frame.Bottom + 40;

            UILabel errorMessage = new UILabel (new CGRect (25, yOffset, View.Frame.Width - 50, 70));
            errorMessage.TextAlignment = UITextAlignment.Center;
            errorMessage.Lines = 3;
            errorMessage.Text = "Your credentials are no longer valid. Please update your credentials.";
            errorMessage.Font = A.Font_AvenirNextRegular17;
            errorMessage.TextColor = UIColor.White;
            contentView.AddSubview (errorMessage);

            yOffset = errorMessage.Frame.Bottom + 20;

            UIView emailBox = new UIView (new CGRect (25, yOffset, View.Frame.Width - 50, 44));
            emailBox.BackgroundColor = UIColor.White;

            var emailField = new UITextField (new CGRect (100, 0, emailBox.Frame.Width - 100, emailBox.Frame.Height));
            emailField.BackgroundColor = UIColor.White;
            emailField.Placeholder = "email@company.com";
            emailField.Font = A.Font_AvenirNextRegular14;
            emailField.BorderStyle = UITextBorderStyle.None;
            emailField.TextAlignment = UITextAlignment.Left;
            emailField.KeyboardType = UIKeyboardType.EmailAddress;
            emailField.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailField.AutocorrectionType = UITextAutocorrectionType.No;
            emailField.Tag = EMAIL_FIELD_TAG;
            emailBox.AddSubview (emailField);

            UILabel emailLabel = new UILabel (new CGRect (10, 0, 60, 44));
            emailLabel.Text = "Email";
            emailLabel.BackgroundColor = UIColor.White;
            emailLabel.TextColor = A.Color_NachoGreen;
            emailLabel.Font = A.Font_AvenirNextMedium14;
            emailBox.AddSubview (emailLabel);
            emailBox.UserInteractionEnabled = true;
            contentView.AddSubview (emailBox);

            yOffset = emailBox.Frame.Bottom + 5f;

            UIView passwordBox = new UIView (new CGRect (25, yOffset, View.Frame.Width - 50, 44));
            passwordBox.BackgroundColor = UIColor.White;

            var passwordField = new UITextField (new CGRect (100, 0, passwordBox.Frame.Width - 100, passwordBox.Frame.Height));
            passwordField.BackgroundColor = UIColor.White;
            passwordField.Placeholder = "Required";
            passwordField.Font = A.Font_AvenirNextRegular14;
            passwordField.BorderStyle = UITextBorderStyle.None;
            passwordField.TextAlignment = UITextAlignment.Left;
            passwordField.SecureTextEntry = true;
            passwordField.KeyboardType = UIKeyboardType.Default;
            passwordField.AutocapitalizationType = UITextAutocapitalizationType.None;
            passwordField.AutocorrectionType = UITextAutocorrectionType.No;
            passwordField.Tag = PASSWORD_FIELD_TAG;
            passwordField.ShouldReturn += ((textField) => {
                textField.ResignFirstResponder ();
                return true;
            });
            passwordBox.AddSubview (passwordField);
            passwordBox.UserInteractionEnabled = true;

            UILabel passwordLabel = new UILabel (new CGRect (10, 0, 80, 44));
            passwordLabel.Text = "Password";
            passwordLabel.BackgroundColor = UIColor.White;
            passwordLabel.TextColor = A.Color_NachoGreen;
            passwordLabel.Font = A.Font_AvenirNextMedium14;
            passwordBox.AddSubview (passwordLabel);
            contentView.AddSubview (passwordBox);

            yOffset = passwordBox.Frame.Bottom + 5f;

            var submitButton = new UIButton (new CGRect (25, yOffset, View.Frame.Width - 50, 45));
            submitButton.BackgroundColor = A.Color_NachoBlue;
            submitButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            submitButton.SetTitle ("Connect", UIControlState.Normal);
            submitButton.AccessibilityLabel = "Connect";
            submitButton.TitleLabel.TextColor = UIColor.White;
            submitButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            submitButton.Tag = SUBMIT_BUTTON_TAG;
            contentView.AddSubview (submitButton);

            submitButton.TouchUpInside += delegate {
                if (!EmailHelper.IsValidEmail (emailField.Text)) {
                    errorMessage.Text = "The email address you entered is not valid. Please update and try again.";
                } else if (null != McAccount.QueryByEmailAddr (emailField.Text).FirstOrDefault ()) {
                    errorMessage.Text = "That email address is already in use. Duplicate accounts are not supported.";
                } else {
                    McCred UsersCredentials = McCred.QueryByAccountId<McCred> (theAccountId).SingleOrDefault ();
                    UsersCredentials.Username = emailField.Text;
                    UsersCredentials.UpdatePassword (passwordField.Text);
                    UsersCredentials.Update ();
                    BackEnd.Instance.CredResp (theAccountId);
                    View.EndEditing (true);
                    DismissViewController (true, null);
                    LoginHelpers.UserInterventionStateChanged (theAccountId);
                }
            };

            emailField.ShouldReturn += ((textField) => {
                passwordField.BecomeFirstResponder ();
                return true;
            });
            yOffset = submitButton.Frame.Bottom + 20f;
            scrollView.AddSubview (contentView);
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            var contentFrame = new CGRect (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        public void ConfigureView ()
        {
            UITextField emailField = (UITextField)View.ViewWithTag (EMAIL_FIELD_TAG);
            emailField.Text = GetUsername ();
            emailField.TextColor = A.Color_NachoRed;

            UITextField passwordField = (UITextField)View.ViewWithTag (PASSWORD_FIELD_TAG);
            passwordField.Text = GetPassword ();
            passwordField.TextColor = A.Color_NachoRed;
        }

        protected string GetUsername ()
        {
            var cred = McCred.QueryByAccountId<McCred> (theAccountId).SingleOrDefault ();
            return cred.Username;
        }

        protected string GetPassword ()
        {

            var cred = McCred.QueryByAccountId<McCred> (theAccountId).SingleOrDefault ();
            return cred.GetPassword ();
        }

        private void OnTextFieldChanged (NSNotification notification)
        {
            maybeEnableConnect ();
        }

        protected void maybeEnableConnect ()
        {
            var emailTextField = (UITextField)contentView.ViewWithTag (EMAIL_FIELD_TAG);
            var passwordTextField = (UITextField)contentView.ViewWithTag (PASSWORD_FIELD_TAG);

            var email = emailTextField.Text;
            var password = passwordTextField.Text;

            var shouldWe = ((0 < email.Length) && (0 < password.Length));

            var submitButton = (UIButton)contentView.ViewWithTag (SUBMIT_BUTTON_TAG);
            submitButton.Enabled = shouldWe;
            submitButton.Alpha = (shouldWe ? 1.0f : 0.5f);
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            // FIXME STEVE
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            LoginHelpers.UserInterventionStateChanged (accountId);
            View.EndEditing (true);
            DismissViewController (true, null);
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            // FIXME STEVE - need to deal with > 1 server scenarios (McAccount.AccountCapabilityEnum).
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            LoginHelpers.UserInterventionStateChanged (accountId);
            View.EndEditing (true);
            DismissViewController (true, null);
        }

        protected override void OnKeyboardChanged ()
        {
            LayoutView ();
            var connectbutton = contentView.ViewWithTag (SUBMIT_BUTTON_TAG);
            scrollView.ScrollRectToVisible (connectbutton.Frame, false);
        }
    }
}
