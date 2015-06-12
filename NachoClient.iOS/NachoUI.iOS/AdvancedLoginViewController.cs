// This file has been autogenerated from a class added in the UI designer.

using System;
using Foundation;
using UIKit;
using CoreGraphics;
using CoreAnimation;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using System.Linq;
using System.Collections.Generic;
using NachoPlatform;
using Google.iOS;

namespace NachoClient.iOS
{
    public partial class AdvancedLoginViewController : NcUIViewController, INachoCredentialsDelegate, INachoCertificateResponderParent, IGIDSignInDelegate, IGIDSignInUIDelegate
    {
        protected nfloat CELL_HEIGHT = 44;

        UILabel errorMessage;

        UIButton customerSupportButton;
        UIButton restartButton;

        ILoginFields loginFields;

        UIScrollView scrollView;
        UIView contentView;
        nfloat yOffset;

        private bool hasSyncedEmail = false;

        string gOriginalPassword = "";

        AccountSettings theAccount;

        private WaitingScreen waitScreen;
        private CertificateView certificateView;

        public delegate void onConnectCallback ();

        public enum LoginStatus
        {
            OK,
            InvalidEmailAddress,
            InvalidServerName,
            InvalidPortNumber,
            BadCredentials,
            AcceptCertificate,
            ServerConf,
            NoNetwork,
            EnterInfo,
        };

        bool stayInAdvanced = false;
        bool googleSignInIsActive = false;

        McAccount.AccountServiceEnum service;

        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
            service = McAccount.AccountServiceEnum.None;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            Log.Info (Log.LOG_UI, "avl: ViewDidLoad");

            waitScreen = new WaitingScreen (View.Frame, this);
            waitScreen.Hidden = true;
            View.Add (waitScreen);

            certificateView = new CertificateView (View.Frame, this);
            View.Add (certificateView);

            View.BackgroundColor = A.Color_NachoGreen;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);

            Log.Info (Log.LOG_UI, "avl: ViewWillAppear");

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                if (this.NavigationController.NavigationBarHidden == true) {
                    this.NavigationController.NavigationBarHidden = false; 
                }
                NavigationItem.SetHidesBackButton (true, false);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;

            if (HaveServiceAndAccount ()) {
                ConfigurePostServiceChoice ();
            }
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
            base.ViewDidAppear (animated);

            Log.Info (Log.LOG_UI, "avl: ViewDidAppear");

            if (!googleSignInIsActive) {
                PromptUserForServiceAndAccount ();
            }
        }

        bool HaveServiceAndAccount ()
        {
            if (McAccount.AccountServiceEnum.None == service) {
                return false;
            }
            return (null != theAccount);
        }

        // ConfigureServiceChoice is pushing other views,
        // which we want to happen after the view is visible
        // so we don't get overlapping animations and crashes.
        void PromptUserForServiceAndAccount ()
        {
            // Step 1, make sure we have a service
            if (McAccount.AccountServiceEnum.None == service) {
                Log.Info (Log.LOG_UI, "avl: configure account type");
                PerformSegue ("SegueToAccountType", this);
                return;
            }

            // Step 2, for GMail
            if (McAccount.AccountServiceEnum.GoogleDefault == service) {
                Log.Info (Log.LOG_UI, "avl: PromptUserForServiceAndAccount service type is google");
                StartGoogleSignIn ();
                return;
            }

            // Step 2, get email & password, and/or advanced selection
            if (null == theAccount) {
                Log.Info (Log.LOG_UI, "avl: PromptUserForServiceAndAccount ask for credentials");
                PerformSegue ("SegueToAccountCredentials", this);
                return;
            }

            Log.Info (Log.LOG_UI, "avl: PromptUserForServiceAndAccount have service and account");
        }

        protected void ConfigurePostServiceChoice ()
        {
            Log.Info (Log.LOG_UI, "avl: configure");

            NavigationItem.Title = "Account Setup";

            handleStatusEnums ();
        }

        // Step 1 callback
        // AccountTypeViewController ServiceSelectedCallback ("SegueToAccountType")
        protected void ServiceSelected (McAccount.AccountServiceEnum service)
        {
            this.service = service;
        }

        // Step 2 callback
        // INachoCredentialsDelegate might just be asking to show advanced without any credentials.
        public void CredentialsDismissed (UIViewController vc, bool startInAdvanced, string email, string password)
        {
            CreateView ();

            theAccount = new AccountSettings ();

            loginFields.emailText = email;
            loginFields.passwordText = password;
            loginFields.showAdvanced = startInAdvanced;

            LayoutView ();

            if (!startInAdvanced) {
                // Start the process
                onConnect ();
            }
        }

        void onConnect ()
        {
            View.EndEditing (true);

            stayInAdvanced = false;

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                UpdateErrorMessage (LoginStatus.NoNetwork);
                return;
            }

            // Checks for valid user, password, and server
            string nuance;
            var loginStatus = loginFields.CanUserConnect (out nuance);
            if (LoginStatus.OK != loginStatus) {
                UpdateErrorMessage (loginStatus, nuance);
                return;
            }

            // Setup the account is there isn't one yet
            var freshAccount = (null == theAccount.Account);

            if (freshAccount) {
                Log.Info (Log.LOG_UI, "avl: onConnect new account");
                theAccount.Account = loginFields.CreateAccount ();
                RefreshTheAccount ();
            } 

            // Save the stuff on the screen (pre-validated by canUserConnect())
            loginFields.SaveUserSettings (ref theAccount);

            // If only password has changed & backend is in CredWait, do cred resp
            if (!freshAccount) {
                if (!String.Equals (gOriginalPassword, loginFields.passwordText, StringComparison.Ordinal)) {
                    Log.Info (Log.LOG_UI, "avl: onConnect retry password");
                    // FIXME STEVE
                    BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (theAccount.Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                    if (BackEndStateEnum.CredWait == backEndState) {
                        BackEnd.Instance.CredResp (theAccount.Account.Id);
                        waitScreen.ShowView ("Verifying Your Credentials...");
                        return;
                    }
                }
            }
                
            BackEnd.Instance.Stop (theAccount.Account.Id);

            // A null server record will re-start auto-d on Backend.Start()
            // Delete the server record if the user didn't enter the server name
            loginFields.MaybeDeleteTheServer ();

            BackEnd.Instance.Start (theAccount.Account.Id);

            waitScreen.ShowView ("Verifying Your Server...");
        }

        void onStartOver ()
        {
            if (null == theAccount.Account) {
                StartOver ();
                return;
            }

            Action action = () => {
                NcAccountHandler.Instance.RemoveAccount (theAccount.Account.Id);
                InvokeOnMainThread (() => {
                    StartOver ();                       
                });
            };
            NcTask.Run (action, "RemoveAccount");
        }

        protected void StartOver ()
        {
            stayInAdvanced = false;
            loginFields = null;
            theAccount = null;
            service = McAccount.AccountServiceEnum.None;
            scrollView.RemoveFromSuperview ();
            NavigationItem.Title = "Account Setup";
            PromptUserForServiceAndAccount ();
        }

        protected void UpdateErrorMessage (LoginStatus currentStatus, string nuance = "")
        {
            switch (currentStatus) {
            case LoginStatus.BadCredentials:
                errorMessage.Text = "There seems to be a problem with your credentials.";
                errorMessage.TextColor = A.Color_NachoRed;
                loginFields.HighlightCredentials ();
                break;
            case LoginStatus.InvalidEmailAddress:
                if (String.IsNullOrEmpty (nuance)) {
                    errorMessage.Text = "The email address you entered is not formatted correctly.";
                } else {
                    errorMessage.Text = nuance;
                }
                errorMessage.TextColor = A.Color_NachoRed;
                loginFields.HighlightEmailError ();
                break;
            case LoginStatus.AcceptCertificate:
                errorMessage.Text = "Accept Certificate?";
                errorMessage.TextColor = A.Color_NachoGreen;
                break;
            case LoginStatus.ServerConf:
                string messagePrefix = null;
                if (nuance == "ServerError") {
                    messagePrefix = "We had a problem connecting to the server";
                } else {
                    messagePrefix = "We had a problem finding the server";
                }
                errorMessage.Text = loginFields.GetServerConfMessage (theAccount, messagePrefix);
                errorMessage.TextColor = A.Color_NachoRed;
                loginFields.HighlightServerConfError ();
                loginFields.showAdvanced = true;
                LayoutView ();
                break;
            case LoginStatus.EnterInfo:
                errorMessage.Text = "Please fill out the required credentials.";
                errorMessage.TextColor = A.Color_NachoGreen;
                loginFields.ClearHighlights ();
                break;
            case LoginStatus.OK:
                errorMessage.Text = "Touch Connect to continue";
                errorMessage.TextColor = A.Color_NachoGreen;
                loginFields.ClearHighlights ();
                break;
            case LoginStatus.NoNetwork:
                errorMessage.Text = "No network connection. Please check that you have internet access.";
                errorMessage.TextColor = A.Color_NachoRed;
                loginFields.ClearHighlights ();
                break;
            case LoginStatus.InvalidServerName:
                if (String.IsNullOrEmpty (nuance)) {
                    errorMessage.Text = "Invalid server name. Please check that you typed it in correctly.";
                } else {
                    errorMessage.Text = nuance;
                }
                errorMessage.TextColor = A.Color_NachoRed;
                break;
            case LoginStatus.InvalidPortNumber:
                if (String.IsNullOrEmpty (nuance)) {
                    errorMessage.Text = "Invalid port number. It must be a number.";
                } else {
                    errorMessage.Text = nuance;
                }
                errorMessage.TextColor = A.Color_NachoRed;
                break;
            }
            Log.Info (Log.LOG_UI, "avl: status={0} {1}", currentStatus, errorMessage.Text);
        }

        /// <summary>
        /// The user hits the Advanced Login button on the wait screen
        /// </summary>
        public void ReturnToAdvanceView ()
        {
            stayInAdvanced = true;
            LayoutView ();
            handleStatusEnums ();
            waitScreen.DismissView ();
        }

        public void SegueToSupport ()
        {
            waitScreen.DismissView ();
            PerformSegue ("SegueToSupport", this);
        }

        private void handleStatusEnums ()
        {
            if (!IsTheAccountSet ()) {
                Log.Info (Log.LOG_UI, "avl: handleStatusEnums account not set");
                UpdateErrorMessage (LoginStatus.EnterInfo);
                waitScreen.DismissView ();
                return;
            }

            var accountId = theAccount.Account.Id;

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                Log.Info (Log.LOG_UI, "avl: handleStatusEnums no network connection");
                UpdateErrorMessage (LoginStatus.NoNetwork);
                waitScreen.DismissView ();
                stopBeIfRunning (accountId);
                return;
            }

            var senderState = BackEnd.Instance.BackEndState (accountId, McAccount.AccountCapabilityEnum.EmailSender);
            var readerState = BackEnd.Instance.BackEndState (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);

            Log.Info (Log.LOG_UI, "avl: handleStatusEnums {0} sender={1} reader={2}", accountId, senderState, readerState);

            if ((BackEndStateEnum.ServerConfWait == senderState) || (BackEndStateEnum.ServerConfWait == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums server conf wait");
                stopBeIfRunning (accountId);
                UpdateErrorMessage (LoginStatus.ServerConf);
                waitScreen.DismissView ();
                return;
            }

            if ((BackEndStateEnum.CredWait == senderState) || (BackEndStateEnum.CredWait == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums cred wait");
                stopBeIfRunning (accountId);
                UpdateErrorMessage (LoginStatus.BadCredentials);
                waitScreen.DismissView ();
                return;
            }

            if ((BackEndStateEnum.CertAskWait == senderState) || (BackEndStateEnum.CertAskWait == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums cert ask wait");
                UpdateErrorMessage (LoginStatus.AcceptCertificate);
                certificateCallbackHandler (accountId);
                return;
            }

            if ((BackEndStateEnum.PostAutoDPreInboxSync == senderState) || (BackEndStateEnum.PostAutoDPreInboxSync == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums PostAutoDPreInboxSync");
                UpdateErrorMessage (LoginStatus.OK);
                if (!stayInAdvanced) {
                    waitScreen.ShowView ("Syncing Your Inbox...");
                } else {
                    waitScreen.DismissView ();
                }
                return;
            }

            if ((BackEndStateEnum.PostAutoDPostInboxSync == senderState) || (BackEndStateEnum.PostAutoDPostInboxSync == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums PostAutoDPostInboxSync");
                if ((BackEndStateEnum.PostAutoDPostInboxSync == senderState) && (BackEndStateEnum.PostAutoDPostInboxSync == readerState)) {
                    LoginHelpers.SetFirstSyncCompleted (accountId, true);
                    TryToFinishUp ();
                }
                return;
            }

            if ((BackEndStateEnum.Running == senderState) || (BackEndStateEnum.Running == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums running");
                UpdateErrorMessage (LoginStatus.OK);
                if (!stayInAdvanced) {
                    waitScreen.ShowView ();
                } else {
                    waitScreen.DismissView ();
                }
                return;
            }

            if ((BackEndStateEnum.NotYetStarted == senderState) || (BackEndStateEnum.NotYetStarted == readerState)) {
                // Trust that things will start soon.
                Log.Info (Log.LOG_UI, "avl: status enums notyetstarted");
                waitScreen.ShowView ();
                return;
            }
                
            Log.Info (Log.LOG_UI, "avl: status enums default");
            UpdateErrorMessage (LoginStatus.EnterInfo);
            waitScreen.DismissView ();
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


        private void stopBeIfRunning (int accountId)
        {
            BackEnd.Instance.Stop (accountId);
        }

        bool IsBackEndRunning ()
        {
            if (null == theAccount) {
                return false;
            }
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

        bool IsAutoDComplete (McAccount account, McAccount.AccountCapabilityEnum capability)
        {
            BackEndStateEnum backEndState = BackEnd.Instance.BackEndState (account.Id, capability);
            return (BackEndStateEnum.PostAutoDPostInboxSync == backEndState) && (BackEndStateEnum.PostAutoDPreInboxSync == backEndState);
        }

        bool IsAutoDComplete ()
        {
            if (null == theAccount) {
                return false;
            }
            var account = theAccount.Account;
            return IsAutoDComplete (account, McAccount.AccountCapabilityEnum.EmailSender) && IsAutoDComplete (account, McAccount.AccountCapabilityEnum.EmailReaderWriter);
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
            // Maybe called from keyboard handler because
            // the notification is still alive when account
            // information is being gathered.  Avoid crash!
            if (null == scrollView) {
                return;
            }

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
                SyncCompleted (accountId);
                return;
            }
            if (NcResult.SubKindEnum.Info_InboxPingStarted == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_InboxPingStarted Status Ind (AdvancedView)");
                SyncCompleted (accountId);
                return;
            }
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Auto-D-Completed Status Ind (Advanced View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Error_NetworkUnavailable");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: ServerConfReq Status Ind (Adv. View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Info_CredReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CredReqCallback Status Ind (Adv. View)");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CertAskCallback Status Ind");
                handleStatusEnums ();
                return;
            }
            if (NcResult.SubKindEnum.Info_NetworkStatus == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Info_NetworkStatus");
                handleStatusEnums ();
                return;
            }
        }

        private void certificateCallbackHandler (int accountId)
        {
            loginFields.ClearHighlights ();
            certificateView.SetCertificateInformation (accountId);
            View.BringSubviewToFront (certificateView);
            certificateView.ShowView ();
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            UpdateErrorMessage (LoginStatus.EnterInfo);
            // FIXME STEVE - need to deal with > 1 server scenarios (McAccount.AccountCapabilityEnum).
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            waitScreen.DismissView ();
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            UpdateErrorMessage (LoginStatus.AcceptCertificate);
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
            handleStatusEnums ();
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

        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("SegueToAccountType")) {
                var vc = (AccountTypeViewController)segue.DestinationViewController;
                vc.ServiceSelected = ServiceSelected;
                return;
            }
            if (segue.Identifier.Equals ("SegueToAccountCredentials")) {
                var vc = (AccountCredentialsViewController)segue.DestinationViewController;
                vc.Setup (this, service);
                return;
            }
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

            switch (McAccount.GetAccountType (service)) {
            case McAccount.AccountTypeEnum.IMAP_SMTP:
                loginFields = new IMapFields (new CGRect (0, yOffset, View.Frame.Width, 0), service, onConnect);
                break;
            case McAccount.AccountTypeEnum.Exchange:
                loginFields = new ExchangeFields (new CGRect (0, yOffset, View.Frame.Width, 0), service, onConnect);
                break;
            case McAccount.AccountTypeEnum.Device:
                // FIXME: Do we need anything here?
                break;
            default:
                NcAssert.CaseError (service.ToString ());
                break;
            }
            contentView.AddSubview (loginFields.View);

            yOffset += loginFields.View.Frame.Height;

            yOffset += 25;

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
        }

        void LayoutView ()
        {
            yOffset = 15f;

            ViewFramer.Create (errorMessage).Y (yOffset);
            yOffset = errorMessage.Frame.Bottom + 15;

            if (null != loginFields) {
                loginFields.Layout ();
                yOffset += loginFields.View.Frame.Height;
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

        public override bool ShouldAutorotate ()
        {
            return false;
        }

        [Action ("UnwindAccountCredentialsViewController:")]
        public void UnwindAccountCredentialsViewController (UIStoryboardSegue segue)
        {
            var transition = CATransition.CreateAnimation ();

            transition.Duration = 0.3;
            transition.Type = CATransition.TransitionFade;

            segue.SourceViewController.NavigationController.View.Layer.AddAnimation (transition, CALayer.Transition);
            segue.SourceViewController.NavigationController.PopViewController (false);
        }

        public class AccountSettings
        {
            public McAccount Account { get; set; }

            public McCred Credentials { get; set; }
           
        }

        void StartGoogleSignIn ()
        {
            Google.iOS.GIDSignIn.SharedInstance.Delegate = this;
            Google.iOS.GIDSignIn.SharedInstance.UIDelegate = this;

            // Add scope to give full access to email
            var scopes = Google.iOS.GIDSignIn.SharedInstance.Scopes.ToList ();
            scopes.Add ("https://mail.google.com");
            Google.iOS.GIDSignIn.SharedInstance.Scopes = scopes.ToArray ();

            googleSignInIsActive = true;
            Google.iOS.GIDSignIn.SharedInstance.SignIn ();
        }

        // GIDSignInDelegate
        public void DidSignInForUser (GIDSignIn signIn, GIDGoogleUser user, NSError error)
        {
            Log.Info (Log.LOG_UI, "avl: DidSignInForUser {0}", error);

            googleSignInIsActive = false;

            // TODO: Handle more errors
            if (null != error) {
                if (error.Code == (int)GIDSignInErrorCode.CodeCanceled) {
                    service = McAccount.AccountServiceEnum.None;
                    PromptUserForServiceAndAccount ();
                    return;
                }
                // Error is not set if user cancels the permissions page
                Log.Error (Log.LOG_UI, "avl: DidSignInForUser {0}", error);
                PromptUserForServiceAndAccount ();
                return;
            }

            GoogleDumper (user);

            service = McAccount.AccountServiceEnum.GoogleDefault;

            // TODO: Check for & reject duplicate account.

            var account = NcAccountHandler.Instance.CreateAccount (service,
                user.Profile.Email,
                user.Authentication.AccessToken, 
                user.Authentication.RefreshToken,
                user.Authentication.AccessTokenExpirationDate.ToDateTime ());
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);

            if (null == theAccount) {
                theAccount = new AccountSettings ();
            }
            theAccount.Account = account;

            CreateView ();
            LayoutView ();

            BackEnd.Instance.Stop (theAccount.Account.Id);

            // A null server record will re-start auto-d on Backend.Start()
            // Delete the server record if the user didn't enter the server name
            loginFields.MaybeDeleteTheServer ();

            BackEnd.Instance.Start (theAccount.Account.Id);

            waitScreen.ShowView ("Verifying Your Server...");

            // TODO:
            // 1. Check for dup account
            // 2. Create account & servers
            // 3. Save auth related materials
            // 4. Call silent sign-on somewhere
            // 5. Handle token expiration and renewal
            // 6. Figure out what to do in perform fetch
        }

        public static void GoogleDumper (GIDGoogleUser user)
        {
            if (null == user) {
                Console.WriteLine ("user is null");
                return;
            }
            Console.WriteLine ("user.AccessibleScopes,Length: {0}", user.AccessibleScopes.Length);
            Console.WriteLine ("user.Authentication: {0}", user.Authentication);
            Console.WriteLine ("user.HostedDomain: {0}", user.HostedDomain);
            Console.WriteLine ("user.Profile: {0}", user.Profile);
            Console.WriteLine ("user.ServerAuthCode: {0}", user.ServerAuthCode);
            Console.WriteLine ("user.UserId: {0}", user.UserId);
            var profile = user.Profile;
            if (null == profile) {
                Console.WriteLine ("user.Profile is null");
            } else {
                Console.WriteLine ("profile.Email: {0}", profile.Email);
                Console.WriteLine ("profile.HasImage: {0}", profile.HasImage);
                Console.WriteLine ("profile.ImageURL {0}", profile.ImageURL (20));
                Console.WriteLine ("profile.Name: {0}", profile.Name);
            }
            var auth = user.Authentication;
            if (null == auth) {
                Console.WriteLine ("user.Authentication is null");
            } else {
                Console.WriteLine ("auth.AccessToken: {0}", auth.AccessToken);
                Console.WriteLine ("auth.AccessTokenExpirationDate: {0}", auth.AccessTokenExpirationDate);
                Console.WriteLine ("auth.IdToken: {0}", auth.IdToken);
                Console.WriteLine ("auth.RefreshToken: {0}", auth.RefreshToken);
            }
        }

    }

}

