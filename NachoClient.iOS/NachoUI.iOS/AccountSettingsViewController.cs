// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using NachoCore.Model;
using NachoCore.Utils;
using System.Linq;
using NachoCore;

namespace NachoClient.iOS
{
    public partial class AccountSettingsViewController : NcUIViewControllerNoLeaks
    {
        protected UIBarButtonItem editButton = new UIBarButtonItem ();
        protected UIBarButtonItem cancelButton = new UIBarButtonItem ();
        protected UIBarButtonItem saveButton = new UIBarButtonItem ();

        protected bool textFieldsEditable = false;

        protected const float HORIZONTAL_PADDING = 25f;
        protected const float LABEL_WIDTH = 90f;
        protected const float SPACER = 15f;
        protected const float LABEL_HEIGHT = 17f;
        protected const float TEXTFIELD_HEIGHT = 17f;

        protected UIColor labelColor = A.Color_NachoDarkText;
        protected UIColor textFieldColor = A.Color_NachoGreen;
        protected UIFont textFieldFond = A.Font_AvenirNextMedium14;

        protected const int NAME_TAG = 100;
        protected const int USERNAME_TAG = 101;
        protected const int PASSWORD_TAG = 102;
        protected const int EMAIL_TAG = 103;
        protected const int MAILSERVER_TAG = 104;
        protected const int CONFERENCE_TAG = 105;
        protected const int SIGNATURE_TAG = 106;

        protected string ORIGINAL_ACCOUNT_NAME_VALUE = "";
        protected string ORIGINAL_USERNAME_VALUE = "";
        protected string ORIGINAL_PASSWORD_VALUE = "";
        protected string ORIGINAL_EMAIL_VALUE = "";
        protected string ORIGINAL_MAILSERVER_VALUE = "";
        protected string ORIGINAL_CONFERENCE_VALUE = "";
        protected string ORIGINAL_SIGNATURE_VALUE = "";

        protected const int GREY_BACKGROUND_VIEW_TAG = 200;
        protected const int STATUS_VIEW_TAG = 201;

        protected const int DISMISS_CHANGES_ALERT_VIEW_TAG = 300;
        protected const int ERROR_ALERT_VIEW_TAG = 301;
        protected const int CANCEL_VALIDATION_ALERT_VIEW_TAG = 302;
        protected const int BAD_NETWORK_ALERT_VIEW_TAG = 303;

        protected bool handleStatusEnums = true;

        protected enum AccountIssue
        {
            None,
            InvalidHost,
            ErrorAuth,
            ErrorComm,
            ErrorUser,
        }

        protected AccountIssue accountIssue = AccountIssue.None;

        public AccountSettingsViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            View.EndEditing (true);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void ViewDidAppear (bool animated)
        {
            CaptureOriginalSettings ();
            base.ViewDidAppear (animated);
        }

        protected override void CreateViewHierarchy ()
        {
            NavigationController.NavigationBar.Translucent = false;
            NavigationItem.Title = "Account Settings";
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            editButton.Image = UIImage.FromBundle ("gen-edit");
            NavigationItem.RightBarButtonItem = editButton;

            editButton.Clicked += EditButtonClicked;
            saveButton.Clicked += SaveButtonClicked;
            cancelButton.Clicked += CancelButtonClicked;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            UIView settingsView = new UIView (new RectangleF (0, 20, View.Frame.Width, 350));
            settingsView.BackgroundColor = UIColor.White;
            settingsView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            settingsView.Layer.BorderWidth = .5f;

            float yOffset = 17;

            UILabel nameLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextRegular14;
            nameLabel.TextAlignment = UITextAlignment.Left;
            nameLabel.TextColor = labelColor;
            nameLabel.Text = "Name";
            settingsView.Add (nameLabel);

            UITextField nameTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            nameTextField.Placeholder = "Exchange";
            nameTextField.TextColor = textFieldColor;
            nameTextField.Font = textFieldFond;
            nameTextField.TextAlignment = UITextAlignment.Left;
            nameTextField.Tag = NAME_TAG;
            settingsView.Add (nameTextField);

            yOffset = nameTextField.Frame.Bottom + 15;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel usernameLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            usernameLabel.Font = A.Font_AvenirNextRegular14;
            usernameLabel.TextAlignment = UITextAlignment.Left;
            usernameLabel.TextColor = labelColor;
            usernameLabel.Text = "Username";
            settingsView.Add (usernameLabel);

            UITextField usernameTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            usernameTextField.Placeholder = "username";
            usernameTextField.TextColor = textFieldColor;
            usernameTextField.Font = textFieldFond;
            usernameTextField.TextAlignment = UITextAlignment.Left;
            usernameTextField.Tag = USERNAME_TAG;
            settingsView.Add (usernameTextField);

            yOffset = usernameTextField.Frame.Bottom + 15;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel passwordLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            passwordLabel.Font = A.Font_AvenirNextRegular14;
            passwordLabel.TextAlignment = UITextAlignment.Left;
            passwordLabel.TextColor = labelColor;
            passwordLabel.Text = "Password";
            settingsView.Add (passwordLabel);

            UITextField passwordTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            passwordTextField.Placeholder = "********";
            passwordTextField.TextColor = textFieldColor;
            passwordTextField.Font = textFieldFond;
            passwordTextField.TextAlignment = UITextAlignment.Left;
            passwordTextField.SecureTextEntry = true;
            passwordTextField.Tag = PASSWORD_TAG;
            settingsView.Add (passwordTextField);

            yOffset = passwordTextField.Frame.Bottom + 15;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel emailLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            emailLabel.Font = A.Font_AvenirNextRegular14;
            emailLabel.TextAlignment = UITextAlignment.Left;
            emailLabel.TextColor = labelColor;
            emailLabel.Text = "Email";
            settingsView.Add (emailLabel);

            UITextField emailTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            emailTextField.Placeholder = "zachq@nachocove.com";
            emailTextField.TextColor = textFieldColor;
            emailTextField.Font = textFieldFond;
            emailTextField.TextAlignment = UITextAlignment.Left;
            emailTextField.Tag = EMAIL_TAG;
            settingsView.Add (emailTextField);

            yOffset = emailTextField.Frame.Bottom + 15;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel mailserverLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            mailserverLabel.Font = A.Font_AvenirNextRegular14;
            mailserverLabel.TextAlignment = UITextAlignment.Left;
            mailserverLabel.TextColor = labelColor;
            mailserverLabel.Text = "Mail Server";
            settingsView.Add (mailserverLabel);

            UITextField mailserverTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            mailserverTextField.Placeholder = "outlook.office365.com";
            mailserverTextField.TextColor = textFieldColor;
            mailserverTextField.Font = textFieldFond;
            mailserverTextField.TextAlignment = UITextAlignment.Left;
            mailserverTextField.Tag = MAILSERVER_TAG;
            settingsView.Add (mailserverTextField);

            yOffset = mailserverTextField.Frame.Bottom + 15;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel conferencecallLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset - 13, LABEL_WIDTH, LABEL_HEIGHT + 25));
            conferencecallLabel.Font = A.Font_AvenirNextRegular14;
            conferencecallLabel.TextAlignment = UITextAlignment.Left;
            conferencecallLabel.TextColor = labelColor;
            conferencecallLabel.Text = "Conference Call Number";
            conferencecallLabel.Lines = 2;
            conferencecallLabel.LineBreakMode = UILineBreakMode.WordWrap;

            settingsView.Add (conferencecallLabel);

            UITextField conferencecallTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            conferencecallTextField.Placeholder = "1928342-3";
            conferencecallTextField.TextColor = textFieldColor;
            conferencecallTextField.Font = textFieldFond;
            conferencecallTextField.TextAlignment = UITextAlignment.Left;
            conferencecallTextField.Tag = CONFERENCE_TAG;
            settingsView.Add (conferencecallTextField);

            yOffset = conferencecallTextField.Frame.Bottom + 15;
            float topSignatureCell = yOffset;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            yOffset += 17;

            UILabel signatureLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset, LABEL_WIDTH, LABEL_HEIGHT));
            signatureLabel.Font = A.Font_AvenirNextRegular14;
            signatureLabel.TextAlignment = UITextAlignment.Left;
            signatureLabel.TextColor = labelColor;
            signatureLabel.Text = "Signature";
            settingsView.Add (signatureLabel);

            UILabel displaySignatureLabel = new UILabel (new RectangleF (signatureLabel.Frame.Right + SPACER, yOffset, 171 - 15, LABEL_HEIGHT));
            displaySignatureLabel.TextColor = textFieldColor;
            displaySignatureLabel.Font = textFieldFond;
            displaySignatureLabel.TextAlignment = UITextAlignment.Left;
            displaySignatureLabel.Text = "Sent from Nacho Mail";
            displaySignatureLabel.Tag = SIGNATURE_TAG;
            settingsView.AddSubview (displaySignatureLabel);

            UIView signatureCellView = new UIView (new RectangleF (0, topSignatureCell, View.Frame.Width, settingsView.Frame.Height - topSignatureCell));
            signatureLabel.BackgroundColor = UIColor.Clear;
            signatureCellView.UserInteractionEnabled = true;

            UITapGestureRecognizer signatureTap = new UITapGestureRecognizer (() => {
                PerformSegue ("SegueToSignatureEdit", this);
            });
            signatureCellView.AddGestureRecognizer (signatureTap);

            settingsView.AddSubview (signatureCellView);

            UIImageView disclosureArrowImageView;
            using (var disclosureArrowIcon = UIImage.FromBundle ("gen-more-arrow")) {
                disclosureArrowImageView = new UIImageView (disclosureArrowIcon);
            }
            disclosureArrowImageView.Frame = new RectangleF (displaySignatureLabel.Frame.Right + 5, yOffset, disclosureArrowImageView.Frame.Width, disclosureArrowImageView.Frame.Height);
            settingsView.AddSubview (disclosureArrowImageView);
            View.Add (settingsView);

            yOffset = settingsView.Frame.Bottom + 20;

            UIView deleteAccountView = new UIView (new RectangleF (0, yOffset, View.Frame.Width, 44));
            deleteAccountView.BackgroundColor = UIColor.White;
            deleteAccountView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            deleteAccountView.Layer.BorderWidth = .5f;

            UIImageView deleteIconImage;
            using (var deleteIcon = UIImage.FromBundle ("email-delete-two")) {
                deleteIconImage = new UIImageView (deleteIcon);
            }
            deleteIconImage.Frame = new RectangleF (HORIZONTAL_PADDING, 8, deleteIconImage.Frame.Width, deleteIconImage.Frame.Height);
            deleteAccountView.AddSubview (deleteIconImage);

            UILabel deleteAccountLabel = new UILabel (new RectangleF (deleteIconImage.Frame.Right + 12, 12, 220, 20));
            deleteAccountLabel.Font = A.Font_AvenirNextMedium14;
            deleteAccountLabel.TextColor = A.Color_NachoBlack;
            deleteAccountLabel.TextAlignment = UITextAlignment.Left;
            deleteAccountLabel.Text = "Delete This Account";
            deleteAccountView.AddSubview (deleteAccountLabel);
            View.Add (deleteAccountView);

            UIView greyBackground = new UIView (new System.Drawing.RectangleF (0, 0, View.Frame.Width, View.Frame.Height));
            greyBackground.BackgroundColor = UIColor.DarkGray;
            greyBackground.Alpha = .4f;
            greyBackground.Tag = GREY_BACKGROUND_VIEW_TAG;
            greyBackground.Hidden = true;
            View.Add (greyBackground);

            //Used to extract the default blue system color
            UIButton y = new UIButton (UIButtonType.System);
            UIColor blue = y.CurrentTitleColor;

            UIView statusView = new UIView (new System.Drawing.RectangleF (View.Frame.Width / 6, View.Frame.Height / 2 - 150, View.Frame.Width * 2 / 3, 150));
            statusView.Tag = STATUS_VIEW_TAG;
            statusView.Layer.CornerRadius = 7.0f;
            statusView.BackgroundColor = UIColor.White;
            statusView.Alpha = 1.0f;
            statusView.Hidden = true;

            UITextView statusMessage = new UITextView (new System.Drawing.RectangleF (8, 2, statusView.Frame.Width - 16, statusView.Frame.Height / 2.4f));
            statusMessage.BackgroundColor = UIColor.White;
            statusMessage.Alpha = 1.0f;
            statusMessage.Font = UIFont.SystemFontOfSize (17);
            statusMessage.TextColor = UIColor.Black;
            statusMessage.Text = "Validating Credentials";
            statusMessage.TextAlignment = UITextAlignment.Center;
            statusView.AddSubview (statusMessage);

            UIActivityIndicatorView theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            theSpinner.Alpha = 1.0f;
            theSpinner.HidesWhenStopped = true;
            theSpinner.Frame = new System.Drawing.RectangleF (statusView.Frame.Width / 2 - 20, 50, 40, 40);
            theSpinner.Color = blue;
            theSpinner.StartAnimating ();

            statusView.AddSubview (theSpinner);

            UIView cancelLine = new UIView (new System.Drawing.RectangleF (0, 105, statusView.Frame.Width, .5f));
            cancelLine.BackgroundColor = UIColor.LightGray;
            statusView.AddSubview (cancelLine);

            UIButton cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, statusView.Frame.Width, 40));
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.White;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.SetTitleColor (blue, UIControlState.Normal);
            statusView.AddSubview (cancelValidation);

            UIAlertView userHitCancel = new UIAlertView ("Validation Cancelled", "Your settings have not been validated and therefore may not work correctly. Would you still like to save?", null, "Save", "Cancel");
            userHitCancel.Tag = CANCEL_VALIDATION_ALERT_VIEW_TAG;
            userHitCancel.Clicked += SaveAnywayClicked;

            cancelValidation.AddTarget (((object sender, EventArgs e) => {
                BackEnd.Instance.CancelValidateConfig (LoginHelpers.GetCurrentAccountId ());
                userHitCancel.Show ();
            }), UIControlEvent.TouchUpInside);

            statusView.AddSubview (cancelValidation);

            View.AddSubview (statusView);
        }

        protected override void ConfigureAndLayout ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McConference theConference = McConference.QueryByAccountId <McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

            var nameTextField = (UITextField)View.ViewWithTag (NAME_TAG);
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
            var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);
            var signatureLabel = (UILabel)View.ViewWithTag (SIGNATURE_TAG);

            if (!String.IsNullOrEmpty (theAccount.DisplayName)) {
                nameTextField.Text = theAccount.DisplayName;
            }

            if (!String.IsNullOrEmpty (theCred.Username)) {
                usernameTextField.Text = theCred.Username;
            }

            if (!String.IsNullOrEmpty (theCred.GetPassword ())) {
                passwordTextField.Text = theCred.GetPassword ();
            }

            if (!String.IsNullOrEmpty (theAccount.EmailAddr)) {
                emailTextField.Text = theAccount.EmailAddr;
            }

            if (!String.IsNullOrEmpty (theServer.Host)) {
                mailserverTextField.Text = theServer.Host;
            }

            if (null == theConference) {
                theConference = new McConference ();
                theConference.AccountId = LoginHelpers.GetCurrentAccountId ();
                theConference.DefaultPhoneNumber = "";
                theConference.Insert ();
            }

            if (!String.IsNullOrEmpty (theConference.DefaultPhoneNumber)) {
                conferenceTextField.Text = theConference.DefaultPhoneNumber;
            }

            if (!String.IsNullOrEmpty (theAccount.Signature)) {
                signatureLabel.Text = theAccount.Signature;
            }

            nameTextField.Enabled = textFieldsEditable;
            usernameTextField.Enabled = textFieldsEditable;
            passwordTextField.Enabled = textFieldsEditable;
            emailTextField.Enabled = textFieldsEditable;
            mailserverTextField.Enabled = textFieldsEditable;
            conferenceTextField.Enabled = textFieldsEditable;

            ColorTextFields ();
        }

        protected void ColorTextFields ()
        {
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);

            usernameTextField.TextColor = textFieldColor;
            passwordTextField.TextColor = textFieldColor;
            emailTextField.TextColor = textFieldColor;
            mailserverTextField.TextColor = textFieldColor;

            switch (accountIssue) {
            case AccountIssue.ErrorAuth:
            case AccountIssue.ErrorComm:
                usernameTextField.TextColor = A.Color_NachoRed;
                passwordTextField.TextColor = A.Color_NachoRed;
                break;
            case AccountIssue.ErrorUser:
                usernameTextField.TextColor = A.Color_NachoRed;
                break;
            case AccountIssue.InvalidHost:
                mailserverTextField.TextColor = A.Color_NachoRed;
                break;
            default:
                break;
            }
        }

        protected bool DidUserEditAccount ()
        {
            var nameTextField = (UITextField)View.ViewWithTag (NAME_TAG);
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
            var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);

            if (nameTextField.Text != ORIGINAL_ACCOUNT_NAME_VALUE) {
                return true;
            }
            if (usernameTextField.Text != ORIGINAL_USERNAME_VALUE) {
                return true;
            }
            if (passwordTextField.Text != ORIGINAL_PASSWORD_VALUE) {
                return true;
            }
            if (emailTextField.Text != ORIGINAL_EMAIL_VALUE) {
                return true;
            }
            if (mailserverTextField.Text != ORIGINAL_MAILSERVER_VALUE) {
                return true;
            }
            if (conferenceTextField.Text != ORIGINAL_CONFERENCE_VALUE) {
                return true;
            }

            return false;
        }

        protected override void Cleanup ()
        {
            cancelButton.Clicked -= CancelButtonClicked;
            editButton.Clicked -= EditButtonClicked;
            saveButton.Clicked -= SaveButtonClicked;

            cancelButton = null;
            editButton = null;
            saveButton = null;

            var cancelValidationAlertView = (UIAlertView)View.ViewWithTag (CANCEL_VALIDATION_ALERT_VIEW_TAG);
            if (null != cancelValidationAlertView) {
                cancelValidationAlertView.Clicked -= SaveAnywayClicked;
                cancelValidationAlertView = null;
            }

            var dismissChangesAlertView = (UIAlertView)View.ViewWithTag (DISMISS_CHANGES_ALERT_VIEW_TAG);
            if (null != dismissChangesAlertView) {
                dismissChangesAlertView.Clicked -= DismissChangesClicked;
                dismissChangesAlertView = null;
            }

            var errorAlertView = (UIAlertView)View.ViewWithTag (ERROR_ALERT_VIEW_TAG);
            if (null != errorAlertView) {
                errorAlertView.Clicked -= SaveAnywayClicked;
                errorAlertView = null;
            }

            var networkFailureAlertView = (UIAlertView)View.ViewWithTag (BAD_NETWORK_ALERT_VIEW_TAG);
            if (null != networkFailureAlertView) {
                networkFailureAlertView.Clicked -= SaveAnywayClicked;
                networkFailureAlertView = null;
            }
        }

        protected void ValidateAndDisplayWaitingView ()
        {
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);

            if (!NachoCore.Utils.RegexUtilities.IsValidHost (mailserverTextField.Text.Trim ())) {
                accountIssue = AccountIssue.InvalidHost;
                ConfigureAndLayout ();
                return;
            }

            McServer testServer = new McServer ();
            testServer.Host = mailserverTextField.Text;

            McCred testCred = new McCred ();
            testCred.SetTestPassword (passwordTextField.Text);
            testCred.Username = (usernameTextField.Text);

            if (!BackEnd.Instance.ValidateConfig (LoginHelpers.GetCurrentAccountId (), testServer, testCred)) {
                UIAlertView badNetworkConnection = new UIAlertView ("Network Error", "There is an issue with the network and we cannot validate your changes. Would you like to save anyway?", null, "Ok", "Cancel");
                badNetworkConnection.Tag = BAD_NETWORK_ALERT_VIEW_TAG;
                badNetworkConnection.Clicked += SaveAnywayClicked;
                badNetworkConnection.Show ();
            } else {
                ShowStatusView ();
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                accountIssue = AccountIssue.None;
                HideStatusView ();
                if (handleStatusEnums) {
                    HandleAccountIssue ();
                }
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorComm;
                HideStatusView ();
                if (handleStatusEnums) {
                    HandleAccountIssue ();
                }
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorAuth;
                HideStatusView ();
                if (handleStatusEnums) {
                    HandleAccountIssue ();
                }
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorUser;
                HideStatusView ();
                if (handleStatusEnums) {
                    HandleAccountIssue ();
                }
            }

        }

        protected void SaveButtonClicked (object sender, EventArgs e)
        {
            handleStatusEnums = true;
            ValidateAndDisplayWaitingView ();
        }

        protected void EditButtonClicked (object sender, EventArgs e)
        {
            ToggleEditing ();
        }

        protected void CancelButtonClicked (object sender, EventArgs e)
        {
            if (DidUserEditAccount ()) {
                UIAlertView dismissChanges = new UIAlertView ("Dismiss Changes", "If you leave this screen your changes will not be saved.", null, "Ok", "Cancel");
                dismissChanges.Tag = DISMISS_CHANGES_ALERT_VIEW_TAG;
                dismissChanges.Clicked += DismissChangesClicked;
                dismissChanges.Show ();
            } else {
                ToggleEditing ();
                DismissViewController (true, null);
            }
        }

        protected void CaptureOriginalSettings ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McConference theConference = McConference.QueryByAccountId<McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

            if (null != theAccount.DisplayName) {
                ORIGINAL_ACCOUNT_NAME_VALUE = theAccount.DisplayName;
            }
            if (null != theCred.Username) {
                ORIGINAL_USERNAME_VALUE = theCred.Username;
            }
            if (null != theCred.GetPassword ()) {
                ORIGINAL_PASSWORD_VALUE = theCred.GetPassword ();
            }
            if (null != theAccount.EmailAddr) {
                ORIGINAL_EMAIL_VALUE = theAccount.EmailAddr;
            }
            if (null != theServer.Host) {
                ORIGINAL_MAILSERVER_VALUE = theServer.Host;
            }
            if (null != theConference.DefaultPhoneNumber) {
                ORIGINAL_CONFERENCE_VALUE = theConference.DefaultPhoneNumber;
            }
        }

        protected void DismissChangesClicked (object sender, UIButtonEventArgs b)
        {
            if (b.ButtonIndex == 0) {
                ToggleEditing ();
                DismissViewController (true, null);
            }
        }

        protected void SaveAnywayClicked (object sender, UIButtonEventArgs b)
        {
            if (b.ButtonIndex == 0) {
                ToggleEditing ();
                SaveAccountSettings ();
                DismissViewController (true, null);
            }
        }

        protected void HandleAccountIssue ()
        {
            string alertViewHeader = "";
            string alertViewMessage = "";

            switch (accountIssue) {
            case AccountIssue.ErrorAuth:
                alertViewHeader = "Invalid Credentials";
                alertViewMessage = "User name or password is incorrect. No emails can be sent or recieved. Save anyway?";
                break;
            case AccountIssue.ErrorComm:
                alertViewHeader = "Validation Failed";
                alertViewMessage = "This account may not be able to send or receive emails. Save anyway?";
                break;
            case AccountIssue.ErrorUser:
                alertViewHeader = "Invalid Username";
                alertViewMessage = "User name is incorrect. No emails can be sent or received. Save anyway?";
                break;
            case AccountIssue.None:
                ToggleEditing ();
                SaveAccountSettings ();
                DismissViewController (true, null);
                return;
            default:
                break;
            }

            UIAlertView errorAlertView = new UIAlertView (alertViewHeader, alertViewMessage, null, "Save", "Cancel");
            errorAlertView.Tag = ERROR_ALERT_VIEW_TAG;
            errorAlertView.Clicked += SaveAnywayClicked;
            errorAlertView.Show ();
            handleStatusEnums = false;

            ColorTextFields ();
        }

        protected void ShowStatusView ()
        {
            UIView greyBackground = (UIView)View.ViewWithTag (GREY_BACKGROUND_VIEW_TAG); 
            UIView statusView = (UIView)View.ViewWithTag (STATUS_VIEW_TAG); 
            greyBackground.Hidden = false;
            statusView.Hidden = false;
        }

        protected void HideStatusView ()
        {
            UIView greyBackground = (UIView)View.ViewWithTag (GREY_BACKGROUND_VIEW_TAG);
            UIView statusView = (UIView)View.ViewWithTag (STATUS_VIEW_TAG);
            greyBackground.Hidden = true;
            statusView.Hidden = true;
        }

        protected void SaveAccountSettings ()
        {
            if (DidUserEditAccount ()) {
                var nameTextField = (UITextField)View.ViewWithTag (NAME_TAG);
                var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
                var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
                var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
                var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
                var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);

                McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
                McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
                McConference theConference = McConference.QueryByAccountId <McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

                theAccount.DisplayName = nameTextField.Text;
                theAccount.EmailAddr = emailTextField.Text;
                theServer.Host = mailserverTextField.Text;
                theCred.Username = usernameTextField.Text;
                theCred.UpdatePassword (passwordTextField.Text);
                theConference.DefaultPhoneNumber = conferenceTextField.Text;

                theAccount.Update ();
                theServer.Update ();
                theCred.Update ();
                theConference.Update ();
            }
        }

        protected void ToggleEditing ()
        {
            textFieldsEditable = !textFieldsEditable;
            if (textFieldsEditable) {
                cancelButton.Image = UIImage.FromBundle ("icn-close");
                saveButton.Title = "Done";
                NavigationItem.SetLeftBarButtonItem (cancelButton, true);
                NavigationItem.SetRightBarButtonItem (saveButton, true);
                UIView.Animate (1, () => {
                    NavigationItem.Title = "";
                });
            } else {
                editButton.Image = UIImage.FromBundle ("gen-edit");
                Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);
                NavigationItem.SetRightBarButtonItem (editButton, true);
                UIView.Animate (1, () => {
                    NavigationItem.Title = "Account Settings";
                });
            }
            ConfigureAndLayout ();
        }
    }
}
