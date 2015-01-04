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
        protected UIView contentView;
        protected UIScrollView scrollView;

        protected UIBarButtonItem editButton;
        protected UIBarButtonItem cancelButton;
        protected UIBarButtonItem saveButton;
        protected UIBarButtonItem backButton;

        protected bool textFieldsEditable = false;

        protected const float HORIZONTAL_PADDING = 25f;
        protected const float LABEL_WIDTH = 90f;
        protected const float SPACER = 15f;
        protected const float LABEL_VERTICAL_SPACER = 17f;
        protected const float LABEL_HEIGHT = 17f;
        protected const float TEXTFIELD_HEIGHT = 50f;

        protected readonly UIColor LABEL_TEXT_COLOR = A.Color_NachoDarkText;
        protected readonly UIColor TEXT_FIELD_TEXT_COLOR = A.Color_NachoGreen;
        protected readonly UIFont TEXT_FIELD_FONT = A.Font_AvenirNextMedium14;

        protected const int ACCOUNT_NAME_TAG = 100;
        protected const int USERNAME_TAG = 101;
        protected const int PASSWORD_TAG = 102;
        protected const int EMAIL_TAG = 103;
        protected const int MAILSERVER_TAG = 104;
        protected const int CONFERENCE_TAG = 105;
        protected const int SIGNATURE_TAG = 106;

        protected string originalAccountNameValue = "";
        protected string originalUsernameValue = "";
        protected string originalPasswordValue = "";
        protected string originalEmailValue = "";
        protected string originalMailServerValue = "";
        protected string originalConferenceValue = "";
        protected string originalSignatureValue = "";

        protected const int GREY_BACKGROUND_VIEW_TAG = 200;
        protected const int STATUS_VIEW_TAG = 201;

        protected const int CANCEL_VALIDATION_BUTTON_TAG = 304;
        protected const int SIGNATURE_VIEW_TAG = 305;

        protected bool handleStatusEnums = true;

        protected UITapGestureRecognizer signatureTapGesture;
        protected UITapGestureRecognizer.Token signatureTapGestureHandlerToken;

        protected float yOffset;
        protected float keyboardHeight;

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
        }

        public override void ViewDidAppear (bool animated)
        {
            CaptureOriginalSettings ();
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
            base.ViewDidAppear (animated);
        }

        public override void ViewWillDisappear (bool animated)
        {
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
            View.EndEditing (true);
            base.ViewWillDisappear (animated);
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
        }

        protected override void CreateViewHierarchy ()
        {
            NavigationController.NavigationBar.Translucent = false;
            NavigationItem.Title = "Account Settings";

            editButton = new UIBarButtonItem ();
            cancelButton = new UIBarButtonItem ();
            backButton = new UIBarButtonItem ();
            saveButton = new UIBarButtonItem ();

            editButton.Image = UIImage.FromBundle ("gen-edit");
            cancelButton.Image = UIImage.FromBundle ("icn-close");
            backButton.Image = UIImage.FromBundle ("nav-backarrow");
            saveButton.Title = "Done";

            backButton.TintColor = A.Color_NachoBlue;

            NavigationItem.SetLeftBarButtonItem (backButton, true);
            NavigationItem.SetRightBarButtonItem (editButton, true); 

            editButton.Clicked += EditButtonClicked;
            saveButton.Clicked += SaveButtonClicked;
            cancelButton.Clicked += CancelButtonClicked;
            backButton.Clicked += BackButtonClicked;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            scrollView = new UIScrollView (new RectangleF (0, 0, View.Frame.Width, View.Frame.Height));
            scrollView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.ScrollEnabled = true;
            scrollView.AlwaysBounceVertical = true;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
            View.AddSubview (scrollView);

            contentView = new UIView (new RectangleF (0, 0, View.Frame.Width, View.Frame.Height));
            contentView.BackgroundColor = A.Color_NachoBackgroundGray;
            scrollView.AddSubview (contentView);

            UIView settingsView = new UIView (new RectangleF (0, 20, View.Frame.Width, TEXTFIELD_HEIGHT * 7));
            settingsView.BackgroundColor = UIColor.White;
            settingsView.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
            settingsView.Layer.BorderWidth = .5f;

            yOffset = 0;

            UILabel nameLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            nameLabel.Font = A.Font_AvenirNextRegular14;
            nameLabel.TextAlignment = UITextAlignment.Left;
            nameLabel.TextColor = LABEL_TEXT_COLOR;
            nameLabel.Text = "Name";
            settingsView.Add (nameLabel);

            UITextField accountNameTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            accountNameTextField.Placeholder = "Exchange";
            accountNameTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            accountNameTextField.Font = TEXT_FIELD_FONT;
            accountNameTextField.TextAlignment = UITextAlignment.Left;
            accountNameTextField.Tag = ACCOUNT_NAME_TAG;
            settingsView.Add (accountNameTextField);

            yOffset = accountNameTextField.Frame.Bottom;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel usernameLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            usernameLabel.Font = A.Font_AvenirNextRegular14;
            usernameLabel.TextAlignment = UITextAlignment.Left;
            usernameLabel.TextColor = LABEL_TEXT_COLOR;
            usernameLabel.Text = "Username";
            settingsView.Add (usernameLabel);

            UITextField usernameTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            usernameTextField.Placeholder = "username";
            usernameTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            usernameTextField.Font = TEXT_FIELD_FONT;
            usernameTextField.TextAlignment = UITextAlignment.Left;
            usernameTextField.Tag = USERNAME_TAG;
            settingsView.Add (usernameTextField);

            yOffset = usernameTextField.Frame.Bottom;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel passwordLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            passwordLabel.Font = A.Font_AvenirNextRegular14;
            passwordLabel.TextAlignment = UITextAlignment.Left;
            passwordLabel.TextColor = LABEL_TEXT_COLOR;
            passwordLabel.Text = "Password";
            settingsView.Add (passwordLabel);

            UITextField passwordTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            passwordTextField.Placeholder = "********";
            passwordTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            passwordTextField.Font = TEXT_FIELD_FONT;
            passwordTextField.TextAlignment = UITextAlignment.Left;
            passwordTextField.SecureTextEntry = true;
            passwordTextField.Tag = PASSWORD_TAG;
            settingsView.Add (passwordTextField);

            yOffset = passwordTextField.Frame.Bottom;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel emailLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            emailLabel.Font = A.Font_AvenirNextRegular14;
            emailLabel.TextAlignment = UITextAlignment.Left;
            emailLabel.TextColor = LABEL_TEXT_COLOR;
            emailLabel.Text = "Email";
            settingsView.Add (emailLabel);

            UITextField emailTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            emailTextField.Placeholder = "zachq@nachocove.com";
            emailTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            emailTextField.Font = TEXT_FIELD_FONT;
            emailTextField.TextAlignment = UITextAlignment.Left;
            emailTextField.Tag = EMAIL_TAG;
            settingsView.Add (emailTextField);

            yOffset = emailTextField.Frame.Bottom;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel mailserverLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            mailserverLabel.Font = A.Font_AvenirNextRegular14;
            mailserverLabel.TextAlignment = UITextAlignment.Left;
            mailserverLabel.TextColor = LABEL_TEXT_COLOR;
            mailserverLabel.Text = "Mail Server";
            settingsView.Add (mailserverLabel);

            UITextField mailserverTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            mailserverTextField.Placeholder = "outlook.office365.com";
            mailserverTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            mailserverTextField.Font = TEXT_FIELD_FONT;
            mailserverTextField.TextAlignment = UITextAlignment.Left;
            mailserverTextField.Tag = MAILSERVER_TAG;
            settingsView.Add (mailserverTextField);

            yOffset = mailserverTextField.Frame.Bottom;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel conferencecallLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + 3f, LABEL_WIDTH, LABEL_HEIGHT + 25));

            conferencecallLabel.Font = A.Font_AvenirNextRegular14;
            conferencecallLabel.TextAlignment = UITextAlignment.Left;
            conferencecallLabel.TextColor = LABEL_TEXT_COLOR;
            conferencecallLabel.Text = "Conference Call Number";
            conferencecallLabel.Lines = 2;
            conferencecallLabel.LineBreakMode = UILineBreakMode.WordWrap;

            settingsView.Add (conferencecallLabel);

            UITextField conferencecallTextField = new UITextField (new RectangleF (nameLabel.Frame.Right + SPACER, yOffset, 171, TEXTFIELD_HEIGHT));
            conferencecallTextField.Placeholder = "1928342-3";
            conferencecallTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            conferencecallTextField.Font = TEXT_FIELD_FONT;
            conferencecallTextField.TextAlignment = UITextAlignment.Left;
            conferencecallTextField.Tag = CONFERENCE_TAG;
            settingsView.Add (conferencecallTextField);

            yOffset = conferencecallTextField.Frame.Bottom;
            float topSignatureCell = yOffset;

            Util.AddHorizontalLine (HORIZONTAL_PADDING, yOffset, settingsView.Frame.Width - HORIZONTAL_PADDING, A.Color_NachoBorderGray, settingsView);

            UILabel signatureLabel = new UILabel (new RectangleF (HORIZONTAL_PADDING, yOffset + LABEL_VERTICAL_SPACER, LABEL_WIDTH, LABEL_HEIGHT));
            signatureLabel.Font = A.Font_AvenirNextRegular14;
            signatureLabel.TextAlignment = UITextAlignment.Left;
            signatureLabel.TextColor = LABEL_TEXT_COLOR;
            signatureLabel.Text = "Signature";
            settingsView.Add (signatureLabel);

            UILabel displaySignatureLabel = new UILabel (new RectangleF (signatureLabel.Frame.Right + SPACER, yOffset + LABEL_VERTICAL_SPACER, 171 - 15, LABEL_HEIGHT));
            displaySignatureLabel.TextColor = TEXT_FIELD_TEXT_COLOR;
            displaySignatureLabel.Font = TEXT_FIELD_FONT;
            displaySignatureLabel.TextAlignment = UITextAlignment.Left;
            displaySignatureLabel.Text = "Sent from Nacho Mail";
            displaySignatureLabel.Tag = SIGNATURE_TAG;
            settingsView.AddSubview (displaySignatureLabel);

            UIView signatureCellView = new UIView (new RectangleF (0, topSignatureCell, View.Frame.Width, settingsView.Frame.Height - topSignatureCell));
            signatureCellView.BackgroundColor = UIColor.Clear;
            signatureCellView.UserInteractionEnabled = true;
            signatureCellView.Tag = SIGNATURE_VIEW_TAG;

            signatureTapGesture = new UITapGestureRecognizer ();
            signatureTapGestureHandlerToken = signatureTapGesture.AddTarget (SignatureTapHandler);
            signatureCellView.AddGestureRecognizer (signatureTapGesture);
            settingsView.AddSubview (signatureCellView);

            UIImageView disclosureArrowImageView;
            using (var disclosureArrowIcon = UIImage.FromBundle ("gen-more-arrow")) {
                disclosureArrowImageView = new UIImageView (disclosureArrowIcon);
            }
            disclosureArrowImageView.Frame = new RectangleF (displaySignatureLabel.Frame.Right + 5, yOffset + LABEL_VERTICAL_SPACER, disclosureArrowImageView.Frame.Width, disclosureArrowImageView.Frame.Height);
            settingsView.AddSubview (disclosureArrowImageView);
            contentView.AddSubview (settingsView);

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
            contentView.Add (deleteAccountView);

            yOffset = deleteAccountView.Frame.Bottom + A.Card_Vertical_Indent;

            UIView greyBackground = new UIView (new System.Drawing.RectangleF (0, 0, View.Frame.Width, View.Frame.Height));
            greyBackground.BackgroundColor = UIColor.DarkGray;
            greyBackground.Alpha = .4f;
            greyBackground.Tag = GREY_BACKGROUND_VIEW_TAG;
            greyBackground.Hidden = true;
            View.Add (greyBackground);

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
            theSpinner.Color = A.Color_SystemBlue;
            theSpinner.StartAnimating ();

            statusView.AddSubview (theSpinner);

            UIView cancelLine = new UIView (new System.Drawing.RectangleF (0, 105, statusView.Frame.Width, .5f));
            cancelLine.BackgroundColor = A.Color_NachoLightBorderGray;
            statusView.AddSubview (cancelLine);

            UIButton cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, statusView.Frame.Width, 40));
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.White;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.SetTitleColor (A.Color_SystemBlue, UIControlState.Normal);
            cancelValidation.Tag = CANCEL_VALIDATION_BUTTON_TAG;
            statusView.AddSubview (cancelValidation);

            cancelValidation.TouchUpInside += CancelValidationButtonClicked;

            statusView.AddSubview (cancelValidation);
            View.AddSubview (statusView);
        }

        protected override void ConfigureAndLayout ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McConference theConference = McConference.QueryByAccountId <McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

            var accountNameTextField = (UITextField)View.ViewWithTag (ACCOUNT_NAME_TAG);
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
            var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);
            var signatureLabel = (UILabel)View.ViewWithTag (SIGNATURE_TAG);

            if (!String.IsNullOrEmpty (theAccount.DisplayName)) {
                accountNameTextField.Text = theAccount.DisplayName;
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

            accountNameTextField.Enabled = textFieldsEditable;
            usernameTextField.Enabled = textFieldsEditable;
            passwordTextField.Enabled = textFieldsEditable;
            emailTextField.Enabled = textFieldsEditable;
            mailserverTextField.Enabled = textFieldsEditable;
            conferenceTextField.Enabled = textFieldsEditable;

            ColorTextFields ();

            LayoutView ();
        }

        protected void LayoutView()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            contentView.Frame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            scrollView.ContentSize = contentView.Frame.Size;
        }

        protected void ColorTextFields ()
        {
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);

            usernameTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            passwordTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            emailTextField.TextColor = TEXT_FIELD_TEXT_COLOR;
            mailserverTextField.TextColor = TEXT_FIELD_TEXT_COLOR;

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
            var accountNameTextField = (UITextField)View.ViewWithTag (ACCOUNT_NAME_TAG);
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
            var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);

            if (accountNameTextField.Text != originalAccountNameValue) {
                return true;
            }
            if (usernameTextField.Text != originalUsernameValue) {
                return true;
            }
            if (passwordTextField.Text != originalPasswordValue) {
                return true;
            }
            if (emailTextField.Text != originalEmailValue) {
                return true;
            }
            if (mailserverTextField.Text != originalMailServerValue) {
                return true;
            }
            if (conferenceTextField.Text != originalConferenceValue) {
                return true;
            }

            return false;
        }

        protected override void Cleanup ()
        {
            cancelButton.Clicked -= CancelButtonClicked;
            editButton.Clicked -= EditButtonClicked;
            saveButton.Clicked -= SaveButtonClicked;
            backButton.Clicked -= BackButtonClicked;

            cancelButton = null;
            editButton = null;
            saveButton = null;
            backButton = null;

            var cancelValidationButton = (UIButton)View.ViewWithTag (CANCEL_VALIDATION_BUTTON_TAG);
            if (null != cancelValidationButton) {
                cancelValidationButton.TouchUpInside -= CancelValidationButtonClicked;
                cancelValidationButton = null;
            }

            signatureTapGesture.RemoveTarget (signatureTapGestureHandlerToken);
            var signatureView = (UIView)View.ViewWithTag (SIGNATURE_VIEW_TAG);
            if (null != signatureView) {
                signatureView.RemoveGestureRecognizer (signatureTapGesture);
            }
        }

        protected void ValidateAndDisplayWaitingView ()
        {
            var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
            var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
            var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);

            if (!NachoCore.Utils.RegexUtilities.IsValidHostName (mailserverTextField.Text.Trim ())) {
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
                var badNetworkConnection = new UIAlertView ("Network Error", "There is an issue with the network and we cannot validate your changes. Would you like to save anyway?", null, "Ok", "Cancel");
                badNetworkConnection.Clicked += SaveAnywayClicked;
                badNetworkConnection.Show ();
            } else {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                ShowStatusView ();
            }
        }

        public void StatusIndicatorTriggered ()
        {
            HideStatusView ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            if (handleStatusEnums) {
                HandleAccountIssue ();
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                accountIssue = AccountIssue.None;
                StatusIndicatorTriggered ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorComm;
                StatusIndicatorTriggered ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorAuth;
                StatusIndicatorTriggered ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                accountIssue = AccountIssue.ErrorUser;
                StatusIndicatorTriggered ();
            }
        }

        protected void SaveButtonClicked (object sender, EventArgs e)
        {
            View.EndEditing (true);
            handleStatusEnums = true;
            ValidateAndDisplayWaitingView ();
        }

        protected void EditButtonClicked (object sender, EventArgs e)
        {
            ToggleEditing ();
        }

        protected void BackButtonClicked (object sender, EventArgs e)
        {
            NavigationController.PopViewControllerAnimated (true);
        }

        protected void CancelButtonClicked (object sender, EventArgs e)
        {
            if (DidUserEditAccount ()) {
                var dismissChanges = new UIAlertView ("Dismiss Changes", "If you leave this screen your changes will not be saved.", null, "Ok", "Cancel");
                dismissChanges.Clicked += DismissChangesClicked;
                dismissChanges.Show ();
            } else {
                ToggleEditing ();
            }
        }

        protected void CancelValidationButtonClicked (object sender, EventArgs e)
        {
            BackEnd.Instance.CancelValidateConfig (LoginHelpers.GetCurrentAccountId ());

            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
            HideStatusView ();

            var cancelledValidationAlertView = new UIAlertView ("Validation Cancelled", "Your settings have not been validated and therefore may not work correctly. Would you still like to save?", null, "Save", "Cancel");
            cancelledValidationAlertView.Clicked += SaveAnywayClicked;
            cancelledValidationAlertView.Show ();

        }

        protected void CaptureOriginalSettings ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            McConference theConference = McConference.QueryByAccountId<McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

            if (null != theAccount.DisplayName) {
                originalAccountNameValue = theAccount.DisplayName;
            }
            if (null != theCred.Username) {
                originalUsernameValue = theCred.Username;
            }
            if (null != theCred.GetPassword ()) {
                originalPasswordValue = theCred.GetPassword ();
            }
            if (null != theAccount.EmailAddr) {
                originalEmailValue = theAccount.EmailAddr;
            }
            if (null != theServer.Host) {
                originalMailServerValue = theServer.Host;
            }
            if (null != theConference.DefaultPhoneNumber) {
                originalConferenceValue = theConference.DefaultPhoneNumber;
            }
        }

        protected void DismissChangesClicked (object sender, UIButtonEventArgs b)
        {
            if (b.ButtonIndex == 0) {
                ToggleEditing ();
            }
            var me = (UIAlertView)sender;
            me.Clicked -= DismissChangesClicked;
        }

        protected void SaveAnywayClicked (object sender, UIButtonEventArgs b)
        {
            if (b.ButtonIndex == 0) {
                SaveAccountSettings ();
                ToggleEditing ();
            }
            var me = (UIAlertView)sender;
            me.Clicked -= SaveAnywayClicked;
        }

        protected void SignatureTapHandler (NSObject sender)
        {
            var gesture = sender as UIGestureRecognizer;
            if (null != gesture) {
                PerformSegue ("SegueToSignatureEdit", this);
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
                SaveAccountSettings ();
                ToggleEditing ();
                return;
            default:
                break;
            }

            var errorAlertView = new UIAlertView (alertViewHeader, alertViewMessage, null, "Save", "Cancel");
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
            View.EndEditing (true);
            if (DidUserEditAccount ()) {
                var accountNameTextField = (UITextField)View.ViewWithTag (ACCOUNT_NAME_TAG);
                var usernameTextField = (UITextField)View.ViewWithTag (USERNAME_TAG);
                var passwordTextField = (UITextField)View.ViewWithTag (PASSWORD_TAG);
                var emailTextField = (UITextField)View.ViewWithTag (EMAIL_TAG);
                var mailserverTextField = (UITextField)View.ViewWithTag (MAILSERVER_TAG);
                var conferenceTextField = (UITextField)View.ViewWithTag (CONFERENCE_TAG);

                McAccount theAccount = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                McServer theServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
                McCred theCred = McCred.QueryByAccountId<McCred> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
                McConference theConference = McConference.QueryByAccountId <McConference> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();

                theAccount.DisplayName = accountNameTextField.Text;
                theAccount.EmailAddr = emailTextField.Text;
                theServer.Host = mailserverTextField.Text;
                theCred.Username = usernameTextField.Text;
                theCred.UpdatePassword (passwordTextField.Text);
                theConference.DefaultPhoneNumber = conferenceTextField.Text;

                theAccount.Update ();
                theServer.Update ();
                theCred.Update ();
                theConference.Update ();

                NachoTabBarController.ReconfigureMoreTab ();
            }
        }

        protected void ToggleEditing ()
        {
            textFieldsEditable = !textFieldsEditable;
            if (textFieldsEditable) {
                NavigationItem.SetLeftBarButtonItem (cancelButton, true);
                NavigationItem.SetRightBarButtonItem (saveButton, true);
                UIView.Animate (1, () => {
                    NavigationItem.Title = "Edit Account";
                });
            } else {
                NavigationItem.SetLeftBarButtonItem (backButton, true);
                NavigationItem.SetRightBarButtonItem (editButton, true);
                UIView.Animate (1, () => {
                    NavigationItem.Title = "Account Settings";
                });
            }
            ConfigureAndLayout ();
        }

        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            LayoutView ();
        }

        public virtual bool HandlesKeyboardNotifications {
            get { return true; }
        }

        private void OnKeyboardNotification (NSNotification notification)
        {
            if (IsViewLoaded) {
                //Check if the keyboard is becoming visible
                bool visible = notification.Name == UIKeyboard.WillShowNotification;
                //Start an animation, using values from the keyboard
                UIView.BeginAnimations ("AnimateForKeyboard");
                UIView.SetAnimationBeginsFromCurrentState (true);
                UIView.SetAnimationDuration (UIKeyboard.AnimationDurationFromNotification (notification));
                UIView.SetAnimationCurve ((UIViewAnimationCurve)UIKeyboard.AnimationCurveFromNotification (notification));
                //Pass the notification, calculating keyboard height, etc.
                bool landscape = InterfaceOrientation == UIInterfaceOrientation.LandscapeLeft || InterfaceOrientation == UIInterfaceOrientation.LandscapeRight;
                if (visible) {
                    var keyboardFrame = UIKeyboard.FrameEndFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                } else {
                    var keyboardFrame = UIKeyboard.FrameBeginFromNotification (notification);
                    OnKeyboardChanged (visible, landscape ? keyboardFrame.Width : keyboardFrame.Height);
                }
                //Commit the animation
                UIView.CommitAnimations (); 
            }
        }
    }
}
