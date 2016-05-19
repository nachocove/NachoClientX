//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;

using AppKit;
using Foundation;

using NachoCore.Utils;
using NachoCore.Model;
using NachoCore;

namespace NachoClient.Mac
{
    public partial class StandardCredentialsViewController : NSViewController, ILoginEvents
    {

        public McAccount Account;
        public McAccount.AccountServiceEnum Service;
        bool AcceptCertOnNextReq;

        public StandardCredentialsViewController (IntPtr handle) : base (handle)
        {
        }

        private const string IsConnectingKey = "isConnecting";
        private bool _IsConnecting;
        [Export (IsConnectingKey)]
        public bool IsConnecting {
            get{
                return _IsConnecting;
            }
            set {
                WillChangeValue (IsConnectingKey);
                _IsConnecting = value;
                DidChangeValue (IsConnectingKey);
            }
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            if (Account != null) {
                EmailField.StringValue = Account.EmailAddr;
            }
        }

        public override NSObject RepresentedObject {
            get {
                return base.RepresentedObject;
            }
            set {
                base.RepresentedObject = value;
                // Update the view, if already loaded.
            }
        }

        partial void Connect (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController submitting");
            var email = EmailField.StringValue.Trim ();
            var password = PasswordField.StringValue.Trim ();
            var issue = IssueWithCredentials (email, password);
            if (issue == null) {
//                statusLabel.Text = "Verifying your information...";
                IsConnecting = true;
                AcceptCertOnNextReq = false;
                if (Account == null) {
                    Account = NcAccountHandler.Instance.CreateAccount (Service, email, password);
                    Log.Info (Log.LOG_UI, "StandardCredentialsViewController created account ID{0}", Account.Id);
//                    if (IsShowingAdvanced) {
//                        advancedFieldsViewController.PopulateAccountWithFields (Account);
//                    }
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, Service);
                } else {
                    Log.Info (Log.LOG_UI, "StandardCredentialsViewController updating account ID{0}", Account.Id);
                    BackEnd.Instance.Stop (Account.Id);
                    Account.EmailAddr = email;
                    Account.Update ();
                    var cred = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                    cred.Username = email;
                    cred.UpdatePassword (password);
//                    if (IsShowingAdvanced) {
//                        advancedFieldsViewController.PopulateAccountWithFields (Account);
//                    }
                }
                StartReceivingLoginEvents ();
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController start ID{0}", Account.Id);
                BackEnd.Instance.Start (Account.Id);
            } else {
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController issue found: {0}", issue);
                var alert = NSAlert.WithMessage(issue, "OK", null, null, "");
                alert.RunModal ();
            }
        }

        String IssueWithCredentials (String email, String password)
        {
            if (!email.Contains ("@")) {
                return "Your email address must include an @.  For Example, username@company.com";
            }
            if (!EmailHelper.IsValidEmail (email)) {
                return "Your email address is not valid.\nFor example, username@company.com";
            }
            String serviceName = null;
            if (NcServiceHelper.IsServiceUnsupported (email, out serviceName)) {
                return String.Format ("Please use your {0} email address instead.", NcServiceHelper.AccountServiceName (Service));
            }
            if (!NcServiceHelper.DoesAddressMatchService (email, Service)) {
                return String.Format ("The email address does not match the service. Please use your {0} email address instead.", NcServiceHelper.AccountServiceName (Service));
            }
            if (LoginHelpers.ConfiguredAccountExists (email)) {
                return "An account with that email address already exists. Duplicate accounts are not supported.";
            }
            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                return "No network connection. Please check that you have internet access.";
            }
            // TODO: advanced fields
//            if (IsShowingAdvanced) {
//                return advancedFieldsViewController.IssueWithFields ();
//            }
            return null;
        }

        #region Backend Events

        void StartReceivingLoginEvents ()
        {
            LoginEvents.Owner = this;
            LoginEvents.AccountId = Account.Id;
        }

        void StopRecevingLoginEvents ()
        {
            LoginEvents.Owner = null;
        }

        public void CredReq (int accountId)
        {
            StopRecevingLoginEvents ();
            HandleCredentialError ();
        }

        public void ServConfReq (int accountId, McAccount.AccountCapabilityEnum capabilities, BackEnd.AutoDFailureReasonEnum arg)
        {
            StopRecevingLoginEvents ();
            HandleServerError ();
        }

        public void CertAskReq (int accountId, McAccount.AccountCapabilityEnum capabilities, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
        {
            if (AcceptCertOnNextReq) {
                AcceptCertOnNextReq = false;
                NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            } else {
                HandleCertificateAsk (capabilities);
            }
        }

        public void NetworkDown ()
        {
            StopRecevingLoginEvents ();
            HandleNetworkUnavailableError ();
        }

        public void PostAutoDPreInboxSync (int accountId)
        {
            StopRecevingLoginEvents ();
            HandleAccountVerified ();
        }

        public void PostAutoDPostInboxSync (int accountId)
        {
            // We never get here for this view because we stop once we see PostAutoDPreInboxSync
        }

        public void ServerIndTooManyDevices (int acccountId)
        {
            StopRecevingLoginEvents ();
            IsConnecting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController got too many devices while verifying");
            ShowCredentialsError ("You are already using the maximum number of devices for this account.  Please contact your system administrator.");
        }

        public void ServerIndServerErrorRetryLater (int acccountId)
        {
            StopRecevingLoginEvents ();
            IsConnecting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController got server error while verifying");
            ShowCredentialsError ("The server is currently unavailable. Please try again later.");
        }

        private void HandleNetworkUnavailableError ()
        {
            IsConnecting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController got network unavailable while verifying");
            ShowCredentialsError ("We were unable to verify your information because your device is offline.  Please try again when your device is online");
        }

        private void HandleServerError ()
        {
            IsConnecting = false;
            BackEnd.Instance.Stop (Account.Id);
            var certErrors = ServerCertificatePeek.ServerErrors (Account.Id);
            if (certErrors.Count > 0) {
                var certErrorStrings = new List<string> ();
                foreach (var server in certErrors.Keys) {
                    certErrorStrings.Add (String.Format ("{0}: {1}", server, certErrors [server].SslPolicyError));
                }
                string errorText = String.Format ("We were unable to connect because of a server certificate issue, which can only be fixed by altering the server's configuration.  {0}", String.Join("; ", certErrorStrings));
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController got ServerConfWait with cert errors for {0}", Service);
                ShowCredentialsError (errorText);
            } else if (Service == McAccount.AccountServiceEnum.GoogleExchange || Service == McAccount.AccountServiceEnum.Office365Exchange) {
                string errorText = "We were unable to verify your information.  Please confirm it is correct and try again.";
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController got ServerConfWait for known exchange service {0}, not showing advanced", Service);
                ShowCredentialsError (errorText);
            } else {
                string errorText = "We were unable to verify your information.  Please confirm or enter advanced configuration information.";
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController got ServerConfWait for service {0}, showing advanced", Service);
                ShowCredentialsError (errorText);
//                if (!IsShowingAdvanced) {
//                    statusLabel.Text = errorText;
//                    ToggleAdvancedFields ();
//                } else {
//                    statusLabel.Text = errorText;
//                }
            }
        }

        private void HandleCredentialError ()
        {
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController got CredWait for service {0}", Service);
            IsConnecting = false;
            BackEnd.Instance.Stop (Account.Id);
            ShowCredentialsError ("Invalid username or password.  Please adjust and try again.");
        }

        private void HandleCertificateAsk (McAccount.AccountCapabilityEnum capability)
        {
            if (NcApplication.Instance.CertAskReqPreApproved (Account.Id, capability)) {
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController got CertAskWait for service {0}, but cert is pre approved, so continuting on", Service);
                NcApplication.Instance.CertAskResp (Account.Id, capability, true);
            } else {
                Log.Info (Log.LOG_UI, "StandardCredentialsViewController got CertAskWait for service {0}, user must approve", Service);
//                StopRecevingLoginEvents ();
                // TODO: ask for credential ok
                NcApplication.Instance.CertAskResp (Account.Id, capability, true);
            }
        }

        private void HandleAccountVerified ()
        {
            Account.ConfigurationInProgress = McAccount.ConfigurationInProgressEnum.Done;
            Account.Update ();
            IsConnecting = false;
            Log.Info (Log.LOG_UI, "StandardCredentialsViewController PostAutoDPreInboxSync for reader or writer");
            var pageController = ParentViewController as WelcomePageController;
            if (pageController != null) {
                pageController.Complete (Account);
            }
        }

        void ShowCredentialsError (string message)
        {
            var alert = NSAlert.WithMessage (message, "OK", null, null, "");
            alert.RunModal ();
        }

        #endregion
    }
}
