// This file has been autogenerated from a class added in the UI designer.

using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Drawing;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using System.Linq;
using System.Collections.Generic;
using NachoPlatform;

namespace NachoClient.iOS
{
    public partial class AdvancedLoginViewController : NcUIViewController
    {
        protected int LINE_OFFSET = 25;
        protected float CELL_HEIGHT = 44;
        protected float INSET = 15;
        protected float TOP_CELL_YVAL = 95;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        protected float keyboardHeight;

        UITextField emailText = new UITextField ();
        UITextField serverText = new UITextField ();
        UITextField domainText = new UITextField ();
        UITextField usernameText = new UITextField ();
        UITextField passwordText = new UITextField ();
        List<UITextField> inputFields = new List<UITextField>();
        UIScrollView scrollView;
        UILabel errorMessage;
        UITextView statusMessage;
        UIButton cancelValidation; 
        UIActivityIndicatorView theSpinner;
        UIView cancelLine;
        UIView statusView;
        UIButton genericSystemButton;
        UIButton connectButton;

        UIColor systemBlue;
        UIAlertView certificateAlert;
        AccountSettings theAccount;
        bool isBERunning;
        int accountId;
        AppDelegate appDelegate;
        public enum errorMessageEnum{
            Server,
            RequiredFields,
            InvalidEmail,
            Username,
            Credentials,
            Certificate,
            ServerConf,
            Network,
            FirstTime
        };

        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            createCertificateAlert ();
          
            generateSystemBlue ();
            createWaitingView ();
            createScrollView ();
            createInputFieldList ();
            theAccount = new AccountSettings ();
            accountId = LoginHelpers.getCurrentAccountId ();
            loadSettingsForAccount (accountId);
            addErrorLabel ();
            addCells ();
            //addLines ();
            fillInKnownFields ();
            configureKeyboards ();
            handleStatusEnums ();
            addConnectButton ();
            haveEnteredEmailAndPass ();
            addCustomerSupportButton ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }
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
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;

            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillHideNotification);
                NSNotificationCenter.DefaultCenter.RemoveObserver (UIKeyboard.WillShowNotification);
            }
        }
        public void setBEState(bool BERunning)
        {
            isBERunning = BERunning;
        }
            
        public void generateSystemBlue()
        {
            genericSystemButton = new UIButton (UIButtonType.System);
            systemBlue = genericSystemButton.CurrentTitleColor;
        }
        public void createScrollView ()
        {
            scrollView = new UIScrollView (View.Frame);
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.ContentSize = new SizeF (View.Frame.Width, View.Frame.Height - 64);
            View.Add (scrollView);
        }
        public void createInputFieldList(){
            inputFields.Add (emailText);
            inputFields.Add (serverText);
            inputFields.Add (domainText);
            inputFields.Add (usernameText);
            inputFields.Add (passwordText);
        }
        public void layoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
        }

        public void createWaitingView()
        {
            statusView = new UIView (new System.Drawing.RectangleF (60,100,View.Frame.Width - 120, 146));
            statusView.Tag = 50;
            statusView.Layer.CornerRadius = 7.0f;
            statusView.BackgroundColor = UIColor.White;
            statusView.Alpha = 1.0f;
            statusMessage = new UITextView (new System.Drawing.RectangleF (8, 2, statusView.Frame.Width - 16, statusView.Frame.Height / 2.4f));
            statusMessage.BackgroundColor = UIColor.White;
            statusMessage.Alpha = 1.0f;
            statusMessage.Font = UIFont.SystemFontOfSize (17);
            statusMessage.TextColor = UIColor.Black;
            statusMessage.Text = "Locating Your Server...";
            statusMessage.TextAlignment = UITextAlignment.Center;
            statusView.AddSubview (statusMessage);

            theSpinner = new UIActivityIndicatorView (UIActivityIndicatorViewStyle.WhiteLarge);
            theSpinner.Alpha = 1.0f;
            theSpinner.HidesWhenStopped = true;
            theSpinner.Tag = 1;
            theSpinner.Frame = new System.Drawing.RectangleF (statusView.Frame.Width / 2 - 20, 50, 40, 40);
            theSpinner.Color = systemBlue;
            theSpinner.StartAnimating ();
            statusView.AddSubview (theSpinner);

            cancelLine = new UIView (new System.Drawing.RectangleF (0, 105, statusView.Frame.Width, .5f));
            cancelLine.BackgroundColor = UIColor.LightGray;
            cancelLine.Tag = 2;
            statusView.AddSubview (cancelLine);

            cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, statusView.Frame.Width, 40));
            cancelValidation.Tag = 3;
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.White;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.TitleLabel.TextColor = systemBlue;
            cancelValidation.TouchUpInside += (object sender, EventArgs e) => {
                if(isBERunning){
                    BackEnd.Instance.Stop ();
                }
                setErrorMessage(errorMessageEnum.FirstTime);
                setTextToRed(new UITextField[] {});
                dismissWaitingView ();
            };
            statusView.Add (cancelValidation);
        }

        public void showWaitingView(){

            UIView greyBackground = new UIView (new System.Drawing.RectangleF (0, 0, View.Frame.Width, View.Frame.Height));
            greyBackground.BackgroundColor = UIColor.DarkGray;
            greyBackground.Alpha = .4f;
            greyBackground.Tag = 69;
            View.Add (greyBackground);

            cancelValidation = new UIButton (new System.Drawing.RectangleF (0, 106, statusView.Frame.Width, 40));
            cancelValidation.Tag = 3;
            cancelValidation.Layer.CornerRadius = 10.0f;
            cancelValidation.BackgroundColor = UIColor.White;
            cancelValidation.TitleLabel.TextAlignment = UITextAlignment.Center;
            cancelValidation.SetTitle ("Cancel", UIControlState.Normal);
            cancelValidation.TitleLabel.TextColor = systemBlue;
            cancelValidation.TouchUpInside += (object sender, EventArgs e) => {
                if(isBERunning){
                    BackEnd.Instance.Stop ();
                }
                setErrorMessage(errorMessageEnum.FirstTime);
                setTextToRed(new UITextField[] {});
                dismissWaitingView ();
            };

            statusView.Add (cancelValidation);
            View.AddSubview (statusView);
        }

        public void dismissWaitingView()
        {
            UIView statusWindow = View.ViewWithTag (50);
            UIView grayBackground = View.ViewWithTag (69);
            UIActivityIndicatorView spinner = (UIActivityIndicatorView)View.ViewWithTag (100);

            if (null != statusWindow) {
                statusWindow.RemoveFromSuperview ();
            }
            if (null != grayBackground) {
                grayBackground.RemoveFromSuperview ();
            }
            if (null != spinner) {
                spinner.RemoveFromSuperview ();
            }

        }

        public void addConnectButton ()
        {
            connectButton = new UIButton (new RectangleF (30, 389, View.Frame.Width - 60, CELL_HEIGHT));
            connectButton.BackgroundColor = A.Color_NachoGreen;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle ("Connect To NachoMail", UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            connectButton.TouchUpInside += (object sender, EventArgs e) => {
                if (canUserConnect ()) {
                    if (!LoginHelpers.GetCredsBit (accountId)) {
                        basicEnterFullConfiguration ();
                    } else {
                        if (haveEnteredHost ()) {
                            if (isValidHost ()) {
                                showWaitingView();
                                tryValidateConfig ();
                            }
                        } else {
                            showWaitingView();
                            tryAutoD ();
                        }
                    }
                }
            };
            scrollView.Add (connectButton);
        }

        public bool haveEnteredEmailAndPass(){
            if (0 == emailText.Text.Length || 0 == passwordText.Text.Length) {
                enableConnect (false);
                return false;
            } else {
                enableConnect (true);
                return true;
            }
        }
        public void enableConnect(bool shouldWe)
        {
            if (true == shouldWe) {
                connectButton.Enabled = true;
                connectButton.Alpha = 1.0f;
            } else {
                connectButton.Enabled = false;
                connectButton.Alpha = .5f;
            }
        }
        public void addCustomerSupportButton ()
        {
            UIButton customerSupportButton = new UIButton (new RectangleF (30, 439, View.Frame.Width - 60, CELL_HEIGHT));
            customerSupportButton.BackgroundColor = A.Color_NachoGreen;
            customerSupportButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            customerSupportButton.SetTitle ("Customer Support", UIControlState.Normal);
            customerSupportButton.TitleLabel.TextColor = UIColor.White;
            customerSupportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            customerSupportButton.TouchUpInside += (object sender, EventArgs e) => {
                PerformSegue("AdvancedLoginToSupport", this);
            };
            scrollView.Add (customerSupportButton);
        }

        public void addErrorLabel ()
        {
            errorMessage = new UILabel (new RectangleF (20, 20, View.Frame.Width - 40, 50));
            errorMessage.Font = A.Font_AvenirNextRegular17;
            errorMessage.TextColor = A.Color_NachoRed;
            errorMessage.Lines = 2;
            errorMessage.TextAlignment = UITextAlignment.Center;
            if (LoginHelpers.GetCredsBit(accountId)) {
                setErrorMessage (errorMessageEnum.FirstTime);
            }
            scrollView.Add (errorMessage);
        }

        public void addCells ()
        {
            AddInputCell ("Email", emailText, "joe@bigdog.com", TOP_CELL_YVAL,true);
            AddInputCell ("Server", serverText, "Required", TOP_CELL_YVAL + 69, true);
            AddInputCell ("Domain", domainText, "Optional", TOP_CELL_YVAL + 138, true);
            AddInputCell ("Username", usernameText, "Required", TOP_CELL_YVAL + 182,false);
            AddInputCell ("Password", passwordText, "******", TOP_CELL_YVAL + 226, true);
            UIView whiteInset = new UIView (new RectangleF(0, TOP_CELL_YVAL + 139, 15, 90));
            whiteInset.BackgroundColor = UIColor.White;
            scrollView.Add (whiteInset);
        }

//        public void addLines ()
//        {
//            AddLine (INSET, TOP_CELL_YVAL + 182, View.Frame.Width - INSET, separatorColor);
//            AddLine (INSET, TOP_CELL_YVAL + 226, View.Frame.Width - INSET, separatorColor);
//        }

        public void configureKeyboards ()
        {
            usernameText.ShouldReturn += (textField) => {
                textField.ResignFirstResponder ();
                return true;
            };
            usernameText.AutocapitalizationType = UITextAutocapitalizationType.None;
            usernameText.AutocorrectionType = UITextAutocorrectionType.No;

            emailText.ShouldReturn += (textField) => {
                haveEnteredEmailAndPass();
                textField.ResignFirstResponder ();
                return true;
            };
            emailText.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailText.AutocorrectionType = UITextAutocorrectionType.No;

            domainText.ShouldReturn += (textField) => {
                textField.ResignFirstResponder ();
                return true;
            };
            domainText.AutocapitalizationType = UITextAutocapitalizationType.None;
            domainText.AutocorrectionType = UITextAutocorrectionType.No;

            serverText.ShouldReturn += (textField) => {
                textField.ResignFirstResponder ();
                return true;
            };
            serverText.AutocapitalizationType = UITextAutocapitalizationType.None;
            serverText.AutocorrectionType = UITextAutocorrectionType.No;

            passwordText.SecureTextEntry = true;
            passwordText.ShouldReturn += (textField) => {
                haveEnteredEmailAndPass();
                textField.ResignFirstResponder ();
                return true;
            };
            passwordText.AutocapitalizationType = UITextAutocapitalizationType.None;
            passwordText.AutocorrectionType = UITextAutocorrectionType.No;
        }

        public void handleStatusEnums ()
        {
            if (accountId != 0) {
                NachoCore.ActiveSync.AsProtoControl protoControl = new NachoCore.ActiveSync.AsProtoControl (BackEnd.Instance, accountId);
                if (protoControl != null) {
                    //NcCaseError
                    BackEndAutoDStateEnum AutoDState = protoControl.AutoDState;
                    switch (AutoDState) {
                    case BackEndAutoDStateEnum.ServerConfWait:
                        setErrorMessage (errorMessageEnum.ServerConf);
                        setTextToRed (new UITextField[] { emailText });
                        if (isBERunning) {
                            BackEnd.Instance.Stop (accountId);
                        }
                        return;

                    case BackEndAutoDStateEnum.CredWait:
                        setErrorMessage (errorMessageEnum.Credentials);
                        setTextToRed (new UITextField[] { usernameText, passwordText });
                        if (isBERunning) {
                            BackEnd.Instance.Stop (accountId);
                        }
                        return;

                    case BackEndAutoDStateEnum.CertAskWait:
                        setErrorMessage (errorMessageEnum.Certificate);
                        certificateAlert.Show ();
                        return;

                    case BackEndAutoDStateEnum.PostAutoDPostFSync:
                        if (LoginHelpers.GetTutorialBit (accountId)) {
                            PerformSegue ("AdvancedLoginToNachoNow", this);
                        } else {
                            PerformSegue ("AdvancedLoginToHome", this);
                        }
                        return;

                    case BackEndAutoDStateEnum.Running:
                        if (LoginHelpers.GetBeStateBit (2)) {
                            errorMessage.Text = "Auto-D is running.";
                            isBERunning = true;
                            showWaitingView();
                        }
                        else{
                            setErrorMessage (errorMessageEnum.FirstTime);
                        }

                        return;
                    }
                }
            }

        }

        public void AddLine (float offset, float yVal, float width, UIColor color)
        {
            var lineUIView = new UIView (new RectangleF (offset, yVal, width, .5f));
            lineUIView.BackgroundColor = color;
            scrollView.Add (lineUIView);
        }

        public void AddInputCell (string labelText, UITextField textInput, string placeHolder, float yVal, bool hasBorder)
        {
            UIView inputBox = new UIView (new RectangleF (0, yVal, View.Frame.Width+1, CELL_HEIGHT));
            inputBox.BackgroundColor = UIColor.White;
            if (hasBorder) {
                inputBox.Layer.BorderColor = UIColor.LightGray.CGColor;
                inputBox.Layer.BorderWidth = .4f;
            }

            UILabel cellLefthandLabel = new UILabel (new RectangleF (INSET, 0, 80, CELL_HEIGHT));
            cellLefthandLabel.Text = labelText;
            cellLefthandLabel.BackgroundColor = UIColor.White;
            cellLefthandLabel.TextColor = A.Color_NachoGreen;
            cellLefthandLabel.Font = A.Font_AvenirNextMedium14;
            inputBox.Add (cellLefthandLabel);

            textInput.Frame = new RectangleF (120, 0, inputBox.Frame.Width - 100, inputBox.Frame.Height);
            textInput.BackgroundColor = UIColor.White;
            textInput.Placeholder = placeHolder;
            textInput.Font = A.Font_AvenirNextRegular14;
            inputBox.Add (textInput);

            scrollView.Add (inputBox);
        }

        public void fillInKnownFields ()
        {
            if (LoginHelpers.GetCredsBit(accountId)) {
                emailText.Text = theAccount.Account.EmailAddr;
                usernameText.Text = theAccount.Credentials.Username;
                passwordText.Text = theAccount.Credentials.Password;
            }
        }


        public void loadSettingsForAccount (int accountId)
        {
            if (LoginHelpers.GetCredsBit(accountId)) {
                theAccount.Account = McAccount.QueryById<McAccount> (accountId);
                theAccount.Credentials = McCred.QueryById<McCred> (theAccount.Account.CredId);
            }
        }

        public void setUsersSettings ()
        {
            McAccount account = McAccount.QueryById<McAccount> (accountId);
            McCred theCred = McCred.QueryById<McCred> (account.CredId); 

           // theAccount.Account = McAccount.QueryById<McAccount> (accountId);
           // McCred theCred = McCred.QueryById<McCred> (theAccount.Account.CredId); 
           // theAccount.Credentials = McCred.QueryById<McCred> (theAccount.Account.CredId);
            if (usernameText.Text.Length > 0) {
                theCred.Username = usernameText.Text;
            } else {
                theCred.Username = emailText.Text;
            }

            theCred.Password = passwordText.Text;
            theCred.Update ();
            account.CredId = theCred.Id;
            account.EmailAddr = emailText.Text;
            account.Update ();

//            theAccount.Credentials = theCred;
//            theAccount.Account.CredId = theCred.Id;
//            theAccount.Account.EmailAddr = emailText.Text;
//            theAccount.Account.Update ();

            theAccount.Account.Id = account.Id;
            theAccount.Credentials.Id = theCred.Id;
        }

        public void  basicEnterFullConfiguration () {
            string credUserName = "";
            NcModel.Instance.RunInTransaction (() => {
                // Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).
                if(usernameText.Text.Length == 0){
                    credUserName = emailText.Text;
                }else{
                    credUserName = usernameText.Text;
                }
                var cred = new McCred () { Username = credUserName, Password = passwordText.Text };
                cred.Insert ();
                theAccount.Credentials = cred;
                int serverId = 0;
                if (haveEnteredHost() && isValidHost()) {
                    var server = new McServer () { Host = serverText.Text };
                    server.Insert ();
                    serverId = server.Id;
                }
                // You will always need to supply the user's email address.
                appDelegate.Account = new McAccount () { EmailAddr = emailText.Text };
                // The account object is the "top", pointing to credential, server, and opaque protocol state.
                appDelegate.Account.CredId = cred.Id;
                appDelegate.Account.ServerId = serverId;
                appDelegate.Account.Insert ();
                theAccount.Account = appDelegate.Account;
                LoginHelpers.SetCredsBit(appDelegate.Account.Id, true);
            });
            accountId = LoginHelpers.getCurrentAccountId ();
            NcAssert.True (0 != accountId, "BAD ACCOUNT");

            BackEnd.Instance.Start (appDelegate.Account.Id);
            isBERunning = true;
            showWaitingView();
        }

        public void tryAutoD ()
        {
            setUsersSettings ();
            BackEnd.Instance.Start (accountId);
            isBERunning = true;
        }

        public void tryValidateConfig ()
        {
            setUsersSettings ();
            McServer test = new McServer ();
            test.Host = serverText.Text;
            BackEnd.Instance.ValidateConfig (accountId, test, theAccount.Credentials);
        }

        public void setTextToRed(UITextField[] whichFields){
            foreach (var textField in inputFields) {
                textField.TextColor = UIColor.Black;
                for (int i = 0; i < whichFields.Count (); i++) {
                    if (textField == whichFields [i]) {
                        textField.TextColor = A.Color_NachoRed;
                    }
                }
            }
        }
        public bool haveEnteredHost ()
        {
            if (0 == serverText.Text.Length) {
                return false;
            } else {
                return true;
            }
        }

        public bool hasNetworkConnection()
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return false;
            } else {
                return true;
            }
        }
        public bool canUserConnect()
        {
            if (!haveEnteredEmailAndPass ()) {
                setErrorMessage(errorMessageEnum.RequiredFields);
                return false;
            }

            if (!isValidEmail (emailText.Text)) {
                setErrorMessage(errorMessageEnum.InvalidEmail);
                return false;
            }

            if(!hasNetworkConnection()){
                setErrorMessage (errorMessageEnum.Network);
                return false;
            }
            return true;
        }
        bool isValidEmail(string email){
            RegexUtilities regexUtil = new RegexUtilities ();
            return regexUtil.IsValidEmail (email);
        }

        public bool isValidHost ()
        {
            UriHostNameType hostnameURI = Uri.CheckHostName (serverText.Text);
            if (hostnameURI == UriHostNameType.Dns || hostnameURI == UriHostNameType.IPv4 || hostnameURI == UriHostNameType.IPv6) {
                return true;
            } else {
                UIAlertView badHost = new UIAlertView ();
                badHost.Title = "Bad Server Name";
                badHost.Message = "Please check that the server name is entered correctly.";
                badHost.AddButton ("Ok");
                badHost.Clicked += (object sender, UIButtonEventArgs e) => {
                };
                badHost.Show ();
                errorMessage.Text = "Invalid server name. Please check that you typed it in correctly.";
                setTextToRed (new UITextField[] {serverText});
                return false;
            }
        }

        public void createCertificateAlert()
        {
            certificateAlert = new UIAlertView ();
            certificateAlert.Title = "Need To Accept Certificate";
            certificateAlert.Message = "Do you trust this?";
            certificateAlert.AddButton ("Accept");
            certificateAlert.AddButton ("Cancel");
            certificateAlert.DismissWithClickedButtonIndex (1, false);
            certificateAlert.Clicked += (object sender, UIButtonEventArgs e) => {
                NcApplication.Instance.CertAskResp (accountId, true);
                LoginHelpers.SetCertificateBit(accountId, true);
                showWaitingView();
            };
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

        protected virtual void OnKeyboardChanged (bool visible, float height)
        {
            var newHeight = (visible ? height : 0);

            if (newHeight == keyboardHeight) {
                return;
            }
            keyboardHeight = newHeight;

            layoutView ();
        }
        public void setErrorMessage(errorMessageEnum whatType)
        {
            switch (whatType) {
            case errorMessageEnum.Credentials:
                errorMessage.Text = "There seems to be a problem with your credentials.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.RequiredFields:
                errorMessage.Text = "You must enter an email address and password.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.InvalidEmail:
                errorMessage.Text = "The email address you entered is not formatted correctly.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.Certificate:
                errorMessage.Text = "Accept Certificate?";
                errorMessage.TextColor = A.Color_NachoGreen;
                return;
            case errorMessageEnum.Server:
                errorMessage.Text = "The server name you entered is not valid. Please fix and try again.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.ServerConf:
                errorMessage.Text = "Looks like we had a problem finding '" + theAccount.Account.EmailAddr + "'.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.FirstTime:
                errorMessage.Text = "Please fill out the required credentials.";
                errorMessage.TextColor = A.Color_NachoGreen;
                return;
            case errorMessageEnum.Username:
                errorMessage.Text = "There seems to be a problem with your user name.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            case errorMessageEnum.Network:
                errorMessage.Text = "No network connection. Please check that you have internet access.";
                errorMessage.TextColor = A.Color_NachoRed;
                return;
            }
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_FolderSyncSucceeded == s.Status.SubKind) {
                LoginHelpers.SetSyncedBit (accountId, true);
                dismissWaitingView ();
                if (LoginHelpers.GetTutorialBit (accountId)) {
                    PerformSegue ("AdvancedLoginToNachoNow", this);
                } else {
                    //FIXME Segue issues what type of Segue to use? No NavBar if coming from basic
                    PerformSegue ("AdvancedLoginToHome", this);
                }
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                statusMessage.TextColor = systemBlue;
                statusMessage.Text = "Found Your Server...";
                theAccount.Server = McServer.QueryById<McServer> (1);
                serverText.Text = theAccount.Server.Host;
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.Network);
                setTextToRed (new UITextField[] {});
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                setTextToRed (new UITextField[] {});
                BackEnd.Instance.Start ();
                isBERunning = true;
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                setTextToRed (new UITextField[] {serverText});
                setErrorMessage (errorMessageEnum.Server);
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.Credentials);
                setTextToRed (new UITextField[] { usernameText, passwordText });
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.Username);
                setTextToRed (new UITextField[] { usernameText});
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.ServerConf);
                setTextToRed (new UITextField[] { emailText});
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Error_CredReqCallback == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.Credentials);
                setTextToRed (new UITextField[] { usernameText, passwordText });
                dismissWaitingView ();
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                setErrorMessage (errorMessageEnum.Certificate);
                certificateAlert.Show ();
            }
        }

        public class AccountSettings
        {
            public string EmailAddress { get; set; }
            public int AccountId { get; set; }
            public int CredId { get; set; }
            public McAccount Account { get; set; }
            public McCred Credentials { get; set; }
            public McServer Server { get; set; }

            public AccountSettings ()
            {

            }
        }
    }
}
