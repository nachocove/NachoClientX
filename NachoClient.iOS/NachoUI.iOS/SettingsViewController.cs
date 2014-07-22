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
        AccountSettings Account = new AccountSettings();
        UIBarButtonItem doneButton;
        const int SPINNER_TAG = 109;

        UIView statusView;
        UIActivityIndicatorView theSpinner;
        UITextView statusMessage;
        UITextView failedConfigWarning;

        UIButton cancelButton;
        UIButton saveButton;

        UILabel signatureLabel;

        public enum statusType
        {
            ErrorAuth,
            ErrorComm,
            Success
        };

        public SettingsViewController (IntPtr handle) : base (handle)
        {
            doneButton = new UIBarButtonItem (UIBarButtonSystemItem.Done);
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);

            NavigationItem.RightBarButtonItem = doneButton;

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
            root.Add (AddAccountSettings ());
            Root = root;

            configureDoneButton ();
        }


        public void configureDoneButton()
        {
            // When user clicks done: check, confirm, and save
            doneButton.Clicked += (object sender, EventArgs e) => {
                McAccount theAccount = McAccount.QueryById<McAccount> (Account.AccountId);
                McCred theCred = McCred.QueryById<McCred> (Account.McCredId);
                McServer theServer = McServer.QueryById<McServer> (Account.ServerId);
                McConference theConference = McConference.QueryById<McConference> (Account.PreferredConferenceId);

                theConference.DefaultPhoneNumber = Account.ConferenceCallNumber;
                theConference.Update();
                theAccount.DisplayName = Account.AccountName;
                theAccount.Signature = Account.EmailSignature;
                theAccount.Update();

                theCred.Username = Account.UserName;
                theCred.Password = Account.Password;
                theAccount.EmailAddr = Account.EmailAddress;
                theServer.Host = Account.MailServer;

                McCred c = new McCred();
                c.Username = Account.UserName;
                c.Password = Account.Password;

                McServer s = new McServer();
                s.Host = Account.MailServer;

                var didStart = BackEnd.Instance.ValidateConfig(theAccount.Id, s, c);

                if(!didStart)
                {
                    //TODO what happens when there's a network failure?
                    Console.WriteLine("NETWORK FAILURE");
                }
                else
                {
                    configureStatusViewForValidating();
                }
            };
        }

        public void configureStatusViewForValidating()
        {
            float viewwidth = View.Frame.Width / 2.5f;
            statusView = new UIView(new System.Drawing.RectangleF(viewwidth/1.3f, 3.5f, viewwidth, View.Frame.Height / 6.0f));
            statusView.Layer.CornerRadius = 15.0f;
            statusView.BackgroundColor = UIColor.DarkGray;
            statusView.Alpha = .8f;

            statusMessage = new UITextView(new System.Drawing.RectangleF(8, 2, statusView.Frame.Width - 16, statusView.Frame.Height / 2.4f));
            statusMessage.BackgroundColor = UIColor.DarkGray;
            statusMessage.Alpha = .8f;
            statusMessage.Font = A.Font_AvenirNextDemiBold17;
            statusMessage.TextColor = A.Color_NachoBlue;
            statusMessage.Text = "Validating";
            statusMessage.TextAlignment = UITextAlignment.Center;
            statusView.AddSubview(statusMessage);

            theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.White);
            theSpinner.HidesWhenStopped = true;
            theSpinner.Tag = SPINNER_TAG;
            theSpinner.Frame = new System.Drawing.RectangleF(statusView.Frame.Width / 2 - 10, statusView.Frame.Height * .55f, 20, 20);
            theSpinner.StartAnimating();
            statusView.AddSubview(theSpinner);

            View.AddSubview(statusView);
        }

        public void saveSettings()
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

        public void configureStatusViewFor(statusType whatType)
        {
            string header;
            string message;
            bool isSuccess;

            float statusViewWidth = 213.0f;
            float statusViewHeight = 139.0f;
            float statusViewX = 58.0f;
            float statusViewY = 3.0f;

            switch (whatType) {
            case statusType.ErrorAuth:
                header = "Invalid Credentials";
                message = "Password or Username is incorrect. No emails can be sent or recieved. Still Continue?";
                isSuccess = false;
                break;
            case statusType.ErrorComm:
                header = "Validation Failed";
                message = "This account may not be able to send or receive emails. Are you sure you want to continue?";
                isSuccess = false;
                break;
            case statusType.Success:
                isSuccess = true;
            default:
                break;
            }

            if (statusView.Subviews.Length > 0) {

            while (statusView.Subviews.Length != 0) {
                UIView x = statusView.Subviews [0];
                x.RemoveFromSuperview ();
                }
            }

            if (isSuccess) {
                statusView.Frame = new System.Drawing.RectangleF (105, statusViewY + TableView.ContentOffset.Y, 110, 45);
                statusView.Layer.CornerRadius = 15.0f;
                statusView.BackgroundColor = UIColor.DarkGray;
                statusView.Alpha = .8f;

                statusMessage.Frame = new System.Drawing.RectangleF (8, 2, statusView.Frame.Width - 16, statusView.Frame.Height - 6);
                statusMessage.BackgroundColor = UIColor.DarkGray;
                statusMessage.Alpha = .8f;
                statusMessage.Font = A.Font_AvenirNextDemiBold17;
                statusMessage.TextColor = A.Color_NachoBlue;
                statusMessage.Text = "Success!";
                statusMessage.TextAlignment = UITextAlignment.Center;
                statusView.AddSubview (statusMessage);

                saveSettings();
                //dismissStatusView();

            } else {

                statusView.Frame = new System.Drawing.RectangleF(statusViewX, statusViewY + TableView.ContentOffset.Y, statusViewWidth, statusViewHeight);
                statusView.Layer.CornerRadius = 15.0f;
                statusView.BackgroundColor = UIColor.DarkGray;
                statusView.Alpha = .8f;

                statusMessage.Frame = new System.Drawing.RectangleF(8, 2, statusView.Frame.Width - 16, 35);
                statusMessage.BackgroundColor = UIColor.DarkGray;
                statusMessage.Alpha = .8f;
                statusMessage.Font = A.Font_AvenirNextDemiBold17;
                statusMessage.TextColor = UIColor.Red;
                statusMessage.Text = header;
                statusMessage.TextAlignment = UITextAlignment.Center;
                statusView.AddSubview(statusMessage);

                failedConfigWarning = new UITextView(new System.Drawing.RectangleF(8, 28.0f, statusView.Frame.Width - 16, statusView.Frame.Height- 70));
                failedConfigWarning.BackgroundColor = UIColor.DarkGray;
                failedConfigWarning.Alpha = .8f;
                failedConfigWarning.Font = A.Font_AvenirNextRegular12;
                failedConfigWarning.TextColor = UIColor.White;
                failedConfigWarning.Text = message;
                failedConfigWarning.TextAlignment = UITextAlignment.Center;
                statusView.AddSubview(failedConfigWarning);

                cancelButton = new UIButton (new System.Drawing.RectangleF (0, 100, statusView.Frame.Width / 2 - .5f, 34));
                cancelButton.BackgroundColor = UIColor.DarkGray;
                cancelButton.Alpha = .8f;
                cancelButton.Font = A.Font_AvenirNextDemiBold17;
                cancelButton.SetTitleColor(UIColor.White, UIControlState.Normal);
                cancelButton.SetTitle ("Cancel", UIControlState.Normal);
                cancelButton.Layer.CornerRadius = 10.0f;

                cancelButton.AddTarget (((object sender, EventArgs e) => {
                    dismissStatusView();
                }), UIControlEvent.TouchUpInside);

                statusView.AddSubview(cancelButton);

                UIView horizontalLine = new UIView (new System.Drawing.RectangleF (0, 99, statusView.Frame.Width, 1.0f));
                horizontalLine.Alpha = .8f;
                horizontalLine.BackgroundColor = UIColor.White;
                statusView.Add (horizontalLine);

                UIView verticalLine = new UIView (new System.Drawing.RectangleF (statusView.Frame.Width/2, 99, 1.0f, 40));
                verticalLine.Alpha = .8f;
                verticalLine.BackgroundColor = UIColor.White;
                statusView.Add (verticalLine);

                saveButton = new UIButton (new System.Drawing.RectangleF (statusView.Frame.Width/2 + 1.5f, 101.5f, statusView.Frame.Width / 2 - 1.5f, 34));
                saveButton.BackgroundColor = UIColor.DarkGray;
                saveButton.Alpha = .8f;
                saveButton.Font = A.Font_AvenirNextDemiBold17;
                saveButton.SetTitleColor(UIColor.White, UIControlState.Normal);
                saveButton.SetTitle ("Save", UIControlState.Normal);
                saveButton.Layer.CornerRadius = 10.0f;

                saveButton.AddTarget (((object sender, EventArgs e) => {
                    saveSettings();
                    dismissStatusView();
                }), UIControlEvent.TouchUpInside);
                statusView.AddSubview(saveButton);
            }
        }

        public void dismissStatusView()
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

                double timer = 0;
                while (timer < 1000000) {
                    timer += .01;
                }

                StopSpinner ();
                configureStatusViewFor(statusType.Success);
                return;
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {

                double timer = 0;
                while (timer < 1000000) {
                    timer += .01;
                }

                StopSpinner ();
                configureStatusViewFor (statusType.ErrorComm);
                return;
            }

            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {

                double timer = 0;
                while (timer < 1000000) {
                    timer += .01;
                }

                StopSpinner ();
                configureStatusViewFor (statusType.ErrorAuth);
                return;
            }
        }

        public void UpdateAccountCredentials()
        {
            McAccount theAccount = McAccount.QueryById<McAccount> (Account.AccountId);
            McCred theCred = McCred.QueryById<McCred> (Account.McCredId);
            McServer theServer = McServer.QueryById<McServer> (Account.ServerId);

            theCred.Username = Account.UserName;
            theCred.Password = Account.Password;
            theAccount.EmailAddr = Account.EmailAddress;
            theServer.Host = Account.MailServer;
        }

        public Section AddAccountSettings()
        {
            var AccountSection = new Section ("ACCOUNT");

            UITextField accountNameText = new UITextField ();
            var AccountName = new CustomTextInputElement (UIImage.FromBundle (""), "Account Name", Account.AccountName, accountNameText);
            accountNameText.ShouldReturn += (textField) => {
                Account.AccountName = textField.Text;
                textField.ResignFirstResponder ();
                return true;
            };
            AccountSection.Add (AccountName);

            UITextField userNameText = new UITextField ();
            var UserName = new CustomTextInputElement (UIImage.FromBundle(""), "User Name", Account.UserName, userNameText);
            userNameText.ShouldReturn += (textField) => {
                Account.UserName = textField.Text;
                textField.ResignFirstResponder();
                return true;
            };
            AccountSection.Add (UserName);

            UITextField passwordText = new UITextField ();
            var Password = new CustomTextInputElement (UIImage.FromBundle(""), "Password", Account.PasswordDisplay, passwordText);
            passwordText.ShouldReturn += (textField) => {
                Account.Password = textField.Text;
                textField.Text = String.Concat(Enumerable.Repeat("\u2022", Account.Password.Length));
                textField.ResignFirstResponder();
                return true;
            };

            passwordText.EditingDidBegin += (object sender, EventArgs e) => {
                passwordText.SecureTextEntry = true;
            };

            passwordText.EditingDidEnd += (object sender, EventArgs e) => {
                passwordText.SecureTextEntry = false;
            };
                
            AccountSection.Add (Password);

            UITextField emailText = new UITextField ();
            var EmailAddress = new CustomTextInputElement (UIImage.FromBundle(""), "Email Address", Account.EmailAddress, emailText);
            emailText.ShouldReturn += (textField) => {
                Account.EmailAddress = textField.Text;
                textField.ResignFirstResponder();
                return true;
            };
            AccountSection.Add (EmailAddress);

            UITextField mailServerText = new UITextField ();
            var MailServer = new CustomTextInputElement (UIImage.FromBundle(""), "Mail Server", Account.MailServer, mailServerText);
            mailServerText.ShouldReturn += (textField) => {
                Account.MailServer = textField.Text;
                textField.ResignFirstResponder();
                return true;
            };
            AccountSection.Add (MailServer);


            UITextField conferenceCallText = new UITextField ();
            var ConferenceCall = new CustomTextInputElement (UIImage.FromBundle(""), "Conference Call #", Account.ConferenceCallNumber, conferenceCallText);
            conferenceCallText.ShouldReturn += (textField) => { 
                Account.ConferenceCallNumber = textField.Text;
                textField.ResignFirstResponder();
                return true;
            };
            AccountSection.Add (ConferenceCall);
               
            signatureLabel = new UILabel ();
            signatureLabel.Text = Account.EmailSignature;
            var SettingsSignatureElement = new SignatureEntryElement ("Email Signature", signatureLabel);
            SettingsSignatureElement.Tapped += () => {
                PushSignatureView();
            };
            AccountSection.Add (SettingsSignatureElement);
            return AccountSection;
        }

        public void PushSignatureView ()
        {
            var root = new RootElement ("Signature");

            UITextView signatureEditTextView = new UITextView (new System.Drawing.RectangleF(0, 0, 320, 100));
            signatureEditTextView.Editable = true;

            signatureEditTextView.ShouldEndEditing += (test) => {
                Account.EmailSignature = test.Text;
                signatureLabel.Text = test.Text;
                return true;
            };

            signatureEditTextView.Changed += (object sender, EventArgs e) => {
                SelectionChanged(signatureEditTextView);
            };

            var thinSec = new ThinSection ();
            var signatureEditingElement = new StyledMultiLineTextInput("", Account.EmailSignature, signatureEditTextView);
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

        public void LoadSettingsForSelectedAccount(McAccount whatAccount)
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
                x.DefaultPhoneNumber = "(503) 345-6789";
                x.Insert ();
                whatAccount.PreferredConferenceId = x.Id;
                whatAccount.Update ();
                Account.PreferredConferenceId = x.Id;
                userConference = x;
            }

            Account.AccountName = whatAccount.DisplayName == null ? "Exchange" : whatAccount.DisplayName;
            Account.UserName = userCredentials.Username;
            Account.Password = userCredentials.Password;
            Account.PasswordDisplay = String.Concat (Enumerable.Repeat ("\u2022", userCredentials.Password.Length));
            Account.EmailAddress = whatAccount.EmailAddr;
            Account.MailServer = userMailServer.Host;
            Account.EmailSignature = whatAccount.Signature == null ? "Sent from NachoMail" : whatAccount.Signature;
            Account.ConferenceCallNumber = userConference.DefaultPhoneNumber == null ? "(562) 488-3229" : userConference.DefaultPhoneNumber;
        }

        public class AccountSettings
        {
            public string AccountName { get; set; } 
            public string UserName { get; set; }
            public string Password { get; set; }
            public string PasswordDisplay {get ;set; }
            public string EmailAddress { get; set; }
            public string MailServer { get; set; }
            public string DaysToSyncMail { get; set; }
            public string DaysToSyncCalendar { get; set; }
            public string EmailSignature { get; set; }
            public string ConferenceCallNumber { get; set; }
            public int AccountId { get; set; }
            public int McCredId { get; set; }
            public int ServerId { get; set; }
            public int PreferredConferenceId { get; set;}
            public AccountSettings()
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
