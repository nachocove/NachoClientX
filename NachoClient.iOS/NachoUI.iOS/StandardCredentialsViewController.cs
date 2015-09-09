// This file has been autogenerated from a class added in the UI designer.

using System;
using CoreGraphics;

using Foundation;
using UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Linq;

namespace NachoClient.iOS
{

    public partial class StandardCredentialsViewController : AccountCredentialsViewController, INachoCertificateResponderParent, AccountAdvancedFieldsViewControllerDelegate, IUITextFieldDelegate
    {

        #region Properties

        private bool StatusIndCallbackIsSet;
        private bool IsShowingAdvanced;
        private bool IsSubmitting;
        private UIView advancedSubview;
        private NSLayoutConstraint[] advancedConstraints;
        AccountAdvancedFieldsViewController advancedFieldsViewController;
        private bool HideAdvancedButton = false;
        private bool LockEmailField = false;

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
                var creds = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                passwordField.Text = creds.GetPassword ();
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
            if (IsMovingFromParentViewController) {
                if (Account != null) {
                    NcAccountHandler.Instance.RemoveAccount (Account.Id);
                }
            }
        }


        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "cert-ask") {
                var vc = (CertAskViewController)segue.DestinationViewController;
                vc.Setup (Account, McAccount.AccountCapabilityEnum.EmailSender);
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
                UpdateForSubmitting ();
                StartListeningForApplicationStatus ();
                scrollView.SetContentOffset (new CGPoint (0, 0), true);
                if (Account == null) {
                    Account = NcAccountHandler.Instance.CreateAccount (Service, email, password);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController created account ID{0}", Account.Id);
                    if (IsShowingAdvanced) {
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, Service);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController Instace.Start for ID{0}", Account.Id);
                    BackEnd.Instance.Start (Account.Id);
                } else {
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController updating account ID{0}", Account.Id);
                    Account.EmailAddr = email;
                    Account.Update ();
                    var cred = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                    cred.Username = email;
                    cred.UpdatePassword (password);
                    if (IsShowingAdvanced) {
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController stop/start ID{0}", Account.Id);
                    BackEnd.Instance.Stop (Account.Id);
                    BackEnd.Instance.Start (Account.Id);
                }
            } else {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController issue found: {0}", issue);
                NcAlertView.ShowMessage (this, "Nacho Mail", issue);
            }
        }

        partial void Support (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController showing support");
            var storyboard = UIStoryboard.FromName ("MainStoryboard_iPhone", null);
            var vc = (SupportViewController)storyboard.InstantiateViewController ("SupportViewController");
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

        void StartListeningForApplicationStatus ()
        {
            if (!StatusIndCallbackIsSet) {
                StatusIndCallbackIsSet = true;
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
            }
        }

        void StopListeningForApplicationStatus ()
        {
            if (StatusIndCallbackIsSet) {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                StatusIndCallbackIsSet = false;
            }
        }

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (!StatusIndCallbackIsSet) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController ignoring status callback because listening has been disabled");
                return;
            }
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_BackEndStateChanged == s.Status.SubKind) {
                if (s.Account != null && s.Account.Id == Account.Id) {

                    var senderState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                    var readerState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);

                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController senderState {0}, readerState {1}", senderState, readerState);

                    if ((BackEndStateEnum.ServerConfWait == senderState) || (BackEndStateEnum.ServerConfWait == readerState)) {
                        StopListeningForApplicationStatus ();
                        HandleServerError ();
                    } else if ((BackEndStateEnum.CredWait == senderState) || (BackEndStateEnum.CredWait == readerState)) {
                        StopListeningForApplicationStatus ();
                        HandleCredentialError ();
                    } else if ((BackEndStateEnum.CertAskWait == senderState) || (BackEndStateEnum.CertAskWait == readerState)) {
                        HandleCertificateAsk ();
                    } else if ((senderState >= BackEndStateEnum.PostAutoDPreInboxSync) && (readerState >= BackEndStateEnum.PostAutoDPreInboxSync)) {
                        StopListeningForApplicationStatus ();
                        HandleAccountVerified ();
                    }
                }
            } else if (NcResult.SubKindEnum.Error_NetworkUnavailable == s.Status.SubKind) {
                StopListeningForApplicationStatus ();
                HandleNetworkUnavailableError ();
            }
        }

        private void HandleNetworkUnavailableError ()
        {
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got network unavailable while verifying");
            ShowCredentialsError ("We were unable to verify your information because your device is offline.  Please try again when your device is online");
        }

        private void HandleServerError ()
        {
            IsSubmitting = false;
            if (Service == McAccount.AccountServiceEnum.GoogleExchange || Service == McAccount.AccountServiceEnum.Office365Exchange) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for known exchange service {0}, not showing advanced", Service);
                ShowCredentialsError ("We were unable to verify your information.  Please confirm it is correct and try again.");
            } else {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for service {0}, showing advanced", Service);
                UpdateForSubmitting ();
                if (!IsShowingAdvanced) {
                    statusLabel.Text = "We were unable to verify your information.  Please confirm or enter advanced configuration information.";
                    ToggleAdvancedFields ();
                } else {
                    statusLabel.Text = "We were unable to verify your information.  Please confirm or enter advanced configuration information.";
                }
            }
        }

        private void HandleCredentialError ()
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CredWait for service {0}", Service);
            IsSubmitting = false;
            ShowCredentialsError ("Invalid username or password.  Please adjust and try again.");
        }

        private void HandleCertificateAsk ()
        {
            if (NcApplication.Instance.CertAskReqPreApproved (Account.Id, McAccount.AccountCapabilityEnum.EmailSender)) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, but cert is pre approved, so continuting on", Service);
                NcApplication.Instance.CertAskResp (Account.Id, McAccount.AccountCapabilityEnum.EmailSender, true);
            } else {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, user must approve", Service);
                StopListeningForApplicationStatus ();
                PerformSegue ("cert-ask", null);
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
            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController no network after certficate accepted by user");
                HandleNetworkUnavailableError ();
            } else {
                StartListeningForApplicationStatus ();
                NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            }
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
