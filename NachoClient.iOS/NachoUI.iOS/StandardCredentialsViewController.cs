// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;

using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Linq;
using NachoPlatform;

namespace NachoClient.iOS
{

    public partial class StandardCredentialsViewController : AccountCredentialsViewController, INachoCertificateResponderParent, AccountAdvancedFieldsViewControllerDelegate, IUITextFieldDelegate, ILoginEvents
    {

        #region Properties

        private bool IsShowingAdvanced;
        private bool IsSubmitting;
        private UIView advancedSubview;
        private NSLayoutConstraint[] advancedConstraints;
        AccountAdvancedFieldsViewController advancedFieldsViewController;
        private bool HideAdvancedButton = false;
        private bool LockEmailField = false;
        private bool AcceptCertOnNextReq = false;

        #endregion

        #region Constructors

        public StandardCredentialsViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

        #endregion

        #region iOS View Lifecycle

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            IsShowingAdvanced = false;
            if (Account != null) {
                Service = Account.AccountService;
                emailField.Text = Account.EmailAddr;
            }
            passwordField.WeakDelegate = this;
            accountIconView.Layer.CornerRadius = accountIconView.Frame.Size.Width / 2.0f;
            if (Account != null) {
                using (var image = Util.ImageForAccount (Account)) {
                    accountIconView.Image = image;
                }
            } else {
                var imageName = Util.GetAccountServiceImageName (Service);
                using (var image = UIImage.FromBundle (imageName)) {
                    accountIconView.Image = image;
                }
            }
            string accountName = NcServiceHelper.AccountServiceName (Service);
            if (Account != null) {
                accountName = Account.DisplayName;
            }
            statusLabel.Text = String.Format ("Please provide your {0} information", accountName);
            submitButton.Layer.CornerRadius = 6.0f;
            UpdateSubmitEnabled ();
            HideAdvancedButton = Service != McAccount.AccountServiceEnum.Exchange;
            if (HideAdvancedButton) {
                advancedButton.Hidden = true;
            }
            using (var icon = UIImage.FromBundle ("Loginscreen-2")) {
                emailField.LeftViewMode = UITextFieldViewMode.Always;
                emailField.AdjustedEditingInsets = new UIEdgeInsets (0, 45, 0, 15);
                emailField.AdjustedLeftViewRect = new CGRect (15, 15, 16, 11);
                emailField.LeftView = new UIImageView (icon);
            }
            using (var icon = UIImage.FromBundle ("Loginscreen-3")) {
                passwordField.AdjustedEditingInsets = new UIEdgeInsets (0, 45, 0, 15);
                passwordField.LeftViewMode = UITextFieldViewMode.Always;
                passwordField.AdjustedLeftViewRect = new CGRect (15, 15, 14, 15);
                passwordField.LeftView = new UIImageView (icon);
            }
            if (Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                ToggleAdvancedFields ();
            } else if (Service == McAccount.AccountServiceEnum.Exchange && Account != null) {
                var cred = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                var showAdvanced = false;
                if (cred != null && cred.UserSpecifiedUsername) {
                    showAdvanced = true;
                }
                var server = McServer.QueryByAccountId<McServer> (Account.Id).FirstOrDefault ();
                if (server != null && !String.IsNullOrEmpty (server.UserSpecifiedServerName)) {
                    showAdvanced = true;
                }
                if (showAdvanced) {
                    ToggleAdvancedFields ();
                }
            }
            if (Account != null && Account.IsMdmBased == true) {
                if (!String.IsNullOrEmpty (NcMdmConfig.Instance.EmailAddr)) {
                    LockEmailField = true;
                    emailField.Enabled = false;
                    emailField.BackgroundColor = emailField.BackgroundColor.ColorWithAlpha (0.6f);
                }
            }
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            if (activityIndicatorView.IsAnimating) {
                activityIndicatorView.StopAnimating ();
            }
            if (IsMovingFromParentViewController) {
                LoginEvents.Owner = null;
                if (Account != null) {
                    NcAccountHandler.Instance.RemoveAccount (Account.Id);
                }
            }
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            UpdateForSubmitting ();
        }


        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "cert-ask") {
                var holder = sender as SegueHolder;
                McAccount.AccountCapabilityEnum capability = (McAccount.AccountCapabilityEnum)holder.value;
                var vc = (CertAskViewController)segue.DestinationViewController;
                vc.Setup (Account, capability);
                vc.CertificateDelegate = this;
            }
        }

        #endregion

        #region User Actions

        partial void Submit (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController submitting");
            var email = emailField.Text.Trim ();
            var password = passwordField.Text;
            var issue = IssueWithCredentials (email, password);
            if (issue == null) {
                View.EndEditing (false);
                statusLabel.Text = "Verifying your information...";
                IsSubmitting = true;
                AcceptCertOnNextReq = false;
                UpdateForSubmitting ();
                scrollView.SetContentOffset (new CGPoint (0, 0), true);
                if (Account == null) {
                    Account = NcAccountHandler.Instance.CreateAccount (Service, email, password);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController created account ID{0}", Account.Id);
                    if (IsShowingAdvanced) {
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, Service);
                } else {
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController updating account ID{0}", Account.Id);
                    BackEnd.Instance.Stop (Account.Id);
                    Account.EmailAddr = email;
                    Account.Update ();
                    var cred = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                    cred.Username = email;
                    cred.UpdatePassword (password);
                    if (IsShowingAdvanced) {
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                }
                StartReceivingLoginEvents ();
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController start ID{0}", Account.Id);
                BackEnd.Instance.Start (Account.Id);
            } else {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController issue found: {0}", issue);
                NcAlertView.ShowMessage (this, "Nacho Mail", issue);
            }
        }

        partial void Support (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController showing support");
            var vc = new SupportViewController ();
            vc.HideNavTitle = false;
            NavigationController.PushViewController (vc, true);
        }

        partial void Advanced (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController advanced toggle from {0} requested", IsShowingAdvanced);
            ToggleAdvancedFields ();
        }

        partial void TextFieldChanged (NSObject sender)
        {
            UpdateSubmitEnabled ();
        }

        protected override void OnKeyboardChanged ()
        {
            scrollView.ContentInset = new UIEdgeInsets (0, 0, keyboardHeight, 0);
            if (keyboardHeight > 0) {
                if (scrollView.Frame.Height - keyboardHeight < submitButton.Frame.Bottom) {
                    scrollView.SetContentOffset (new CGPoint (0, statusLabel.Frame.Top - 18), false);
                }
            }
        }

        #endregion

        #region View Helpers

        void ToggleAdvancedFields ()
        {
            if (!IsShowingAdvanced) {
                if (Service == McAccount.AccountServiceEnum.Exchange || Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                    if (advancedFieldsViewController == null) {
                        if (Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                            advancedFieldsViewController = (ImapAdvancedFieldsViewController)Storyboard.InstantiateViewController ("ImapAdvancedFields");
                        } else if (Service == McAccount.AccountServiceEnum.Exchange) {
                            advancedFieldsViewController = (ExchangeAdvancedFieldsViewController)Storyboard.InstantiateViewController ("ExchangeAdvancedFields");
                        }
                        if (advancedFieldsViewController != null) {
                            advancedSubview = advancedFieldsViewController.View.Subviews [0];
                            advancedFieldsViewController.AccountDelegate = this;
                            if (Account != null && Account.IsMdmBased) {
                                advancedFieldsViewController.LockFieldsForMDMConfig (NcMdmConfig.Instance);
                            }
                        }
                    }
                    if (advancedSubview != null) {
                        IsShowingAdvanced = true;
                        advancedButton.SetTitle ("Hide Advanced", UIControlState.Normal);
                        advancedFieldsViewController.PopulateFieldsWithAccount (Account);
                        advancedSubview.Frame = new CGRect (0, 0, advancedView.Frame.Width, advancedSubview.Frame.Height);
                        advancedView.AddSubview (advancedSubview);
                        advancedConstraints = NSLayoutConstraint.FromVisualFormat ("|-0-[view]-0-|", 0, null, NSDictionary.FromObjectAndKey (advancedSubview, (NSString)"view"));
                        advancedConstraints = advancedConstraints.Concat (NSLayoutConstraint.FromVisualFormat ("V:|-0-[view]-0-|", 0, null, NSDictionary.FromObjectAndKey (advancedSubview, (NSString)"view"))).ToArray ();
                        advancedView.RemoveConstraint (advancedHeightConstraint);
                        advancedView.AddConstraints (advancedConstraints);
                    }
                    advancedView.LayoutIfNeeded ();
                }
            } else {
                if (Service == McAccount.AccountServiceEnum.Exchange) {
                    IsShowingAdvanced = false;
                    advancedButton.SetTitle ("Advanced Sign In", UIControlState.Normal);
                    advancedHeightConstraint.Constant = 0.0f;
                    if (advancedSubview != null && advancedSubview.Superview != null) {
                        advancedView.RemoveConstraints (advancedConstraints);
                        advancedConstraints = null;
                        advancedSubview.RemoveFromSuperview ();
                        advancedView.AddConstraint (advancedHeightConstraint);
                        advancedFieldsViewController.UnpopulateAccount (Account);
                    }
                }
            }
            UpdateSubmitEnabled ();
        }

        void UpdateForSubmitting ()
        {
            if (IsSubmitting) {
                activityIndicatorView.Hidden = false;
                accountIconView.Hidden = true;
                activityIndicatorView.StartAnimating ();
                if (!LockEmailField) {
                    emailField.Enabled = false;
                }
                passwordField.Enabled = false;
                submitButton.Enabled = false;
                submitButton.Alpha = 0.5f;
                supportButton.Hidden = true;
                advancedButton.Hidden = true;
                if (IsShowingAdvanced) {
                    advancedFieldsViewController.SetFieldsEnabled (false);
                }
            } else {
                activityIndicatorView.StopAnimating ();
                activityIndicatorView.Hidden = true;
                accountIconView.Hidden = false;
                if (!LockEmailField) {
                    emailField.Enabled = true;
                }
                passwordField.Enabled = true;
                supportButton.Hidden = false;
                advancedButton.Hidden = HideAdvancedButton;
                UpdateSubmitEnabled ();
                if (IsShowingAdvanced) {
                    advancedFieldsViewController.SetFieldsEnabled (true);
                }
            }
        }

        void ShowCredentialsError (String statusText)
        {
            UpdateForSubmitting ();
            statusLabel.Text = statusText;
        }

        void UpdateSubmitEnabled ()
        {
            bool isAdvancedComplete = true;
            if (IsShowingAdvanced) {
                isAdvancedComplete = advancedFieldsViewController.CanSubmitFields ();
            }
            if (isAdvancedComplete && emailField.Text.Length > 0 && passwordField.Text.Length > 0) {
                submitButton.Alpha = 1.0f;
                submitButton.Enabled = true;
            } else {
                submitButton.Alpha = 0.5f;
                submitButton.Enabled = false;
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
            if (IsShowingAdvanced) {
                return advancedFieldsViewController.IssueWithFields ();
            }
            return null;
        }

        [Export ("textFieldShouldReturn:")]
        public bool ShouldReturn (UITextField textField)
        {
            if (submitButton.Enabled) {
                textField.ResignFirstResponder ();
                Submit (textField);
                return true;
            }
            return false;
        }

        #endregion

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
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got too many devices while verifying");
            ShowCredentialsError ("You are already using the maximum number of devices for this account.  Please contact your system administrator.");
        }

        public void ServerIndServerErrorRetryLater (int acccountId)
        {
            StopRecevingLoginEvents ();
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got server error while verifying");
            ShowCredentialsError ("The server is currently unavailable. Please try again later.");
        }

        private void HandleNetworkUnavailableError ()
        {
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got network unavailable while verifying");
            ShowCredentialsError ("We were unable to verify your information because your device is offline.  Please try again when your device is online");
        }

        string getServerErrors ()
        {
            string serverErrorsTxt = "";
            var serverErrors = ServerCertificatePeek.ServerErrors (Account.Id);
            if (serverErrors.Count > 0) {
                foreach (var server in serverErrors.Keys) {
                    serverErrorsTxt += string.Format ("{0}: {1}", server, serverErrors[server].SslPolicyError);
                }
            }
            return serverErrorsTxt;
        }

        private void HandleServerError ()
        {
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            var serverErrors = getServerErrors ();
            if (Service == McAccount.AccountServiceEnum.GoogleExchange || Service == McAccount.AccountServiceEnum.Office365Exchange) {
                string errorText = "We were unable to verify your information.  Please confirm it is correct and try again.";
                if (!string.IsNullOrWhiteSpace (serverErrors)) {
                    errorText += string.Format ("  ({0})", serverErrors);
                }
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for known exchange service {0}, not showing advanced", Service);
                ShowCredentialsError (errorText);
            } else {
                string errorText = "We were unable to verify your information.  Please confirm or enter advanced configuration information.";
                if (!string.IsNullOrWhiteSpace (serverErrors)) {
                    errorText += string.Format ("  ({0})", serverErrors);
                }
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for service {0}, showing advanced", Service);
                UpdateForSubmitting ();
                if (!IsShowingAdvanced) {
                    statusLabel.Text = errorText;
                    ToggleAdvancedFields ();
                } else {
                    statusLabel.Text = errorText;
                }
            }
        }

        private void HandleCredentialError ()
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CredWait for service {0}", Service);
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            ShowCredentialsError ("Invalid username or password.  Please adjust and try again.");
        }

        private void HandleCertificateAsk (McAccount.AccountCapabilityEnum capability)
        {
            if (NcApplication.Instance.CertAskReqPreApproved (Account.Id, capability)) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, but cert is pre approved, so continuting on", Service);
                NcApplication.Instance.CertAskResp (Account.Id, capability, true);
            } else {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, user must approve", Service);
                StopRecevingLoginEvents ();
                var holder = new SegueHolder (capability);
                PerformSegue ("cert-ask", holder);
            }
        }

        private void HandleAccountVerified ()
        {
            IsSubmitting = false;
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController PostAutoDPreInboxSync for reader or writer");
            AccountDelegate.AccountCredentialsViewControllerDidValidateAccount (this, Account);
        }

        #endregion

        #region Cert Ask Interface

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController certificate rejected by user");
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            LoginHelpers.UserInterventionStateChanged (accountId);
            UpdateForSubmitting ();
            statusLabel.Text = "Account not created because the certificate was not accepted";
            DismissViewController (true, null);
        }

        // INachoCertificateResponderParent
        public void AcceptCertificate (int accountId)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController certificate accepted by user");
            LoginHelpers.UserInterventionStateChanged (accountId);
            DismissViewController (true, null);
            StartReceivingLoginEvents ();
            // Checking the backend state should either result in a newtork down callback, in which case
            // we stop, or a cert wait callback, in which case we'll accept the cert.
            AcceptCertOnNextReq = true;
            LoginEvents.CheckBackendState ();
        }

        #endregion

        #region Advanced Fields Delegate

        public void AdvancedFieldsControllerDidChange (AccountAdvancedFieldsViewController vc)
        {
            UpdateSubmitEnabled ();
        }

        #endregion
           
    }
}
