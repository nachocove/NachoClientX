// This file has been autogenerated from a class added in the UI designer.

using System;
using Foundation;
using UIKit;
using CoreGraphics;
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
        protected nfloat CELL_HEIGHT = 44;

        UILabel errorMessage;

        UIButton connectButton;
        UIButton customerSupportButton;
        UIButton advancedButton;
        UIButton restartButton;

        ExchangeFields exchangeFields;

        UIScrollView scrollView;
        UIView contentView;
        nfloat yOffset;

        private bool hasSyncedEmail = false;

        string gOriginalPassword = "";

        AccountSettings theAccount;

        public UIView loadingCover;
        private WaitingScreen waitScreen;
        private CertificateView certificateView;

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
            TouchConnect,
        };

        McAccount presetAccount = null;
        string presetEmailAddress = "";
        string presetPassword = "";

        bool showAdvanced = false;
        bool stayInAdvanced = false;

        public void SetAdvanced (string emailAddress, string password)
        {
            presetEmailAddress = emailAddress;
            presetPassword = password;
            showAdvanced = true;
        }

        public void SetAccount (McAccount account)
        {
            presetAccount = account;
        }

        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            theAccount = new AccountSettings ();
            theAccount.Account = presetAccount;
               
            CreateView ();


            waitScreen = new WaitingScreen (View.Frame);
            waitScreen.SetOwner (this);
            waitScreen.CreateView ();
            View.Add (waitScreen);

            certificateView = new CertificateView (View.Frame);
            certificateView.SetOwner (this);
            certificateView.CreateView ();
            View.Add (certificateView);

            RefreshTheAccount ();
            RefreshUI ();

            // Preset only if we haven't got an account set up yet
            if (String.IsNullOrEmpty (exchangeFields.emailText)) {
                if (null == theAccount.Account) {
                    exchangeFields.emailText = presetEmailAddress;
                }
            }
            if (String.IsNullOrEmpty (exchangeFields.passwordText)) {
                if (null == theAccount.Account) {
                    exchangeFields.passwordText = presetPassword;
                    gOriginalPassword = presetPassword;
                }
            }
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
        }

        public override void ViewDidAppear (bool animated)
        {
            if (null != theAccount.Credentials) {
                showAdvanced |= theAccount.Credentials.UserSpecifiedUsername;
            }
            if (null != theAccount.Server) {
                showAdvanced |= (null != theAccount.Server.UserSpecifiedServerName);
            }
 
            // Layout before waitScreen.ShowView() hides the nav bar
            LayoutView ();

            if (!stayInAdvanced && IsBackEndRunning ()) {
                if (IsAutoDComplete ()) {
                    handleStatusEnums ();
                } else {
                    waitScreen.ShowView ();
                }
            } else {
                NavigationItem.Title = "Account Setup";
                loadingCover.Hidden = true;
                handleStatusEnums ();
            }

            base.ViewDidAppear (animated);
        }

        public override bool ShouldAutorotate ()
        {
            return false;
        }

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToSupport")) {
                // On return, don't automatically
                // restart the waiting cover view.
                stayInAdvanced = true;
                return;
            }
            if (segue.Identifier.Equals ("SegueToHome")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToTabController")) {
                return;
            }
        }

        private void CreateView ()
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
            errorMessage = new UILabel (new CGRect (20, 15, View.Frame.Width - 40, 50));
            errorMessage.Font = A.Font_AvenirNextRegular17;
            errorMessage.BackgroundColor = A.Color_NachoNowBackground;
            errorMessage.TextColor = A.Color_NachoRed;
            errorMessage.Lines = 2;
            errorMessage.TextAlignment = UITextAlignment.Center;
            contentView.AddSubview (errorMessage);

            yOffset = errorMessage.Frame.Bottom + 15;

            exchangeFields = new ExchangeFields (new CGRect (0, yOffset, View.Frame.Width, 0), EmailOrPasswordChanged);
            contentView.AddSubview (exchangeFields);

            yOffset += exchangeFields.Frame.Height;

            yOffset += 25;

            connectButton = new UIButton (new CGRect (25, yOffset, View.Frame.Width - 50, 46));
            connectButton.AccessibilityLabel = "Connect";
            connectButton.BackgroundColor = A.Color_NachoTeal;
            connectButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            connectButton.SetTitle ("Connect", UIControlState.Normal);
            connectButton.TitleLabel.TextColor = UIColor.White;
            connectButton.TitleLabel.Font = A.Font_AvenirNextDemiBold17;
            connectButton.Layer.CornerRadius = 4f;
            connectButton.Layer.MasksToBounds = true;
            connectButton.TouchUpInside += onConnect;

            contentView.AddSubview (connectButton);

            yOffset = connectButton.Frame.Bottom + 15;

            advancedButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            advancedButton.AccessibilityLabel = "Advanced Sign In";
            advancedButton.BackgroundColor = A.Color_NachoNowBackground;
            advancedButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
            advancedButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            advancedButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            advancedButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                showAdvanced = true;
                handleStatusEnums ();
            };
            contentView.AddSubview (advancedButton);
            yOffset = advancedButton.Frame.Bottom + 20;

            customerSupportButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            customerSupportButton.AccessibilityLabel = "Customer Support";
            customerSupportButton.BackgroundColor = A.Color_NachoNowBackground;
            customerSupportButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            customerSupportButton.SetTitle ("Customer Support", UIControlState.Normal);
            customerSupportButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            customerSupportButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            customerSupportButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                PerformSegue ("SegueToSupport", this);
            };
            contentView.AddSubview (customerSupportButton);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            restartButton = new UIButton (new CGRect (50, yOffset, View.Frame.Width - 100, 20));
            restartButton.AccessibilityLabel = "Start Over";
            restartButton.BackgroundColor = A.Color_NachoNowBackground;
            restartButton.TitleLabel.TextAlignment = UITextAlignment.Center;
            restartButton.SetTitle ("Start Over", UIControlState.Normal);
            restartButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            restartButton.TitleLabel.Font = A.Font_AvenirNextRegular14;
            restartButton.TouchUpInside += (object sender, EventArgs e) => {
                View.EndEditing (true);
                onStartOver ();
            };
            contentView.AddSubview (restartButton);
            yOffset = restartButton.Frame.Bottom + 20;

            loadingCover = new UIView (View.Frame);
            loadingCover.BackgroundColor = A.Color_NachoGreen;
            contentView.Add (loadingCover);

        }

        void onStartOver ()
        {
            stayInAdvanced = false;
            if (null != theAccount.Account) {
                Action action = () => {
                    NcAccountHandler.Instance.RemoveAccount (theAccount.Account.Id);
                    InvokeOnMainThread (() => {
                        // go back to main screen
                        NcUIRedirector.Instance.GoBackToMainScreen ();                        
                    });
                };
                NcTask.Run (action, "RemoveAccount");
            }
        }

        void onConnect (object sender, EventArgs e)
        {
            View.EndEditing (true);

            stayInAdvanced = false;

            // Checks for valid user, password, and server
            if (!canUserConnect ()) {
                return; // error has been displayed
            }

            // Setup the account is there isn't one yet
            var freshAccount = (null == theAccount.Account);

            if (freshAccount) {
                Log.Info (Log.LOG_UI, "avl: onConnect new account");
                var appDelegate = (AppDelegate)UIApplication.SharedApplication.Delegate;
                // FIXME STEVE value of None causes crash.
                theAccount.Account = NcAccountHandler.Instance.CreateAccount (McAccount.AccountServiceEnum.None, exchangeFields.emailText, exchangeFields.passwordText);
                RefreshTheAccount ();
            } 

            // Save the stuff on the screen (pre-validated by canUserConnect())
            exchangeFields.SaveUserSettings (ref theAccount);

            // If only password has changed & backend is in CredWait, do cred resp
            if (!freshAccount) {
                if (!String.Equals (gOriginalPassword, exchangeFields.passwordText, StringComparison.Ordinal)) {
                    Log.Info (Log.LOG_UI, "avl: onConnect retry password");
                    // FIXME STEVE
                    BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                    if (BackEndStateEnum.CredWait == backEndState) {
                        BackEnd.Instance.CredResp (theAccount.Account.Id);
                        waitScreen.SetLoadingText ("Verifying Your Credentials...");
                        waitScreen.ShowView ();
                        return;
                    }
                }
            }

            BackEnd.Instance.Stop (theAccount.Account.Id);

            // A null server record will re-start auto-d on Backend.Start()
            // Delete the server record if the user didn't enter the server name
            if ((null != theAccount.Server) && (null == theAccount.Server.UserSpecifiedServerName)) {
                exchangeFields.DeleteTheServer (ref theAccount, "onConnect");
            }
            waitScreen.SetLoadingText ("Verifying Your Server...");
            BackEnd.Instance.Start (theAccount.Account.Id);
            waitScreen.ShowView ();
        }

        protected void ConfigureView (LoginStatus currentStatus, string nuance = "")
        {
            haveEnteredEmailAndPass ();
            exchangeFields.RefreshTheServer (ref theAccount);

            switch (currentStatus) {
            case LoginStatus.BadCredentials:
                errorMessage.Text = "There seems to be a problem with your credentials.";
                errorMessage.TextColor = A.Color_NachoRed;
                exchangeFields.HighlightEverything ();
                break;
            case LoginStatus.ValidateSuccessful:
                exchangeFields.ClearHighlights ();
                break;
            case LoginStatus.InvalidEmail:
                if (String.IsNullOrEmpty (nuance)) {
                    errorMessage.Text = "The email address you entered is not formatted correctly.";
                } else {
                    errorMessage.Text = nuance;
                }
                errorMessage.TextColor = A.Color_NachoRed;
                exchangeFields.HighlightEmailError ();
                break;
            case LoginStatus.AcceptCertificate:
                errorMessage.Text = "Accept Certificate?";
                errorMessage.TextColor = A.Color_NachoGreen;
                break;
            case LoginStatus.BadServer:
                errorMessage.Text = "The server name you entered is not valid. Please fix and try again.";
                errorMessage.TextColor = A.Color_NachoRed;
                exchangeFields.HighlightServerError ();
                break;
            case LoginStatus.ServerConf:
                string messagePrefix = null;
                if (nuance == "ServerError") {
                    messagePrefix = "We had a problem connecting to the server";
                } else {
                    messagePrefix = "We had a problem finding the server";
                }
                if (null == theAccount.Server) {
                    errorMessage.Text = messagePrefix + " for '" + theAccount.Account.EmailAddr + "'.";
                } else if (null == theAccount.Server.UserSpecifiedServerName) {
                    errorMessage.Text = messagePrefix + "'" + theAccount.Server.Host + "'.";
                } else {
                    errorMessage.Text = messagePrefix + "'" + theAccount.Server.UserSpecifiedServerName + "'.";
                }
                errorMessage.TextColor = A.Color_NachoRed;
                if (!String.IsNullOrEmpty (exchangeFields.serverText)) {
                    exchangeFields.HighlightServerError ();
                } else {
                    exchangeFields.HighlightEmailError ();
                }
                showAdvanced = true;
                break;
            case LoginStatus.EnterInfo:
                errorMessage.Text = "Please fill out the required credentials.";
                errorMessage.TextColor = A.Color_NachoGreen;
                exchangeFields.ClearHighlights ();
                break;
            case LoginStatus.TouchConnect:
                errorMessage.Text = "Touch Connect to continue";
                errorMessage.TextColor = A.Color_NachoGreen;
                exchangeFields.ClearHighlights ();
                break;
            case LoginStatus.BadUsername:
                errorMessage.TextColor = A.Color_NachoRed;
                if (!String.IsNullOrEmpty (exchangeFields.usernameText)) {
                    exchangeFields.HighlightUsernameError ();
                    errorMessage.Text = "There seems to be a problem with your user name.";
                } else {
                    exchangeFields.HighlightEmailError ();
                    errorMessage.Text = "There seems to be a problem with your email.";
                }
                break;
            case LoginStatus.NoNetwork:
                errorMessage.Text = "No network connection. Please check that you have internet access.";
                errorMessage.TextColor = A.Color_NachoRed;
                exchangeFields.ClearHighlights ();
                break;
            case LoginStatus.InvalidServerName:
                if (String.IsNullOrEmpty (nuance)) {
                    errorMessage.Text = "Invalid server name. Please check that you typed it in correctly.";
                } else {
                    errorMessage.Text = nuance;
                }
                errorMessage.TextColor = A.Color_NachoRed;
                exchangeFields.HighlightServerError ();
                break;
            }

            Log.Info (Log.LOG_UI, "avl: status={0} {1}", currentStatus, errorMessage.Text);

            LayoutView ();
        }

        /// <summary>
        /// The user hits the Advanced Login button on the wait screen
        /// </summary>
        public void ReturnToAdvanceView ()
        {
            showAdvanced = true;
            stayInAdvanced = true;
            handleStatusEnums ();
            waitScreen.DismissView ();
        }

        public void SegueToSupport ()
        {
            waitScreen.DismissView ();
            PerformSegue ("SegueToSupport", this);
        }

        void LayoutView ()
        {
            yOffset = 15f;

            ViewFramer.Create (errorMessage).Y (yOffset);
            yOffset = errorMessage.Frame.Bottom + 15;

            exchangeFields.Layout ();
            yOffset += exchangeFields.Frame.Height;

            ViewFramer.Create (connectButton).Y (yOffset);
            yOffset = connectButton.Frame.Bottom + 20;

            if (showAdvanced) {
                advancedButton.Hidden = true;
            } else {
                advancedButton.Hidden = false;
                ViewFramer.Create (advancedButton).Y (yOffset);
                yOffset = advancedButton.Frame.Bottom + 20;
            }

            ViewFramer.Create (customerSupportButton).Y (yOffset);
            yOffset = customerSupportButton.Frame.Bottom + 20;

            ViewFramer.Create (restartButton).Y (yOffset);
            yOffset = restartButton.Frame.Bottom + 20;

            scrollView.Frame = new CGRect (0, 0, View.Frame.Width, View.Frame.Height - keyboardHeight);
            var contentFrame = new CGRect (0, 0, View.Frame.Width, yOffset);
            contentView.Frame = contentFrame;
            scrollView.ContentSize = contentFrame.Size;
        }

        private void EmailOrPasswordChanged (UITextField textField)
        {
            haveEnteredEmailAndPass ();
        }

        private bool haveEnteredEmailAndPass ()
        {
            if (0 == exchangeFields.emailText.Length || 0 == exchangeFields.passwordText.Length) {
                connectButton.Enabled = false;
                connectButton.Alpha = .5f;
                return false;
            } else {
                connectButton.Enabled = true;
                connectButton.Alpha = 1.0f;
                return true;
            }
        }

        private void handleStatusEnums ()
        {
            if (!IsTheAccountSet ()) {
                Log.Info (Log.LOG_UI, "avl: handleStatusEnums account not set");
                ConfigureView (LoginStatus.EnterInfo);
                haveEnteredEmailAndPass ();
                return;
            }

            var accountId = theAccount.Account.Id;
            // FIXME STEVE
            BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (accountId, McAccount.AccountCapabilityEnum.EmailSender);
            Log.Info (Log.LOG_UI, "avl: handleStatusEnums {0}={1}", accountId, backEndState);

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                Log.Info (Log.LOG_UI, "avl: handleStatusEnums no network connection");
                ConfigureView (LoginStatus.NoNetwork);
                waitScreen.DismissView ();
                stopBeIfRunning (accountId);
            }

            switch (backEndState) {
            case BackEndStateEnum.ServerConfWait:
                Log.Info (Log.LOG_UI, "avl: ServerConfWait Auto-D-State-Enum On Page Load");
                stopBeIfRunning (accountId);
                ConfigureView (LoginStatus.ServerConf);
                waitScreen.DismissView ();
                break;
            case BackEndStateEnum.CredWait:
                Log.Info (Log.LOG_UI, "avl: CredWait Auto-D-State-Enum On Page Load");
                ConfigureView (LoginStatus.BadCredentials);
                waitScreen.DismissView ();
                break;
            case BackEndStateEnum.CertAskWait:
                Log.Info (Log.LOG_UI, "avl: CertAskWait Auto-D-State-Enum On Page Load");
                ConfigureView (LoginStatus.AcceptCertificate);
                certificateCallbackHandler (accountId);
                waitScreen.ShowView ();
                break;
            case BackEndStateEnum.PostAutoDPreInboxSync:
                Log.Info (Log.LOG_UI, "avl: PostAutoDPreInboxSync Auto-D-State-Enum On Page Load");
                ConfigureView (LoginStatus.TouchConnect);
                if (!stayInAdvanced) {
                    waitScreen.SetLoadingText ("Syncing Your Inbox...");
                    waitScreen.ShowView ();
                }
                break;
            case BackEndStateEnum.PostAutoDPostInboxSync:
                Log.Info (Log.LOG_UI, "avl: PostAutoDPostInboxSync Auto-D-State-Enum On Page Load");
                LoginHelpers.SetFirstSyncCompleted (accountId, true);
                TryToFinishUp ();
                break;
            case BackEndStateEnum.Running:
                Log.Info (Log.LOG_UI, "avl: Running Auto-D-State-Enum On Page Load");
                ConfigureView (LoginStatus.TouchConnect);
                if (!stayInAdvanced) {
                    waitScreen.ShowView ();
                }
                break;
            default:
                ConfigureView (LoginStatus.EnterInfo);
                waitScreen.DismissView ();
                break;
            }
        }

        /// <summary>
        /// Refreshs the account.  These are static for the life of this view
        /// </summary>
        private void RefreshTheAccount ()
        {
            if (null != theAccount.Account) {
                // Reload the currently active account record
                var accountId = theAccount.Account.Id;
                theAccount.Account = McAccount.QueryById<McAccount> (accountId);
                theAccount.Credentials = McCred.QueryByAccountId<McCred> (accountId).SingleOrDefault ();
                gOriginalPassword = theAccount.Credentials.GetPassword ();
                Log.Info (Log.LOG_UI, "avl: refresh the account");
            }
        }

        void RefreshUI ()
        {
            errorMessage.Text = "";
            gOriginalPassword = "";
        }

        private bool canUserConnect ()
        {
            if (!haveEnteredEmailAndPass ()) {
                return false;
            }
            string serviceName;
            var emailAddress = exchangeFields.emailText;
            if (EmailHelper.IsServiceUnsupported (emailAddress, out serviceName)) {
                var nuance = String.Format ("Nacho Mail does not support {0} yet.", serviceName);
                ConfigureView (LoginStatus.InvalidEmail, nuance);
                return false;
            }
            if (!String.IsNullOrEmpty (exchangeFields.serverText)) {
                if (EmailHelper.ParseServerWhyEnum.Success_0 != EmailHelper.IsValidServer (exchangeFields.serverText)) {
                    ConfigureView (LoginStatus.InvalidServerName);
                    return false;
                }
            }
            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                ConfigureView (LoginStatus.NoNetwork);
                return false;
            }
            return true;
        }

        private void stopBeIfRunning (int accountId)
        {
            BackEnd.Instance.Stop (accountId);
        }

        bool IsBackEndRunning ()
        {
            if (null == theAccount.Account) {
                return false;
            }
            NcAssert.True (IsTheAccountSet ());
            // FIXME STEVE
            BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            Log.Info (Log.LOG_UI, "avl:  isrunning state {0}", backEndState);
            if (BackEndStateEnum.NotYetStarted == backEndState) {
                return true;
            }
            if (BackEndStateEnum.Running == backEndState) {
                return true;
            }
            return IsAutoDComplete ();
        }

        bool IsAutoDComplete ()
        {
            if (null == theAccount.Account) {
                return false;
            }
            NcAssert.True (IsTheAccountSet ());
            // FIXME STEVE
            BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            if (BackEndStateEnum.PostAutoDPostInboxSync == backEndState) {
                return true;
            }
            if (BackEndStateEnum.PostAutoDPreInboxSync == backEndState) {
                return true;
            }
            return false;
        }

        bool IsTheAccountSet ()
        {
            return (null != theAccount.Account);
        }

        int GetTheAccountId ()
        {
            NcAssert.True (IsTheAccountSet ());
            return theAccount.Account.Id;
        }

        protected override void OnKeyboardChanged ()
        {
            LayoutView ();
            scrollView.SetContentOffset (new CGPoint (0, -scrollView.ContentInset.Top), false);
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            // Can't do anything without an account
            if ((null == theAccount) || (null == theAccount.Account)) {
                return;
            }

            // Won't do anything if this isn't our account
            if ((null != s.Account) && (s.Account.Id != theAccount.Account.Id)) {
                return;
            }

            int accountId = theAccount.Account.Id;

            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_EmailMessageSetChanged Status Ind (AdvancedView)");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Info_InboxPingStarted == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_InboxPingStarted Status Ind (AdvancedView)");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Auto-D-Completed Status Ind (Advanced View)");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Error_NetworkUnavailable");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: ServerConfReq Status Ind (Adv. View)");
                ConfigureView (LoginStatus.ServerConf, nuance: s.Status.Why.ToString ());
                waitScreen.DismissView ();
                stopBeIfRunning (accountId);
            }
            if (NcResult.SubKindEnum.Info_CredReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CredReqCallback Status Ind (Adv. View)");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CertAskCallback Status Ind");
                handleStatusEnums ();
            }
            if (NcResult.SubKindEnum.Info_NetworkStatus == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Info_NetworkStatus");
                handleStatusEnums ();
            }
        }

        private void certificateCallbackHandler (int accountId)
        {
            exchangeFields.ClearHighlights ();
            certificateView.SetCertificateInformation (accountId);
            certificateView.ShowView ();
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            ConfigureView (LoginStatus.EnterInfo);
            // FIXME STEVE - need to deal with > 1 server scenarios (McAccount.AccountCapabilityEnum).
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            waitScreen.DismissView ();
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            ConfigureView (LoginStatus.AcceptCertificate);
            // FIXME STEVE - need to deal with > 1 server scenarios (McAccount.AccountCapabilityEnum).
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
        }

        private void SyncCompleted (int accountId)
        {
            LoginHelpers.SetFirstSyncCompleted (accountId, true);
            if (!hasSyncedEmail) {
                waitScreen.Layer.RemoveAllAnimations ();
                waitScreen.StartSyncedEmailAnimation (accountId);
                hasSyncedEmail = true;
            }
        }

        public void FinishedSyncedEmailAnimation (int accountId)
        {
            TryToFinishUp ();
        }

        void TryToFinishUp ()
        {
            if (LoginHelpers.HasViewedTutorial ()) {
                if (null == NcApplication.Instance.Account) {
                    // FIXME: There ought to be a better way
                    NcApplication.Instance.Account = theAccount.Account;
                }
                PerformSegue ("SegueToTabController", this);
            } else {
                PerformSegue ("SegueToHome", this);
            }
        }

        public class AccountSettings
        {
            public McAccount Account { get; set; }

            public McCred Credentials { get; set; }

            public McServer Server { get; set; }
        }
    }


}

