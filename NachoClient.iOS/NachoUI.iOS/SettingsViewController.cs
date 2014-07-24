// This file has been autogenerated from a class added in the UI designer.

using System;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.Dialog;
using SWRevealViewControllerBinding;
using NachoCore.Utils;
using NachoCore;
using NachoCore.Model;
using System.Linq;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    public partial class SettingsViewController : NcDialogViewController
    {
        AccountSettings Account = new AccountSettings ();
        UIBarButtonItem doneButton;
        const int SPINNER_TAG = 109;
        UIView statusView;
        UIActivityIndicatorView theSpinner;
        UITextView statusMessage;
        UITextField accountNameText;
        UITextField userNameText;
        UITextField passwordText;
        UITextField emailText;
        UITextField mailServerText;
        UITextField conferenceCallText;
        UILabel signatureLabel;
        CustomTextInputElement Password;
        CustomTextInputElement UserName;
        UIColor userNameAndPasswordTextColor;
        List<UITextField> textFields;
        bool didLoad = false;

        public enum statusType
        {
            ErrorAuth,
            ErrorComm,
            ErrorUser,
            Success,
            Validating}
        ;

        public SettingsViewController (IntPtr handle) : base (handle)
        {
            doneButton = new UIBarButtonItem (UIBarButtonSystemItem.Done);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
            userNameAndPasswordTextColor = UIColor.Gray;
            NavigationItem.RightBarButtonItem = doneButton;

            configureTextFields ();

            // Navigation
            revealButton.Action = new MonoTouch.ObjCRuntime.Selector ("revealToggle:");
            revealButton.Target = this.RevealViewController ();

            // Multiple buttons on the left side
            NavigationItem.LeftBarButtonItems = new UIBarButtonItem[] { revealButton };

            //The intention is that when this method gets called you will pass in the account
            //That you are going to configure. We only have one account right now so we don't 
            //Have to worry about that. 
            LoadSettingsForSelectedAccount (null);

            var root = new RootElement ("Settings");
            if (!didLoad)
                root.Add (AddAccountSettings ());
            Root = root;

            didLoad = true;
            configureDoneButton ();
        }

        public void configureTextFields ()
        {
            accountNameText = new UITextField ();
            passwordText = new UITextField ();
            userNameText = new UITextField ();
            emailText = new UITextField ();
            mailServerText = new UITextField ();
            conferenceCallText = new UITextField ();

            textFields = new List<UITextField> () {
                accountNameText,
                passwordText,
                userNameText,
                emailText,
                mailServerText,
                conferenceCallText
            };

            foreach (var tf in textFields) {
                tf.AutocorrectionType = UITextAutocorrectionType.No;
                tf.AutocapitalizationType = UITextAutocapitalizationType.None;
            }
        }

        public void configureDoneButton ()
        {
            // When user clicks done: check, confirm, and save
            doneButton.Clicked += (object sender, EventArgs e) => {
                McAccount theAccount = McAccount.QueryById<McAccount> (Account.AccountId);
                McCred theCred = McCred.QueryById<McCred> (Account.McCredId);
                McServer theServer = McServer.QueryById<McServer> (Account.ServerId);
                McConference theConference = McConference.QueryById<McConference> (Account.PreferredConferenceId);

                Account.AccountName = accountNameText.Text;
                Account.UserName = userNameText.Text;
                Account.Password = passwordText.Text;
                Account.EmailAddress = emailText.Text; 
                Account.MailServer = mailServerText.Text;
                Account.ConferenceCallNumber = conferenceCallText.Text;

                theConference.DefaultPhoneNumber = Account.ConferenceCallNumber;
                theConference.Update ();
                theAccount.DisplayName = Account.AccountName;
                theAccount.Signature = Account.EmailSignature;
                theAccount.Update ();

                theCred.Username = Account.UserName;
                theCred.Password = Account.Password;
                theAccount.EmailAddr = Account.EmailAddress;
                theServer.Host = Account.MailServer;

                var didStart = BackEnd.Instance.ValidateConfig (theAccount.Id, theServer, theCred);

                if (!didStart) {
                    //TODO what happens when there's a network failure?
                    Console.WriteLine ("NETWORK FAILURE");
                } else {
                    configureStatusViewFor (statusType.Validating);
                }

                View.ResignFirstResponder ();
                ResignFirstResponder ();
                View.EndEditing (true);
            };
        }

        public void configureStatusViewForValidating ()
        {
            statusView = new UIView (new System.Drawing.RectangleF (View.Frame.Width / 6, 100 + TableView.ContentOffset.Y, View.Frame.Width * 2 / 3, 150));
            statusView.Layer.CornerRadius = 15.0f;
            statusView.BackgroundColor = UIColor.LightGray;
            statusView.Alpha = 1.0f;

            statusMessage = new UITextView (new System.Drawing.RectangleF (8, 2, statusView.Frame.Width - 16, statusView.Frame.Height / 2.4f));
            statusMessage.BackgroundColor = UIColor.LightGray;
            statusMessage.Alpha = .8f;
            statusMessage.Font = A.Font_AvenirNextRegular17;
            statusMessage.TextColor = UIColor.Black;
            statusMessage.Text = "Validating Credentials";
            statusMessage.TextAlignment = UITextAlignment.Center;
            statusView.AddSubview (statusMessage);

            theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            theSpinner.HidesWhenStopped = true;
            theSpinner.Tag = SPINNER_TAG;
            theSpinner.Frame = new System.Drawing.RectangleF (statusView.Frame.Width / 2 - 20, 50, 40, 40);
            theSpinner.StartAnimating ();
            statusView.AddSubview (theSpinner);

            UIView cancelLine = new UIView (new System.Drawing.RectangleF (0, 105, statusView.Frame.Width, .5f));
            cancelLine.BackgroundColor = UIColor.Gray;
            statusView.AddSubview (cancelLine);

            UIButton cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, statusView.Frame.Width, 40));
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.LightGray;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.TitleLabel.TextColor = UIColor.Blue;

            cancelValidation.AddTarget (((object sender, EventArgs e) => {
                dismissStatusView ();

            }), UIControlEvent.TouchUpInside);

            statusView.AddSubview (cancelValidation);

            View.AddSubview (statusView);
        }

        public void saveSettings ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (Account.AccountId);
            McCred theCred = McCred.QueryById<McCred> (Account.McCredId);
            McServer theServer = McServer.QueryById<McServer> (Account.ServerId);

            theCred.Username = Account.UserName;
            theCred.Password = Account.Password;
            theAccount.EmailAddr = Account.EmailAddress;
            theServer.Host = Account.MailServer;

            theCred.Update ();
            theAccount.Update ();
            theServer.Update ();
        }

        public void configureStatusViewFor (statusType whatType)
        {
            string header = "";
            string message = "";
            bool isSuccess = false;
            bool includeButtons = false;

            switch (whatType) {
            case statusType.ErrorAuth:
                header = "Invalid Credentials";
                message = "User name or password is incorrect. No emails can be sent or recieved. Still Continue?";
                includeButtons = true;
                break;
            case statusType.ErrorComm:
                header = "Validation Failed";
                message = "This account may not be able to send or receive emails. Are you sure you want to continue?";
                includeButtons = true;
                break;
            case statusType.ErrorUser:
                header = "Invalid Credentials";
                message = "User name or password is incorrect. No emails can be sent or recieved. Still Continue?";
                includeButtons = true;
                break;
            case statusType.Success:
                isSuccess = true;
                break;
            case statusType.Validating:
                configureStatusViewForValidating ();
                break;
            default:
                break;
            }

            if (isSuccess) {
                saveSettings ();
            }

            if (includeButtons) {
                UIAlertView errorView = new UIAlertView (header, message, null, "Save", "Cancel");
                errorView.Clicked += (object sender, UIButtonEventArgs e) => {
                    if (e.ButtonIndex == 0) {
                        saveSettings ();
                    }
                };
                errorView.Show ();
            }
        }

        public void dismissStatusView ()
        {
            if (statusView.Subviews.Length > 0) {

                while (statusView.Subviews.Length != 0) {
                    UIView x = statusView.Subviews [0];
                    x.RemoveFromSuperview ();
                }
            }
            statusView.Frame = new System.Drawing.RectangleF (0, 0, 0, 0);
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            StopSpinner ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                UpdateAccountCredentials ();
                StopSpinner ();
                dismissStatusView ();
                configureStatusViewFor (statusType.Success);
                userNameAndPasswordTextColor = UIColor.Gray;
                passwordText.TextColor = userNameAndPasswordTextColor;
                userNameText.TextColor = userNameAndPasswordTextColor;
                return;
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                StopSpinner ();
                dismissStatusView ();
                configureStatusViewFor (statusType.ErrorComm);
                userNameAndPasswordTextColor = UIColor.Gray;
                userNameText.TextColor = userNameAndPasswordTextColor;
                passwordText.TextColor = userNameAndPasswordTextColor;
                return;
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                StopSpinner ();
                dismissStatusView ();
                configureStatusViewFor (statusType.ErrorAuth);
                userNameAndPasswordTextColor = A.Color_NachoRed;
                passwordText.TextColor = userNameAndPasswordTextColor;
                userNameText.TextColor = userNameAndPasswordTextColor; 
                return;
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                StopSpinner ();
                dismissStatusView ();
                configureStatusViewFor (statusType.ErrorAuth);
                userNameAndPasswordTextColor = A.Color_NachoRed;
                passwordText.TextColor = userNameAndPasswordTextColor;
                userNameText.TextColor = userNameAndPasswordTextColor; 
                return;
            }
        }

        public void UpdateAccountCredentials ()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (Account.AccountId);
            McCred theCred = McCred.QueryById<McCred> (Account.McCredId);
            McServer theServer = McServer.QueryById<McServer> (Account.ServerId);

            theCred.Username = Account.UserName;
            theCred.Password = Account.Password;
            theAccount.EmailAddr = Account.EmailAddress;
            theServer.Host = Account.MailServer;
        }

        public Section AddAccountSettings ()
        {
            var AccountSection = new Section ("ACCOUNT");

            var AccountName = new CustomTextInputElement (UIImage.FromBundle (""), "Account Name", Account.AccountName, accountNameText);
            accountNameText.ShouldReturn += (textField) => {
                //Account.AccountName = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };

            accountNameText.EditingDidEndOnExit += (object sender, EventArgs e) => {
                accountNameText.ResignFirstResponder ();
            };

            accountNameText.EditingDidEnd += (object sender, EventArgs e) => {
                accountNameText.ResignFirstResponder ();
            };

            AccountSection.Add (AccountName);

            UserName = new CustomTextInputElement (UIImage.FromBundle (""), "User Name", Account.UserName, userNameText);
            userNameText.ShouldReturn += (textField) => {
                //Account.UserName = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };
                
            userNameText.EditingDidBegin += (object sender, EventArgs e) => {
                userNameText.TextColor = userNameAndPasswordTextColor;
            };

            AccountSection.Add (UserName);

            Password = new CustomTextInputElement (UIImage.FromBundle (""), "Password", Account.Password, passwordText);
            passwordText.SecureTextEntry = true;
            passwordText.ShouldReturn += (textField) => {
                Account.Password = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };

            passwordText.EditingDidEnd += (object sender, EventArgs e) => {
                passwordText.TextColor = userNameAndPasswordTextColor;
            };

            AccountSection.Add (Password);

            var EmailAddress = new CustomTextInputElement (UIImage.FromBundle (""), "Email Address", Account.EmailAddress, emailText);
            emailText.ShouldReturn += (textField) => {
                Account.EmailAddress = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };
            AccountSection.Add (EmailAddress);

            var MailServer = new CustomTextInputElement (UIImage.FromBundle (""), "Mail Server", Account.MailServer, mailServerText);
            mailServerText.ShouldReturn += (textField) => {
                //Account.MailServer = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };
            AccountSection.Add (MailServer);

            var ConferenceCall = new CustomTextInputElement (UIImage.FromBundle (""), "Conference Call #", Account.ConferenceCallNumber, conferenceCallText);
            conferenceCallText.ShouldReturn += (textField) => { 
                Account.ConferenceCallNumber = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };
            AccountSection.Add (ConferenceCall);
               
            signatureLabel = new UILabel ();
            signatureLabel.Text = Account.EmailSignature;
            var SettingsSignatureElement = new SignatureEntryElement ("Email Signature", signatureLabel);
            SettingsSignatureElement.Tapped += () => {
                PushSignatureView ();
            };
            AccountSection.Add (SettingsSignatureElement);
            return AccountSection;
        }

        public void PushSignatureView ()
        {
            var root = new RootElement ("Signature");

            UITextView signatureEditTextView = new UITextView (new System.Drawing.RectangleF (0, 0, 320, 100));
            signatureEditTextView.Editable = true;

            signatureEditTextView.ShouldEndEditing += (test) => {
                Account.EmailSignature = test.Text;
                signatureLabel.Text = test.Text;
                return true;
            };

            signatureEditTextView.Changed += (object sender, EventArgs e) => {
                SelectionChanged (signatureEditTextView);
            };

            var thinSec = new ThinSection ();
            var signatureEditingElement = new StyledMultiLineTextInput ("", Account.EmailSignature, signatureEditTextView);
            thinSec.Add (signatureEditingElement);
            root.Add (thinSec);

            var signatureEditingViewController = new DialogViewController (root, true);
            NavigationController.PushViewController (signatureEditingViewController, true);
        }

        public void SelectionChanged (UITextView textView)
        {
            var caretRect = textView.GetCaretRectForPosition (textView.SelectedTextRange.end);
            caretRect.Size = new System.Drawing.SizeF (caretRect.Size.Width, caretRect.Size.Height + textView.TextContainerInset.Bottom);
            var frame = textView.Frame;
            frame.Size = new System.Drawing.SizeF (textView.ContentSize.Width, textView.ContentSize.Height + 40);
            textView.Frame = frame;
            caretRect.Y += textView.Frame.Y;
        }

        void EditAccount ()
        {
            Log.Info (Log.LOG_UI, "Edit account");
            var editViewController = new EditAccountViewController (null);
            NavigationController.PushViewController (editViewController, true);
        }

        public void LoadSettingsForSelectedAccount (McAccount whatAccount)
        {
            if (null == whatAccount) {
                whatAccount = NcModel.Instance.Db.Table<McAccount> ().First ();
            }

            McCred userCredentials = McCred.QueryById<McCred> (whatAccount.CredId);
            McServer userMailServer = McServer.QueryById<McServer> (whatAccount.ServerId);
            McConference userConference = McConference.QueryById<McConference> (whatAccount.PreferredConferenceId);
            Account.AccountId = whatAccount.Id;
            Account.McCredId = userCredentials.Id;
            Account.ServerId = userMailServer.Id;

            if (null != userConference) {
                Account.PreferredConferenceId = userConference.Id;
            } else {
                McConference x = new McConference ();
                x.DefaultPhoneNumber = "";
                x.Insert ();
                whatAccount.PreferredConferenceId = x.Id;
                whatAccount.Update ();
                Account.PreferredConferenceId = x.Id;
                userConference = x;
            }

            Account.AccountName = whatAccount.DisplayName == null ? "Exchange" : whatAccount.DisplayName;
            Account.UserName = userCredentials.Username;
            Account.Password = userCredentials.Password;
            Account.EmailAddress = whatAccount.EmailAddr;
            Account.MailServer = userMailServer.Host;
            Account.EmailSignature = whatAccount.Signature == null ? "Sent from NachoMail" : whatAccount.Signature;
            Account.ConferenceCallNumber = userConference.DefaultPhoneNumber == null ? "" : userConference.DefaultPhoneNumber;
        }

        public class AccountSettings
        {
            public string AccountName { get; set; }

            public string UserName { get; set; }

            public string Password { get; set; }

            public string EmailAddress { get; set; }

            public string MailServer { get; set; }

            public string DaysToSyncMail { get; set; }

            public string DaysToSyncCalendar { get; set; }

            public string EmailSignature { get; set; }

            public string ConferenceCallNumber { get; set; }

            public int AccountId { get; set; }

            public int McCredId { get; set; }

            public int ServerId { get; set; }

            public int PreferredConferenceId { get; set; }

            public AccountSettings ()
            {

            }
        }

        protected void StartSpinner ()
        {
            var spinner = View.ViewWithTag (SPINNER_TAG) as UIActivityIndicatorView;
            spinner.StartAnimating ();
        }

        protected void StopSpinner ()
        {
            theSpinner.StopAnimating ();
        }

        void Kickstart ()
        {
            Log.Info (Log.LOG_UI, "Kickstart pressed");
            // TODO: Kickstart
        }

        void Reset ()
        {
            Log.Info (Log.LOG_UI, "Reset pressed");
            // TODO: Reset
        }
    }
}
