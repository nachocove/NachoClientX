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
    public partial class AdvancedLoginViewController : NcUIViewController, INachoCertificateResponderParent
    {
        protected float CELL_HEIGHT = 44;
        protected float INSET = 15;
        protected float keyboardHeight;
        const int GRAY_BACKGROUND_TAG = 20;

        UITextField emailText = new UITextField ();
        UITextField serverText = new UITextField ();
        UITextField domainText = new UITextField ();
        UITextField usernameText = new UITextField ();
        UITextField passwordText = new UITextField ();
        List<UITextField> inputFields = new List<UITextField> ();
        UIScrollView scrollView;
        UILabel errorMessage;

        public WaitingScreen waitScreen;

        public UIView statusViewBackground;
        public string certificateInformation = "";
        public bool hasSyncedEmail = false;


        UIButton connectButton;
        AccountSettings theAccount;
        AppDelegate appDelegate;
        CertificateView certificateView;
        UIView contentView;
        float yOffset;

        public UIView loadingCover;

        public enum LoginStatus
        {
            ValidateSuccessful,
            BadServer,
            InvalidEmail,
            InvalidServerName,
            BadUsername,
            BadCredentials,
            AcceptCertificate,
            ServerConf,
            NoNetwork,
            EnterInfo,
        };

        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
            appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            theAccount = new AccountSettings ();
            loadSettingsForAccount ();
            CreateView ();

            waitScreen = new WaitingScreen (View.Frame);
            waitScreen.SetOwner (this);
            waitScreen.CreateView ();
            View.Add (waitScreen);

            certificateView = new CertificateView (View.Frame);
            certificateView.SetOwner (this);
            certificateView.CreateView ();
            View.Add (certificateView);
        }

        public override void ViewDidAppear (bool animated)
        {
            if (HandlesKeyboardNotifications) {
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillHideNotification, OnKeyboardNotification);
                NSNotificationCenter.DefaultCenter.AddObserver (UIKeyboard.WillShowNotification, OnKeyboardNotification);
            }

            fillInKnownFields ();
            if (LoginHelpers.IsCurrentAccountSet ()) {
                if (LoginHelpers.HasViewedTutorial (LoginHelpers.GetCurrentAccountId ())) {
                    handleStatusEnums ();
                } else {
                    waitScreen.ShowView ();
                }
            } else {
                handleStatusEnums ();
            }

            if (waitScreen.Hidden == true) {
                NavigationItem.Title = "Account Setup";
                loadingCover.Hidden = true;
            }

            LayoutView ();
            base.ViewDidAppear (animated);
        }

        public override void ViewWillAppear (bool animated)
        {
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;

                if (this.NavigationController.NavigationBarHidden == true) {
                    this.NavigationController.NavigationBarHidden = false; 
                }

                NavigationItem.SetHidesBackButton (true, false);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            base.ViewWillAppear (animated);
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

        public override bool ShouldAutorotate ()
        {
            return false;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToSupport")) {
                return;
            }
        }

        public void CreateView ()
        {
            if (null != this.NavigationController) {
                NavigationController.NavigationBar.Opaque = true;
                NavigationController.NavigationBar.BackgroundColor = A.Color_NachoGreen.ColorWithAlpha (1.0f);
                NavigationController.NavigationBar.Translucent = false;
            }


            scrollView = new UIScrollView (View.Frame);
            scrollView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.KeyboardDismissMode = UIScrollViewKeyboardDismissMode.OnDrag;
            View.AddSubview (scrollView);

            contentView = new UIView (View.Frame);
            contentView.BackgroundColor = A.Color_NachoNowBackground;
            scrollView.AddSubview (contentView);

            yOffset = 15f;
            errorMessage = new UILabel (new RectangleF (20, 15, View.Frame.Width - 40, 50));
            errorMessage.Font = A.Font_AvenirNextRegular17;
            errorMessage.BackgroundColor = A.Color_NachoNowBackground;
            errorMessage.TextColor = A.Color_NachoRed;
            errorMessage.Lines = 2;
            errorMessage.TextAlignment = UITextAlignment.Center;
            contentView.AddSubview (errorMessage);

            yOffset = errorMessage.Frame.Bottom + 15;
            AddInputCell ("Email", emailText, "joe@bigdog.com", yOffset, true);
            yOffset += CELL_HEIGHT + 25;
            AddInputCell ("Server", serverText, "Required", yOffset, true);
            yOffset += CELL_HEIGHT + 25;
            float topDomainCell = yOffset;
            AddInputCell ("Domain", domainText, "Optional", yOffset, true);
            yOffset += CELL_HEIGHT;
            AddInputCell ("Username", usernameText, "Required", yOffset, false);
            yOffset += CELL_HEIGHT;
            AddInputCell ("Password", passwordText, "******", yOffset, true);
            yOffset += CELL_HEIGHT + 25;

            UIView whiteInset = new UIView (new RectangleF (0, topDomainCell + 1, 15, 90));
            whiteInset.BackgroundColor = UIColor.White;
            contentView.AddSubview (whiteInset);

            connectButton = new UIButton (new RectangleF (25, yOffset, View.Frame.Width - 50, 46));
            connectButton.BackgroundColor = A.Color_NachoTeal;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle ("Connect", UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            connectButton.Layer.CornerRadius = 4f;
            connectButton.Layer.MasksToBounds = true;
            connectButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                if (canUserConnect ()) {
                    if (!LoginHelpers.IsCurrentAccountSet ()) {
                        if (haveEnteredHost ()) {
                            if (IsValidServer (serverText.Text)) {
                                basicEnterFullConfiguration ();
                            }
                        } else {
                            basicEnterFullConfiguration ();
                        }
                    } else {
                        if (haveEnteredHost ()) {
                            if (IsValidServer (serverText.Text)) {
                                tryValidateConfig ();
                                waitScreen.ShowView ();
                            }
                        } else {
                            tryAutoD ();
                            waitScreen.ShowView ();
                        }
                    }
                }
            };
            contentView.AddSubview (connectButton);

            yOffset = connectButton.Frame.Bottom + 15;

            UIButton customerSupportButton = new UIButton (new RectangleF (50, yOffset, View.Frame.Width - 100, 30));
            customerSupportButton.BackgroundColor = A.Color_NachoNowBackground;
            customerSupportButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            customerSupportButton.SetTitle ("Customer Support", UIControlState.Normal);
            customerSupportButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            customerSupportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            customerSupportButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                PerformSegue("SegueToSupport", this);
            };
            contentView.AddSubview (customerSupportButton);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            loadingCover = new UIView (View.Frame);
            loadingCover.BackgroundColor = A.Color_NachoGreen;
            contentView.Add (loadingCover);

            createInputFieldList ();
            configureKeyboards ();
        }

        public void ConfigureView (LoginStatus currentStatus)
        {
            haveEnteredEmailAndPass ();

            switch (currentStatus) {
            case LoginStatus.BadCredentials:
                errorMessage.Text = "There seems to be a problem with your credentials.";
                errorMessage.TextColor = A.Color_NachoRed;
                setTextToRed (new UITextField[] { emailText, usernameText, passwordText });
                return;
            case LoginStatus.ValidateSuccessful:
                setTextToRed (new UITextField[] { });
                return;
            case LoginStatus.InvalidEmail:
                errorMessage.Text = "The email address you entered is not formatted correctly.";
                errorMessage.TextColor = A.Color_NachoRed;
                setTextToRed (new UITextField[] { emailText });
                return;
            case LoginStatus.AcceptCertificate:
                errorMessage.Text = "Accept Certificate?";
                errorMessage.TextColor = A.Color_NachoGreen;
                return;
            case LoginStatus.BadServer:
                errorMessage.Text = "The server name you entered is not valid. Please fix and try again.";
                errorMessage.TextColor = A.Color_NachoRed;
                setTextToRed (new UITextField[] { serverText });
                return;
            case LoginStatus.ServerConf:
                errorMessage.Text = "Looks like we had a problem finding '" + theAccount.Account.EmailAddr + "'.";
                errorMessage.TextColor = A.Color_NachoRed;
                if (serverText.Text.Length > 0) {
                    setTextToRed (new UITextField[] { emailText, serverText });
                } else {
                    setTextToRed (new UITextField[] { emailText });
                }
                return;
            case LoginStatus.EnterInfo:
                errorMessage.Text = "Please fill out the required credentials.";
                errorMessage.TextColor = A.Color_NachoGreen;
                setTextToRed (new UITextField[] { });
                return;
            case LoginStatus.BadUsername:
                errorMessage.TextColor = A.Color_NachoRed;
                if (usernameText.Text.Length > 0) {
                    setTextToRed (new UITextField[] { usernameText });
                    errorMessage.Text = "There seems to be a problem with your user name.";
                } else {
                    setTextToRed (new UITextField[] { emailText });
                    errorMessage.Text = "There seems to be a problem with your email.";
                }
                return;
            case LoginStatus.NoNetwork:
                errorMessage.Text = "No network connection. Please check that you have internet access.";
                errorMessage.TextColor = A.Color_NachoRed;
                setTextToRed (new UITextField[] { });
                return;
            case LoginStatus.InvalidServerName:
                errorMessage.Text = "Invalid server name. Please check that you typed it in correctly.";
                errorMessage.TextColor = A.Color_NachoRed;
                setTextToRed (new UITextField[] { serverText });
                return;
            }
        }

        public void createInputFieldList ()
        {
            inputFields.Add (emailText);
            inputFields.Add (serverText);
            inputFields.Add (domainText);
            inputFields.Add (usernameText);
            inputFields.Add (passwordText);
        }

        protected void LayoutView ()
        {
            scrollView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            var contentFrame = new RectangleF (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        public bool haveEnteredEmailAndPass ()
        {
            if (0 == emailText.Text.Length || 0 == passwordText.Text.Length) {
                enableConnect (false);
                return false;
            } else {
                enableConnect (true);
                return true;
            }
        }

        public void enableConnect (bool shouldWe)
        {
            if (true == shouldWe) {
                connectButton.Enabled = true;
                connectButton.Alpha = 1.0f;
            } else {
                connectButton.Enabled = false;
                connectButton.Alpha = .5f;
            }
        }

        public void configureKeyboards ()
        {
            emailText.ShouldReturn += (textField) => {
                haveEnteredEmailAndPass ();
                textField.TextColor = UIColor.Black;
                View.EndEditing(true);
                return true;
            };
            emailText.EditingChanged += (object sender, EventArgs e) => {
                haveEnteredEmailAndPass ();
            };
            emailText.AutocapitalizationType = UITextAutocapitalizationType.None;
            emailText.AutocorrectionType = UITextAutocorrectionType.No;

            serverText.ShouldReturn += (textField) => {
                textField.TextColor = UIColor.Black;
                View.EndEditing(true);
                return true;
            };
            serverText.AutocapitalizationType = UITextAutocapitalizationType.None;
            serverText.AutocorrectionType = UITextAutocorrectionType.No;

            domainText.ShouldReturn += (textField) => {
                View.EndEditing(true);
                return true;
            };
            domainText.AutocapitalizationType = UITextAutocapitalizationType.None;
            domainText.AutocorrectionType = UITextAutocorrectionType.No;

            usernameText.ShouldReturn += (textField) => {
                usernameText.TextColor = UIColor.Black;
                View.EndEditing(true);
                return true;
            };
            usernameText.AutocapitalizationType = UITextAutocapitalizationType.None;
            usernameText.AutocorrectionType = UITextAutocorrectionType.No;

            passwordText.SecureTextEntry = true;
            passwordText.ShouldReturn += (textField) => {
                haveEnteredEmailAndPass ();
                textField.TextColor = UIColor.Black;
                View.EndEditing(true);
                return true;
            };
            passwordText.EditingChanged += (object sender, EventArgs e) => {
                haveEnteredEmailAndPass ();
            };
            passwordText.AutocapitalizationType = UITextAutocapitalizationType.None;
            passwordText.AutocorrectionType = UITextAutocorrectionType.No;
        }

        public void handleStatusEnums ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {

                BackEndAutoDStateEnum backEndState = BackEnd.Instance.AutoDState (LoginHelpers.GetCurrentAccountId ());

                switch (backEndState) {
                case BackEndAutoDStateEnum.ServerConfWait:
                    Log.Info (Log.LOG_UI, "ServerConfWait Auto-D-State-Enum On Page Load");
                    stopBeIfRunning ();
                    ConfigureView (LoginStatus.ServerConf);
                    return;

                case BackEndAutoDStateEnum.CredWait:
                    Log.Info (Log.LOG_UI, "CredWait Auto-D-State-Enum On Page Load");
                    ConfigureView (LoginStatus.BadCredentials);
                    return;

                case BackEndAutoDStateEnum.CertAskWait:
                    Log.Info (Log.LOG_UI, "CertAskWait Auto-D-State-Enum On Page Load");
                    ConfigureView (LoginStatus.AcceptCertificate);
                    certificateCallbackHandler ();
                    waitScreen.ShowView ();
                    return;

                case BackEndAutoDStateEnum.PostAutoDPreInboxSync:
                    Log.Info (Log.LOG_UI, "PostAutoDPreInboxSync Auto-D-State-Enum On Page Load");
                    LoginHelpers.SetAutoDCompleted (LoginHelpers.GetCurrentAccountId (), true);
                    errorMessage.Text = "Waiting for Inbox-Sync.";
                    waitScreen.SetLoadingText ("Syncing Your Inbox...");
                    waitScreen.ShowView ();
                    return;

                case BackEndAutoDStateEnum.PostAutoDPostInboxSync:
                    Log.Info (Log.LOG_UI, "PostAutoDPostInboxSync Auto-D-State-Enum On Page Load");
                    LoginHelpers.SetFirstSyncCompleted (LoginHelpers.GetCurrentAccountId (), true);
                    PerformSegue(StartupViewController.NextSegue(), this);
                    return;

                case BackEndAutoDStateEnum.Running:
                    Log.Info (Log.LOG_UI, "Running Auto-D-State-Enum On Page Load");
                    errorMessage.Text = "Auto-D is running.";
                    waitScreen.ShowView ();
                    return;

                default:
                    ConfigureView (LoginStatus.EnterInfo);
                    return;
                }
            } else {
                ConfigureView (LoginStatus.EnterInfo);
            }
        }

        public void AddInputCell (string labelText, UITextField textInput, string placeHolder, float yVal, bool hasBorder)
        {
            UIView inputBox = new UIView (new RectangleF (0, yVal, View.Frame.Width + 1, CELL_HEIGHT));
            inputBox.BackgroundColor = UIColor.White;
            if (hasBorder) {
                inputBox.Layer.BorderColor = A.Color_NachoBorderGray.CGColor;
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

            contentView.AddSubview (inputBox);
        }

        public void fillInKnownFields ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                if (LoginHelpers.HasProvidedCreds (LoginHelpers.GetCurrentAccountId ())) {
                    emailText.Text = theAccount.Account.EmailAddr;
                    if (theAccount.Credentials.Username != theAccount.Account.EmailAddr) {
                        usernameText.Text = theAccount.Credentials.Username;
                    }
                    passwordText.Text = theAccount.Credentials.GetPassword ();
                    if (null != theAccount.Server) {
                        serverText.Text = theAccount.Server.Host;
                        if (443 != theAccount.Server.Port) {
                            serverText.Text += ":" + theAccount.Server.Port.ToString ();
                        }
                    }
                }
            }
        }

        public void loadSettingsForAccount ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                if (LoginHelpers.HasProvidedCreds (LoginHelpers.GetCurrentAccountId ())) {
                    theAccount.Account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                    theAccount.Credentials = McCred.QueryByAccountId<McCred> (theAccount.Account.Id).SingleOrDefault ();
                    theAccount.Server = McServer.QueryByAccountId<McServer> (theAccount.Account.Id).SingleOrDefault ();
                }
            }
        }

        public void setUsersSettings ()
        {
            theAccount.Account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
            theAccount.Credentials = McCred.QueryByAccountId<McCred> (theAccount.Account.Id).SingleOrDefault (); 

            if (usernameText.Text.Length > 0) {
                theAccount.Credentials.Username = usernameText.Text;
            } else {
                theAccount.Credentials.Username = emailText.Text;
            }

            theAccount.Credentials.UpdatePassword (passwordText.Text);
            theAccount.Credentials.Update ();
            theAccount.Account.EmailAddr = emailText.Text;
            theAccount.Account.Update ();
        }

        public void  basicEnterFullConfiguration ()
        {
            string credUserName = "";
            NcModel.Instance.RunInTransaction (() => {

                // You will always need to supply the user's email address.
                appDelegate.Account = new McAccount () { EmailAddr = emailText.Text };
                appDelegate.Account.Signature = "Sent from Nacho Mail";
                appDelegate.Account.Insert ();
                // FIXME Need to regex-validate UI inputs.
                // You will always need to supply user credentials (until certs, for sure).
                if (usernameText.Text.Length == 0) {
                    credUserName = emailText.Text;
                } else {
                    credUserName = usernameText.Text;
                }
                var cred = new McCred () { 
                    AccountId = appDelegate.Account.Id,
                    Username = credUserName, 
                };
                cred.Insert ();
                cred.UpdatePassword (passwordText.Text);
                theAccount.Credentials = cred;
                int serverId = 0;
                if (haveEnteredHost ()) {
                    var server = new McServer () { 
                        AccountId = appDelegate.Account.Id,
                    };
                    SetHostAndPort(server);
                    server.Insert ();
                    serverId = server.Id;
                }

                theAccount.Account = appDelegate.Account;
                Telemetry.RecordAccountEmailAddress (appDelegate.Account);
                LoginHelpers.SetHasProvidedCreds (appDelegate.Account.Id, true);
            });

            BackEnd.Instance.Start (LoginHelpers.GetCurrentAccountId ());
            waitScreen.ShowView ();
        }

        public void removeServerRecord ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                var account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                var removeServerRecord = McServer.QueryByAccountId<McServer> (account.Id).SingleOrDefault ();
                if (null != removeServerRecord) {
                    removeServerRecord.Delete ();
                }
            }
        }

        public void tryAutoD ()
        {
            setUsersSettings ();
            removeServerRecord ();
            startBe ();
        }

        public void tryValidateConfig ()
        {
            McServer mailServer = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId ()).FirstOrDefault ();
            if (null != mailServer) {
                SetHostAndPort (mailServer);
                mailServer.Update ();
            } else {
                mailServer = new McServer ();
                SetHostAndPort (mailServer);
                mailServer.AccountId = LoginHelpers.GetCurrentAccountId ();
                mailServer.Insert ();
            }
            setUsersSettings ();
            BackEnd.Instance.ValidateConfig (LoginHelpers.GetCurrentAccountId (), mailServer, theAccount.Credentials);
        }

        public void setTextToRed (UITextField[] whichFields)
        {
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

        public bool canUserConnect ()
        {
            if (!haveEnteredEmailAndPass ()) {
                return false;
            }

            if (!isValidEmail (emailText.Text)) {
                ConfigureView (LoginStatus.InvalidEmail);
                return false;
            }

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection()) {
                ConfigureView (LoginStatus.NoNetwork);
                return false;
            }
            return true;
        }

        bool isValidEmail (string email)
        {
            RegexUtilities regexUtil = new RegexUtilities ();
            return regexUtil.IsValidEmail (email);
        }

        protected bool IsValidHost (string host)
        {
            UriHostNameType fullServerUri = Uri.CheckHostName (host.Trim());
            if(fullServerUri == UriHostNameType.Dns  ||
                fullServerUri == UriHostNameType.IPv4 ||
                fullServerUri == UriHostNameType.IPv6) {
                return true;
            }
            return false;
        }

        protected bool IsValidPort (int port) 
        {
            if (port < 0 || port > 65535) {
                return false;
            } else {
                return true;
            }
        }

        protected bool IsValidServer (string server)
        {
            if (IsValidHost (server)) {
                return true;
            }

            //fullServerUri didn't pass...validate host/port separately
            Uri serverURI;
            try{
                serverURI = new Uri ("my://" + serverText.Text.Trim ());
            }catch {
                ConfigureView (LoginStatus.InvalidServerName);
                return false;
            }

            var host = serverURI.Host;
            var port = serverURI.Port;

            if (!IsValidHost (host)) {
                ConfigureView (LoginStatus.InvalidServerName);
                return false;
            }

            //host cleared, checking port
            if (!IsValidPort(port)) {
                ConfigureView (LoginStatus.InvalidServerName);
                return false;
            }

            return true;
        }

        protected void SetHostAndPort(McServer forServer)
        {
            NcAssert.True (IsValidServer (serverText.Text), "Server is not valid");

            if(IsValidHost(serverText.Text)){
                forServer.Host = serverText.Text.Trim ();
                forServer.Port = 443;
                return;
            }

            Uri serverURI = new Uri ("my://" + serverText.Text.Trim ());
            forServer.Host = serverURI.Host;
            forServer.Port = serverURI.Port;
        }

        public void stopBeIfRunning ()
        {
            BackEnd.Instance.Stop (LoginHelpers.GetCurrentAccountId ());
        }

        public void startBe ()
        {
            BackEndAutoDStateEnum backEndState = BackEnd.Instance.AutoDState (LoginHelpers.GetCurrentAccountId ());

            if (BackEndAutoDStateEnum.CredWait == backEndState) {
                BackEnd.Instance.CredResp (LoginHelpers.GetCurrentAccountId ());
            } else {
                BackEnd.Instance.Stop (LoginHelpers.GetCurrentAccountId ());
                BackEnd.Instance.Start (LoginHelpers.GetCurrentAccountId ());
            }
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
            LayoutView ();
            scrollView.SetContentOffset (new PointF (0, -scrollView.ContentInset.Top), false);
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Info_EmailMessageSetChanged Status Ind (AdvancedView)");
                LoginHelpers.SetFirstSyncCompleted (LoginHelpers.GetCurrentAccountId (), true);
                if (!hasSyncedEmail) {
                    waitScreen.Layer.RemoveAllAnimations ();
                    waitScreen.StartSyncedEmailAnimation ();
                    hasSyncedEmail = true;
                }
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Auto-D-Completed Status Ind (Advanced View)");
                waitScreen.SetLoadingText ("Syncing Your Inbox...");
                theAccount.Server = McServer.QueryByAccountId<McServer> (LoginHelpers.GetCurrentAccountId()).FirstOrDefault();
                serverText.Text = theAccount.Server.Host;
                LoginHelpers.SetAutoDCompleted (LoginHelpers.GetCurrentAccountId (), true);
                if(!LoginHelpers.HasViewedTutorial(LoginHelpers.GetCurrentAccountId())){
                    PerformSegue(StartupViewController.NextSegue(), this);
                }
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                ConfigureView (LoginStatus.NoNetwork);
                waitScreen.DismissView ();
                stopBeIfRunning ();
            }
            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "Validate Config Successful Status Ind (Advanced View)");
                ConfigureView (LoginStatus.ValidateSuccessful);
                loadSettingsForAccount ();
                startBe ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                ConfigureView (LoginStatus.BadServer);
                waitScreen.DismissView ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                ConfigureView (LoginStatus.BadCredentials);
                waitScreen.DismissView ();
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                ConfigureView (LoginStatus.BadUsername);
                waitScreen.DismissView ();
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "ServerConfReq Status Ind (Adv. View)");
                ConfigureView (LoginStatus.ServerConf);
                waitScreen.DismissView ();
                stopBeIfRunning ();
            }
            if (NcResult.SubKindEnum.Info_CredReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "CredReqCallback Status Ind (Adv. View)");
                ConfigureView (LoginStatus.BadCredentials);
                waitScreen.DismissView ();
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "CertAskCallback Status Ind");
                ConfigureView (LoginStatus.AcceptCertificate);
                certificateCallbackHandler ();
            }
        }

        public void certificateCallbackHandler ()
        {
            setTextToRed (new UITextField[]{ });
            certificateView.SetCertificateInformation ();
            certificateView.ShowView ();
            waitScreen.InvalidateAutomaticSegueTimer ();
        }

        public void DontAcceptCertificate ()
        {
            ConfigureView (LoginStatus.EnterInfo);
        }

        public void AcceptCertificate ()
        {
            ConfigureView (LoginStatus.AcceptCertificate);
            NcApplication.Instance.CertAskResp (LoginHelpers.GetCurrentAccountId (), true);
            waitScreen.InitializeAutomaticSegueTimer ();
        }

        public class AccountSettings
        {
            public string EmailAddress { get; set; }

            public McAccount Account { get; set; }

            public McCred Credentials { get; set; }

            public McServer Server { get; set; }

            public AccountSettings ()
            {

            }
        }
    }
}

