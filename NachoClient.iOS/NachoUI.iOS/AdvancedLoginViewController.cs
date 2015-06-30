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
    public partial class AdvancedLoginViewController : NcUIViewController, ILoginProtocol, INachoCredentialsDelegate, INachoCertificateResponderParent, IGIDSignInDelegate, IGIDSignInUIDelegate
    {
        ILoginFields loginFields;
        WaitingScreen waitScreen;
        CertificateView certificateView;
        LoginProtocolControl loginProtocolControl;

        string email;
        string password;
        McAccount account;
        McAccount.AccountServiceEnum service;

        public enum ConnectStatusEnum
        {
            Connect,
            Support,
            StartOver
        }

        public delegate void onConnectCallback (ConnectStatusEnum status, McAccount account);

        public override void ViewDidLoad ()
        {
            Log.Info (Log.LOG_UI, "avl: ViewDidLoad");

            base.ViewDidLoad ();

            loginProtocolControl = new LoginProtocolControl (this);

            waitScreen = new WaitingScreen (View.Frame, this);
            waitScreen.Hidden = true;
            View.Add (waitScreen);

            certificateView = new CertificateView (View.Frame, this);
            View.Add (certificateView);

            View.BackgroundColor = A.Color_NachoGreen;
        }


        public override void ViewWillAppear (bool animated)
        {
            Log.Info (Log.LOG_UI, "avl: ViewWillAppear");

            base.ViewWillAppear (animated);

            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
                if (this.NavigationController.NavigationBarHidden == true) {
                    this.NavigationController.NavigationBarHidden = false; 
                }
                NavigationItem.SetHidesBackButton (true, false);
            }
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = false;
                if (this.NavigationController.NavigationBarHidden == true) {
                    this.NavigationController.NavigationBarHidden = false; 
                }
            }
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        public override void ViewDidAppear (bool animated)
        {
            Log.Info (Log.LOG_UI, "avl: ViewDidAppear");

            base.ViewDidAppear (animated);

            if (null == account) {
                // Configus interruptus?
                account = McAccount.GetAccountBeingConfigured ();
                if (null != account) {
                    service = account.AccountService;
                    BackEnd.Instance.Start (account.Id);
                }
            }

            if (McAccount.AccountServiceEnum.None == service) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NoService, "avl: ViewDidAppear");
            }

            if ((uint)LoginProtocolControl.States.FinishWait == loginProtocolControl.sm.State) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.TryAgain, "avl: ViewDidAppear");
            }
        }

        public void FinishUp ()
        {
            waitScreen.Layer.RemoveAllAnimations ();

            if (!LoginHelpers.HasViewedTutorial ()) {
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.ShowTutorial, "avl: FinishUp");
            } else {
                waitScreen.StartSyncedEmailAnimation (account.Id);
            }
        }

        public void FinishedSyncedEmailAnimation (int accountId)
        {
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AllDone, "avl: FinishedSyncedEmailAnimation");
        }

        public void PromptForService ()
        {
            PerformSegue ("SegueToAccountType", this);
        }

        public void ShowAdvancedConfiguration ()
        {
            var rect = new CGRect (0, 0, View.Frame.Width, View.Frame.Height);
            switch (service) {
            case McAccount.AccountServiceEnum.Exchange:
                loginFields = new ExchangeFields (account, rect, onConnect);
                break;
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                loginFields = new IMapFields (account, rect, onConnect);
                break;
            default:
                NcAssert.CaseError ();
                break;
            }
            View.AddSubview (loginFields.View);
        }

        void onConnect (ConnectStatusEnum connect, McAccount account)
        {
            View.EndEditing (true);

            switch (connect) {
            case ConnectStatusEnum.Connect:
                if (null == this.account) {
                    this.account = account;
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: onConnect");
                } else {
                    this.account = account;
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NotYetStarted, "avl: onConnect");
                }
                break;
            case ConnectStatusEnum.StartOver:
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.StartOver, "avl: onConnect");
                break;
            case ConnectStatusEnum.Support:
                PerformSegue ("SegueToSupport", this);
                break;
            }
        }

        public void ShowAdvancedConfigurationWithError ()
        {
        }

        public void ShowNoNetwork ()
        {
            NcActionSheet.Show (View, this, null,
                String.Format ("No network connection. Please check that you have internet access."),
                new NcAlertAction ("Try again", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.TryAgain, "avl: ShowNoNetwork");

                }),
                new NcAlertAction ("Cancel", () => {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: ShowNoNetwork");

                }));
            return;
        }

        public void ShowCertAsk ()
        {
            certificateView.SetCertificateInformation (account.Id);
            View.BringSubviewToFront (certificateView);
            certificateView.ShowView ();
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CertRejected, "avl: DontAcceptCertificate");
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CertAccepted, "avl: DontAcceptCertificate");
        }

        public void Start ()
        {
            waitScreen.ShowView ("Verifying Your Server...");
            BackEnd.Instance.Start (account.Id);
        }

        public void UpdateUI ()
        {
            waitScreen.ShowView ("Syncing Your Inbox...");
        }

        public void PromptForCredentials ()
        {
            PerformSegue ("SegueToAccountCredentials", new SegueHolder (false));
        }

        public void ShowCredReq ()
        {
            PerformSegue ("SegueToAccountCredentials", new SegueHolder (true));
        }

        public void StartGoogleLogin ()
        {
            Google.iOS.GIDSignIn.SharedInstance.Delegate = this;
            Google.iOS.GIDSignIn.SharedInstance.UIDelegate = this;

            // Add scope to give full access to email
            var scopes = Google.iOS.GIDSignIn.SharedInstance.Scopes.ToList ();
            scopes.Add ("https://mail.google.com");
            scopes.Add ("https://www.googleapis.com/auth/calendar");
            scopes.Add ("https://www.google.com/m8/feeds/");
            Google.iOS.GIDSignIn.SharedInstance.Scopes = scopes.ToArray ();

            Google.iOS.GIDSignIn.SharedInstance.SignIn ();
        }

        // GIDSignInDelegate
        public void DidSignInForUser (GIDSignIn signIn, GIDGoogleUser user, NSError error)
        {
            Log.Info (Log.LOG_UI, "avl: DidSignInForUser {0}", error);

            if (null != error) {
                if (error.Code == (int)GIDSignInErrorCode.CodeCanceled) {
                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: DidSignInForUser");
                    return;
                }
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.Quit, "avl: DidSignInForUser (unknown error)");
                return;
            }
                
            service = McAccount.AccountServiceEnum.GoogleDefault;

            // TODO: Check for & reject duplicate account.

            account = NcAccountHandler.Instance.CreateAccount (service,
                user.Profile.Email,
                user.Authentication.AccessToken, 
                user.Authentication.RefreshToken,
                user.Authentication.AccessTokenExpirationDate.ToDateTime ());
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);

            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: DidSignInForUser");
        }

        public void StartGoogleLoginWithComplaint ()
        {
        }

        public void StartSync ()
        {
            waitScreen.ShowView ("Verifying Your Server...");
            BackEnd.Instance.Start (account.Id);
        }

        public void TryAgainOrQuit ()
        {
        }

        public void ShowTutorial ()
        {
            PerformSegue ("SegueToHome", this);
        }

        public void Done ()
        {
            account.ConfigurationInProgress = false;
            account.Update ();

            // FIXME: Only set if null or device
            NcApplication.Instance.Account = account;

            NavigationController.PopToRootViewController (true);
        }

        public void Quit ()
        {
            RemoveWorkInProgress (() => NavigationController.PopToRootViewController (false));
        }

        public void StartOver ()
        {
            RemoveWorkInProgress (() => NavigationController.PopToRootViewController (false));
        }



        public AdvancedLoginViewController (IntPtr handle) : base (handle)
        {
            service = McAccount.AccountServiceEnum.None;
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

        public void CredentialsDismissed (UIViewController vc, bool startInAdvanced, string email, string password, bool credReqCallback, bool startOver)
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
            if (credReqCallback) {
                // Save email & password
                var cred = McCred.QueryByAccountId<McCred> (account.Id).Single ();
                account.EmailAddr = email;
                cred.UpdatePassword (password);
                account.Update ();
                cred.Update ();
                Log.Info (Log.LOG_UI, "avl: a/c updated {0}/{1}", account.Id, cred.Id);
                BackEnd.Instance.CredResp (account.Id);
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CredUpdate, "avl: CredentialsDismissed");
            }

            account = NcAccountHandler.Instance.CreateAccount (service, email, password);
            NcAccountHandler.Instance.MaybeCreateServersForIMAP (account, service);
            loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.AccountCreated, "avl: CredentialsDismissed");
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
            if (NcResult.SubKindEnum.Info_AsAutoDComplete == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Auto-D-Completed Status Ind (Advanced View)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: Advanced Login status callback: Error_NetworkUnavailable");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Error_ServerConfReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: ServerConfReq Status Ind (Adv. View)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Info_CredReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CredReqCallback Status Ind (Adv. View)");
                EventFromEnum ();
                return;
            }
            if (NcResult.SubKindEnum.Error_CertAskReqCallback == s.Status.SubKind) {
                Log.Info (Log.LOG_UI, "avl: CertAskCallback Status Ind");
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
                loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.CredReqCallback, "avl: EventFromEnem cred req");
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
                var credReqCallback = (bool)holder.value;
                vc.Setup (this, service, credReqCallback, email, password);
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
            // FIXME
            waitScreen.DismissView ();
        }

        public void SegueToSupport ()
        {
            waitScreen.DismissView ();
            PerformSegue ("SegueToSupport", this);
        }

        protected override void OnKeyboardChanged ()
        {
            // Maybe called from keyboard handler because
            // the notification is still alive when account
            // information is being gathered.  Avoid crash!

            // FIXME
            // LayoutView ();
        }

        public override bool ShouldAutorotate ()
        {
            return false;
        }
    }
}

