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
using System.Net.Http;

namespace NachoClient.iOS
{
    public partial class AdvancedLoginViewController : NcUIViewController, ILoginProtocol, INachoCredentialsDelegate, INachoCertificateResponderParent, IGIDSignInDelegate, IGIDSignInUIDelegate
    {
        ILoginFields loginFields;
        WaitingScreen waitingScreen;
        CertificateView certificateView;
        LoginProtocolControl loginProtocolControl;

        string email;
        string password;
        McAccount account;
        McAccount.AccountServiceEnum service;

        public enum ConnectCallbackStatusEnum
        {
            Connect,
            Support,
            StartOver,
            CredResponse,
            DuplicateAccount,
            ContinueToShowAdvanced,
        }

        void RemoveWindows ()
        {
            if (null != loginFields) {
                loginFields.View.RemoveFromSuperview ();
                loginFields = null;
            }
            if (null != waitingScreen) {
                waitingScreen.RemoveFromSuperview ();
                waitingScreen = null;
            }
            if (null != certificateView) {
                certificateView.RemoveFromSuperview ();
                certificateView = null;
            }
        }

        public delegate void onConnectCallback (ConnectCallbackStatusEnum status, McAccount account, string email, string password);

        public delegate void onValidateCallback (McCred creds, List<McServer> servers);

        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
            service = McAccount.AccountServiceEnum.None;
        }

        public override void ViewDidLoad ()
        {
            Log.Info (Log.LOG_UI, "avl: ViewDidLoad");

            base.ViewDidLoad ();

            loginProtocolControl = new LoginProtocolControl (this);

            View.BackgroundColor = A.Color_NachoGreen;
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            NavigationItem.SetHidesBackButton (true, false);
            if (null != NavigationController) {
                NavigationController.SetNavigationBarHidden (false, false);
                NavigationController.NavigationBar.BackgroundColor = A.Color_NachoGreen;
                if (this.NavigationController.RespondsToSelector (new ObjCRuntime.Selector ("interactivePopGestureRecognizer"))) {
                    this.NavigationController.InteractivePopGestureRecognizer.Enabled = false;
                }
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != NavigationController) {
                NavigationController.SetNavigationBarHidden (false, false);
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override bool HidesBottomBarWhenPushed {
            get {
                return true;
            }
        }

        public override void ViewDidAppear (bool animated)
        {
            Log.Info (Log.LOG_UI, "avl: AdvanceLoginViewController ViewDidAppear");

            base.ViewDidAppear (animated);

            if (null == account) {
                // Configus interruptus?
                account = McAccount.GetAccountBeingConfigured ();
                if (null != account) {
                    Log.Info (Log.LOG_UI, "avl: AdvanceLoginViewController reloading account being configured");
                    email = account.EmailAddr;
                    service = account.AccountService;
                    password = LoginHelpers.GetPassword (account);
                    BackEnd.Instance.Start (account.Id);
                }
            }

            if (LoginHelpers.GetGoogleSignInCallbackArrived ()) {
                LoginHelpers.SetGoogleSignInCallbackArrived (false);
                // Account should  be null but just in case
                // don't usurp an in-progress configuration
                if (null == account) {
                    StartGoogleSilentLogin ();
                    return;
                }
            }

            if (McAccount.AccountServiceEnum.None == service) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NoService, "avl: AdvanceLoginViewController ViewDidAppear");
                return;
            }

            // User can visit support from advanced view before an account is created
            if (null == account) {
                switch (service) {
                case McAccount.AccountServiceEnum.Exchange:
                case McAccount.AccountServiceEnum.IMAP_SMTP:
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowAdvanced, "avl: AdvanceLoginViewController ViewDidAppear");
                    return;
                default:
                    return;
                }
            }

            if ((uint)LoginProtocolControl.States.FinishWait == loginProtocolControl.sm.State) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.TryAgain, "avl: AdvanceLoginViewController ViewDidAppear");
                return;
            }

            if ((uint)LoginProtocolControl.States.TutorialSupportWait == loginProtocolControl.sm.State) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AllDone, "avl: AdvanceLoginViewController ViewDidAppear");
                EventFromEnum ();
                return;
            }

            // Kickstart if we are just starting out and we're still in the start state
            if ((uint)LoginProtocolControl.States.Start == loginProtocolControl.sm.State) {
                EventFromEnum ();
                return;
            }

        }

        public void FinishUp ()
        {
            if (null == waitingScreen) {
                waitingScreen = new WaitingScreen (new CGRect (0, 0, View.Frame.Width, View.Frame.Height), this);
                View.AddSubview (waitingScreen);
            } else {
                waitingScreen.Layer.RemoveAllAnimations ();
            }

            if (!LoginHelpers.HasViewedTutorial ()) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowTutorial, "avl: FinishUp");
            } else {
                waitingScreen.StartSyncedEmailAnimation (account.Id);
            }
        }

        public void FinishedSyncedEmailAnimation (int accountId)
        {
            RemoveWindows ();
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AllDone, "avl: FinishedSyncedEmailAnimation");
        }

        public void PromptForService ()
        {
            PerformSegue ("SegueToAccountType", this);
        }

        // Google Apps and Office 365 have a single hard-wired
        // server.  If we get a server conf callback, it means
        // the domain associated with email couldn't be found.
        // In this case, we re-prompt for credentials with an
        // appropriate message.
        public void ShowServerConfCallback ()
        {
            switch (service) {
            case McAccount.AccountServiceEnum.GoogleExchange:
            case McAccount.AccountServiceEnum.Office365Exchange:
                PerformSegue ("SegueToAccountCredentials", new SegueHolder (NachoCredentialsRequestEnum.ServerConfCallback));
                break;
            default:
                ShowAdvancedConfiguration (LoginProtocolControl.Prompt.ServerConf);
                break;
            }
        }

        public void ShowAdvancedConfiguration (LoginProtocolControl.Prompt prompt)
        {
            RemoveWindows ();

            if (null != account) {
                BackEnd.Instance.Stop (account.Id);
            }

            // FIXME: Getting server conf callback for known servers
            var accountType = McAccount.GetAccountType (service);

            if ((service != McAccount.AccountServiceEnum.Exchange) && (service != McAccount.AccountServiceEnum.IMAP_SMTP)) {
                Log.Error (Log.LOG_UI, "avl: Showing advanced view for {0}", service);
            }

            var rect = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            switch (accountType) {
            case McAccount.AccountTypeEnum.Exchange:
                loginFields = new ExchangeFields (account, prompt, email, password, rect, onConnect);
                break;
            case McAccount.AccountTypeEnum.IMAP_SMTP:
                loginFields = new IMapFields (account, prompt, email, password, rect, onConnect);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            View.AddSubview (loginFields.View);
        }

        void onConnect (ConnectCallbackStatusEnum connect, McAccount account, string email, string password)
        {
            View.EndEditing (true);

            this.email = email;
            this.password = password;

            switch (connect) {
            case ConnectCallbackStatusEnum.Connect:
                if (null == this.account) {
                    this.account = account;
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: onConnect");
                } else {
                    this.account = account;
                    BackEnd.Instance.Stop (account.Id);
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NotYetStarted, "avl: onConnect");
                }
                break;
            case ConnectCallbackStatusEnum.StartOver:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.StartOver, "avl: onConnect");
                break;
            case ConnectCallbackStatusEnum.Support:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowSupport, "avl: onConnect");
                break;
            case ConnectCallbackStatusEnum.DuplicateAccount:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.DuplicateAccount, "avl: onConnect");
                break;
            case ConnectCallbackStatusEnum.CredResponse:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CredUpdate, "avl: onConnect");
                break;
            case ConnectCallbackStatusEnum.ContinueToShowAdvanced:
                break;
            }
        }

        public void ShowNoNetwork ()
        {
            NcAlertView.Show (this, null,
                String.Format ("No network connection. Please check that you have internet access."),
                new NcAlertAction ("Try again", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.TryAgain, "avl: ShowNoNetwork");

                }),
                new NcAlertAction ("Cancel", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: ShowNoNetwork");

                }));
            return;
        }

        public void ShowDuplicateAccount ()
        {
            NcAlertView.Show (this, null,
                String.Format ("This account already exists."),
                new NcAlertAction ("OK", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: ShowDuplicateAccount");

                }));
            return;
        }

        public void ShowCertAsk ()
        {
            RemoveWindows ();
            // FIXME: need to pass thru and handle the requested capabilities
            if (NcApplication.Instance.CertAskReqPreApproved (account.Id, McAccount.AccountCapabilityEnum.EmailSender)) {
                AcceptCertificate (account.Id);
                return;
            }
            certificateView = new CertificateView (new CGRect (0, 0, View.Frame.Width, View.Frame.Height), this);
            certificateView.SetCertificateInformation (account.Id, McAccount.AccountCapabilityEnum.EmailSender);
            View.AddSubview (certificateView);
            View.BringSubviewToFront (certificateView);
            certificateView.ShowView ();
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            RemoveWindows ();
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CertRejected, "avl: DontAcceptCertificate");
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            RemoveWindows ();
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CertAccepted, "avl: AcceptCertificate");
        }

        public void ShowCertRejected ()
        {
            NcAlertView.Show (this, null,
                String.Format ("Cannot configure this account without accepting the certificate."),
                new NcAlertAction ("OK", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: ShowCertRejected");

                }));
            return;
        }

        public void Start ()
        {
            ShowWaitingScreen ("Verifying Your Server...");
            BackEnd.Instance.Start (account.Id);
        }

        public void UpdateUI ()
        {
            if (null == waitingScreen) {
                ShowWaitingScreen ("Syncing Your Inbox...");
            } else {
                waitingScreen.ShowView ("Syncing Your Inbox...");
            }
        }

        public void ShowWaitingScreen (string waitingMessage)
        {
            if (null == waitingScreen) {
                RemoveWindows ();
                waitingScreen = new WaitingScreen (new CGRect (0, 0, View.Frame.Width, View.Frame.Height), this);
                View.AddSubview (waitingScreen);
            }
            waitingScreen.ShowView (waitingMessage);
        }

        public void PromptForCredentials ()
        {
            RemoveWindows ();
            PerformSegue ("SegueToAccountCredentials", new SegueHolder (NachoCredentialsRequestEnum.InitialAsk));
        }

        public void ShowCredReq ()
        {
            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                ShowAdvancedConfiguration (LoginProtocolControl.Prompt.CredRequest);
                break;
            default:
                RemoveWindows ();
                PerformSegue ("SegueToAccountCredentials", new SegueHolder (NachoCredentialsRequestEnum.CredReqCallback));
                break;
            }
        }

        // The signout/signin trick causes the
        // Google UI to always prompt for user
        // instead of automatically taking the
        // currently signed-in user.
        public void StartGoogleLogin ()
        {
            Log.Info (Log.LOG_UI, "avl: StartGoogleLogin");
            // Uncomment to test with browser on simulator
            // Google.iOS.GIDSignIn.SharedInstance.AllowsSignInWithBrowser = true;
            // Google.iOS.GIDSignIn.SharedInstance.AllowsSignInWithWebView = false;
            Google.iOS.GIDSignIn.SharedInstance.Delegate = this;
            Google.iOS.GIDSignIn.SharedInstance.UIDelegate = this;
            Google.iOS.GIDSignIn.SharedInstance.SignOut ();
            Google.iOS.GIDSignIn.SharedInstance.SignIn ();
        }

        public void StartGoogleSilentLogin ()
        {
            Log.Info (Log.LOG_UI, "avl: StartGoogleSilentLogin");
            Google.iOS.GIDSignIn.SharedInstance.Delegate = this;
            Google.iOS.GIDSignIn.SharedInstance.UIDelegate = this;
            Google.iOS.GIDSignIn.SharedInstance.SignInSilently ();
        }

        // GIDSignInDelegate
        public void DidSignInForUser (GIDSignIn signIn, GIDGoogleUser user, NSError error)
        {
            Log.Info (Log.LOG_UI, "avl: DidSignInForUser {0}", error);

            var accountBeingConfigured = McAccount.GetAccountBeingConfigured ();
            if (null != accountBeingConfigured) {
                Log.Error (Log.LOG_UI, "avl: DidSignInForUser did not expect to find an account being configured");
                return;
            }

            if (null != error) {
                if (((int)Google.iOS.GIDSignInErrorCode.HasNoAuthInKeychain) != error.Code) {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: DidSignInForUser");
                }
                return;
            }
                
            service = McAccount.AccountServiceEnum.GoogleDefault;

            if (LoginHelpers.ConfiguredAccountExists (user.Profile.Email)) {
                // Already have this one.
                Log.Info (Log.LOG_UI, "avl: AppDelegate DidSignInForUser existing account: {0}", user.Profile.Email);
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.DuplicateAccount, "avl: DidSignInForUser");
                return;
            }

            account = NcAccountHandler.Instance.CreateAccount (service,
                user.Profile.Email,
                user.Authentication.AccessToken, 
                user.Authentication.RefreshToken,
                user.Authentication.AccessTokenExpirationDate.ToDateTime ());
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);

            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: DidSignInForUser");

            if (user.Profile.HasImage) {
                FetchGooglePortrait (account, user.Profile.ImageURL (40));
            }
        }

        async void FetchGooglePortrait (McAccount account, NSUrl imageUrl)
        {
            try {
                var httpClient = new HttpClient ();
                byte[] contents = await httpClient.GetByteArrayAsync (imageUrl);
                var portrait = McPortrait.InsertFile (account.Id, contents);
                account.DisplayPortraitId = portrait.Id;
                account.Update ();
            } catch (Exception e) {
                Log.Info (Log.LOG_UI, "avl: FetchGooglePortrait {0}", e);
            }
        }

        public void StartSync ()
        {
            ShowWaitingScreen ("Verifying Your Server...");
            BackEnd.Instance.Start (account.Id);
        }

        public void ShowSupport ()
        {
            RemoveWindows ();
            PerformSegue ("SegueToSupport", this);
        }

        public void ShowTutorial ()
        {
            RemoveWindows ();
            PerformSegue ("SegueToHome", this);
        }

        public void Done ()
        {
            account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
            account.Update ();

            // FIXME: Only set if null or device
            NcApplication.Instance.Account = account;
            LoginHelpers.SetSwitchToTime (account);
            BackEnd.Instance.Start (); // earlier we stopped all others. Restart them now.

            RemoveWindows ();
            NavigationController.PopToRootViewController (true);
        }

        public void Quit ()
        {
            RemoveWorkInProgress (() => {
                RemoveWindows ();
                NavigationController.PopToRootViewController (false);
            });
        }

        public void StartOver ()
        {
            RemoveWorkInProgress (() => {
                RemoveWindows ();
                NavigationController.PopToRootViewController (false);
            });
        }

        protected void ServiceSelected (McAccount.AccountServiceEnum service)
        {
            this.service = service;

            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ExchangePicked, "avl: ServiceSelected");
                break;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ImapPicked, "avl: ServiceSelected");
                break;
            case McAccount.AccountServiceEnum.GoogleDefault:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.GmailPicked, "avl: ServiceSelected");
                break;
            default:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.KnownServicePicked, "avl: ServiceSelected");
                break;
            }
        }

        public void CredentialsDismissed (UIViewController vc, bool startInAdvanced, string email, string password, NachoCredentialsRequestEnum why, bool startOver)
        {
            this.email = email;
            this.password = password;

            if (startInAdvanced) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowAdvanced, "avl: CredentialsDismissed");
                return;
            }
            if (startOver) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.StartOver, "avl: CredentialsDismissed");
                return;
            }

            // If the email address has changed,
            // remove the account being configured
            if (null != account) {
                if (!String.Equals (account.EmailAddr, email)) {
                    NcAccountHandler.Instance.RemoveAccount (account.Id);
                    account = null;
                }
            }

            // Does this email address exist in the db?  Complain if not the 'in progress' account
            if (LoginHelpers.ConfiguredAccountExists (email)) {
                Log.Info (Log.LOG_UI, "avl: CredentialsDismissed existing account: {0}", email);
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.DuplicateAccount, "avl: CredentialsDismissed");
                return;
            }

            // Is the email address unchanged & this a cred req?  Update creds & go.
            if (null != account) {
                if (NachoCredentialsRequestEnum.CredReqCallback == why) {
                    var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();
                    cred.UpdatePassword (password);
                    cred.Username = email;
                    cred.Update ();
                    BackEnd.Instance.CredResp (account.Id);
                    Log.Info (Log.LOG_UI, "avl: UpdateCredentialsAndGo a/c updated {0}/{1}", account.Id, cred.Id);
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CredUpdate, "avl: CredentialsDismissed");
                    ShowWaitingScreen ("Verifying Your Server...");
                    return;
                }
            }

            // Create or re-create the account if it's null.
            // If the user didn't change the email address, then the old account is still around.
            if (null == account) {
                account = NcAccountHandler.Instance.CreateAccount (service, email, password);
                NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: CredentialsDismissed");
            } else {
                var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();
                cred.UpdatePassword (password);
                BackEnd.Instance.Stop (account.Id);
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ServerUpdate, "avl: CredentialsDismissed");
            }
              
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (null == account) {
                return;
            }

            // Won't do anything if this isn't our account
            if ((null != s.Account) && (s.Account.Id != account.Id)) {
                return;
            }

            if (NcResult.SubKindEnum.Info_EmailMessageSetChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_EmailMessageSetChanged Status Ind (AdvancedView)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Info_InboxPingStarted == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_InboxPingStarted Status Ind (AdvancedView)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Info_BackEndStateChanged == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Info_BackEndStateChanged Status Ind (Advanced View)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Error_NetworkUnavailable");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Info_NetworkStatus == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Info_NetworkStatus");
                if (NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                    // FIXME: Kickstart if network status is restored
                }
                return;
            }
        }

        private void EventFromEnum ()
        {
            NcAssert.NotNull (account);

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NoNetwork, "avl: EventFromEnum no network");
                return;
            }

            var accountId = account.Id;

            var senderState = BackEnd.Instance.BackEndState (accountId, McAccount.AccountCapabilityEnum.EmailSender);
            var readerState = BackEnd.Instance.BackEndState (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);

            Log.Info (Log.LOG_UI, "avl: handleStatusEnums {0} sender={1} reader={2}", accountId, senderState, readerState);

            if ((BackEndStateEnum.ServerConfWait == senderState) || (BackEndStateEnum.ServerConfWait == readerState)) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ServerConfCallback, "avl: EventFromEnum server conf wait");
                return;
            }

            if ((BackEndStateEnum.CredWait == senderState) || (BackEndStateEnum.CredWait == readerState)) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CredReqCallback, "avl: EventFromEnum cred req");
                return;
            }

            if ((BackEndStateEnum.CertAskWait == senderState) || (BackEndStateEnum.CertAskWait == readerState)) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CertAskCallback, "avl: EventFromEnum cert ask");
                return;
            }

            if ((BackEndStateEnum.PostAutoDPreInboxSync == senderState) || (BackEndStateEnum.PostAutoDPreInboxSync == readerState)) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.PostAutoDPreInboxSync, "avl: EventFromEnum pre inbox sync");
                return;
            }

            if ((BackEndStateEnum.PostAutoDPostInboxSync == senderState) || (BackEndStateEnum.PostAutoDPostInboxSync == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums PostAutoDPostInboxSync");
                if ((BackEndStateEnum.PostAutoDPostInboxSync == senderState) && (BackEndStateEnum.PostAutoDPostInboxSync == readerState)) {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.PostAutoDPostInboxSync, "avl: EventFromEnum post inbox sync");
                }
                return;
            }

            if ((BackEndStateEnum.Running == senderState) || (BackEndStateEnum.Running == readerState)) {
                Log.Info (Log.LOG_UI, "avl: status enums running");
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Running, "avl: EventFromEnum running");
                return;
            }

            if ((BackEndStateEnum.NotYetStarted == senderState) || (BackEndStateEnum.NotYetStarted == readerState)) {
                // Trust that things will start soon.
                Log.Info (Log.LOG_UI, "avl: status enums notyetstarted");
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NotYetStarted, "avl: EventFromEnum not started");
                return;
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
                var holder = (SegueHolder)sender;
                var reason = (NachoCredentialsRequestEnum)holder.value;
                vc.Setup (this, service, reason, email, password);
                return;
            }
            if (segue.Identifier.Equals ("SegueToSupport")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToHome")) {
                return;
            }
            if (segue.Identifier.Equals ("SegueToTabController")) {
                return;
            }
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

        // On quit or start over
        void RemoveWorkInProgress (Action postCleanup)
        {
            if (null == account) {
                postCleanup ();
                return;
            }

            Action action = () => {
                NcAccountHandler.Instance.RemoveAccount (account.Id);
                InvokeOnMainThread (() => {
                    postCleanup ();
                });
            };
            NcTask.Run (action, "RemoveAccount");
        }

        /// <summary>
        /// The user hits the Advanced Login button on the wait screen
        /// </summary>
        public void ReturnToAdvanceView ()
        {
            if (CanShowAdvanced ()) {
                waitingScreen.DismissView ();
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowAdvanced, "avl: ReturnToAdvanceView stopped");
            }
        }

        public bool CanShowAdvanced ()
        {
            return (McAccount.AccountServiceEnum.Exchange == service) || (McAccount.AccountServiceEnum.IMAP_SMTP == service);
        }

        public void SegueToSupport ()
        {
            waitingScreen.DismissView ();
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowSupport, "avl: onConnect");
        }

        protected override void OnKeyboardChanged ()
        {
            // Maybe called from keyboard handler because
            // the notification is still alive when account
            // information is being gathered.  Avoid crash!

            if (null != loginFields) {
                loginFields.Layout (View.Frame.Height - keyboardHeight);
            }
        }

        public override bool ShouldAutorotate ()
        {
            return false;
        }
    }
}

