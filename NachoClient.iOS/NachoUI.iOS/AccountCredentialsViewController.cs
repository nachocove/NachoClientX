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
    public interface AccountCredentialsViewControllerDelegate
    {
        void AccountCredentialsViewControllerDidValidateAccount (AccountCredentialsViewController vc, McAccount account);
    }

    public partial class AccountCredentialsViewController : NcUIViewControllerNoLeaks, INachoCertificateResponderParent, AccountAdvancedFieldsViewControllerDelegate, IUITextFieldDelegate
    {

        public AccountCredentialsViewControllerDelegate AccountDelegate;
        public McAccount.AccountServiceEnum Service;
        public McAccount Account;
        private bool StatusIndCallbackIsSet;
        private bool IsShowingAdvanced;
        private bool IsSubmitting;
        private UIView advancedSubview;
        private NSLayoutConstraint[] advancedConstraints;
        AccountAdvancedFieldsViewController advancedFieldsViewController;
       

        public AccountCredentialsViewController (IntPtr handle) : base (handle)
        {
            NavigationItem.BackBarButtonItem = new UIBarButtonItem ();
            NavigationItem.BackBarButtonItem.Title = "";
        }

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
            var imageName = Util.GetAccountServiceImageName (Service);
            using (var image = UIImage.FromBundle (imageName)) {
                accountIconView.Image = image;
            }
            statusLabel.Text = String.Format("Please provide your {0} information", NcServiceHelper.AccountServiceName (Service));
            submitButton.Layer.CornerRadius = 6.0f;
            UpdateSubmitEnabled ();
            advancedButton.Hidden = Service != McAccount.AccountServiceEnum.Exchange;
            using (var icon = UIImage.FromBundle("Loginscreen-2")){
                emailField.LeftViewMode = UITextFieldViewMode.Always;
                emailField.AdjustedEditingInsets = new UIEdgeInsets (0, 45, 0, 15);
                emailField.AdjustedLeftViewRect = new CGRect(15, 15, 16, 11);
                emailField.LeftView = new UIImageView(icon);
            }
            using (var icon = UIImage.FromBundle("Loginscreen-3")){
                passwordField.AdjustedEditingInsets = new UIEdgeInsets (0, 45, 0, 15);
                passwordField.LeftViewMode = UITextFieldViewMode.Always;
                passwordField.AdjustedLeftViewRect = new CGRect(15, 15, 14, 15);
                passwordField.LeftView = new UIImageView(icon);
            }
            if (Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                ToggleAdvancedFields ();
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

        partial void Submit (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController submitting");
            var email = emailField.Text.Trim ();
            var password = passwordField.Text;
            var issue = IssueWithCredentials(email, password);
            if (issue == null){
                View.EndEditing(false);
                statusLabel.Text = "Verifying your information...";
                IsSubmitting = true;
                UpdateForSubmitting();
                StartListeningForApplicationStatus();
                scrollView.SetContentOffset(new CGPoint(0, 0), true);
                if (Account != null && (!String.Equals (Account.EmailAddr, email) || IsShowingAdvanced)) {
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController removing account ID{0}", Account.Id);
                    NcAccountHandler.Instance.RemoveAccount (Account.Id);
                    Account = null;
                }
                if (Account == null){
                    Account = NcAccountHandler.Instance.CreateAccount (Service, email, password);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController created account ID{0}", Account.Id);
                    if (IsShowingAdvanced){
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, Service);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController Instace.Start for ID{0}", Account.Id);
                    BackEnd.Instance.Start (Account.Id);
                }else{
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController updating account ID{0}", Account.Id);
                    var cred = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                    cred.UpdatePassword (password);
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController stop/start ID{0}", Account.Id);
                    BackEnd.Instance.Stop (Account.Id);
                    BackEnd.Instance.Start (Account.Id);
                }
            }else{
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController issue found: {0}", issue);
                NcAlertView.ShowMessage (this, "Nacho Mail", issue);
            }
        }

        String IssueWithCredentials (String email, String password)
        {
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
                return advancedFieldsViewController.IssueWithFields (email);
            }
            return null;
        }

        partial void Support (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController showing support");
            var storyboard = UIStoryboard.FromName("MainStoryboard_iPhone", null);
            var vc = (SupportViewController)storyboard.InstantiateViewController("SupportViewController");
            vc.HideNavTitle = false;
            NavigationController.PushViewController(vc, true);
        }

        partial void Advanced (NSObject sender)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController advanced toggle from {0} requested", IsShowingAdvanced);
            ToggleAdvancedFields();
        }

        partial void TextFieldChanged (NSObject sender)
        {
            UpdateSubmitEnabled();
        }

        void ToggleAdvancedFields()
        {
            if (!IsShowingAdvanced) {
                if (Service == McAccount.AccountServiceEnum.Exchange || Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                    IsShowingAdvanced = true;
                    advancedButton.SetTitle ("Hide Advanced", UIControlState.Normal);
                    if (advancedFieldsViewController == null) {
                        if (Service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                            advancedFieldsViewController = (ImapAdvancedFieldsViewController)Storyboard.InstantiateViewController ("ImapAdvancedFields");
                        } else if (Service == McAccount.AccountServiceEnum.Exchange) {
                            advancedFieldsViewController = (ExchangeAdvancedFieldsViewController)Storyboard.InstantiateViewController ("ExchangeAdvancedFields");
                        }
                        if (advancedFieldsViewController != null) {
                            advancedSubview = advancedFieldsViewController.View.Subviews [0];
                            advancedFieldsViewController.AccountDelegate = this;
                            advancedFieldsViewController.PopulateFieldsWithAccount (Account);
                        }
                    }
                    if (advancedSubview != null) {
                        advancedSubview.Frame = new CGRect (0, 0, advancedView.Frame.Width, advancedSubview.Frame.Height);
                        advancedView.AddSubview (advancedSubview);
                        advancedConstraints = NSLayoutConstraint.FromVisualFormat ("|-0-[view]-0-|", 0, null, NSDictionary.FromObjectAndKey (advancedSubview, (NSString)"view"));
                        advancedConstraints = advancedConstraints.Concat(NSLayoutConstraint.FromVisualFormat ("V:|-0-[view]-0-|", 0, null, NSDictionary.FromObjectAndKey (advancedSubview, (NSString)"view"))).ToArray();
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
                    }
                }
            }
        }

        void UpdateForSubmitting ()
        {
            if (IsSubmitting) {
                activityIndicatorView.Hidden = false;
                accountIconView.Hidden = true;
                activityIndicatorView.StartAnimating ();
                emailField.Enabled = false;
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
                emailField.Enabled = true;
                passwordField.Enabled = true;
                supportButton.Hidden = false;
                advancedButton.Hidden = Service != McAccount.AccountServiceEnum.Exchange;
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
            }else{
                submitButton.Alpha = 0.5f;
                submitButton.Enabled = false;
            }
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

        private void StatusIndicatorCallback (object sender, EventArgs e)
        {
            if (!StatusIndCallbackIsSet) {
                Log.Info (Log.LOG_UI, "AccountCredentialsViewController ignoring status callback because listening has been disabled");
                return;
            }
            var s = (StatusIndEventArgs)e;
            if (s.Account != null && s.Account.Id == Account.Id) {

//                if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
//                    loginProtocolControl.sm.PostEvent ((uint)LoginProtocolControl.Events.E.NoNetwork, "avl: EventFromEnum no network");
//                }

                var senderState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                var readerState = BackEnd.Instance.BackEndState (Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter);

                Log.Info (Log.LOG_UI, "AccountCredentialsViewController senderState {0}, readerState {1}", senderState, readerState);

                if ((BackEndStateEnum.ServerConfWait == senderState) || (BackEndStateEnum.ServerConfWait == readerState)) {
                    StopListeningForApplicationStatus ();
                    IsSubmitting = false;
                    if (Service == McAccount.AccountServiceEnum.GoogleExchange || Service == McAccount.AccountServiceEnum.Office365Exchange) {
                        Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for known exchange service {0}, not showing advanced", Service);
                        ShowCredentialsError ("We were unable to verify your information.  Please confirm it is correct and try again");
                    } else {
                        Log.Info (Log.LOG_UI, "AccountCredentialsViewController got ServerConfWait for service {0}, showing advanced", Service);
                        UpdateForSubmitting ();
                        statusLabel.Text = "We were unable to verify your informiation.  Please enter advanced configuration information.";
                        if (!IsShowingAdvanced) {
                            ToggleAdvancedFields ();
                        }
                    }
                } else if ((BackEndStateEnum.CredWait == senderState) || (BackEndStateEnum.CredWait == readerState)) {
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CredWait for service {0}", Service);
                    IsSubmitting = false;
                    StopListeningForApplicationStatus ();
                    ShowCredentialsError ("Invalid username or password.  Please adjust and try again.");
                } else if ((BackEndStateEnum.CertAskWait == senderState) || (BackEndStateEnum.CertAskWait == readerState)) {
                    if (NcApplication.Instance.CertAskReqPreApproved (Account.Id, McAccount.AccountCapabilityEnum.EmailSender)) {
                        Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, but cert is pre approved, so continuting on", Service);
                        NcApplication.Instance.CertAskResp (Account.Id, McAccount.AccountCapabilityEnum.EmailSender, true);
                    } else {
                        Log.Info (Log.LOG_UI, "AccountCredentialsViewController got CertAskWait for service {0}, user must approve", Service);
                        PerformSegue ("cert-ask", null);
                    }
                } else if ((senderState >= BackEndStateEnum.PostAutoDPreInboxSync) && (readerState >= BackEndStateEnum.PostAutoDPreInboxSync)) {
                    IsSubmitting = false;
                    Log.Info (Log.LOG_UI, "AccountCredentialsViewController PostAutoDPreInboxSync for reader or writer");
                    StopListeningForApplicationStatus ();
                    AccountDelegate.AccountCredentialsViewControllerDidValidateAccount (this, Account);
                }
            }
        }

        // INachoCertificateResponderParent
        public void DontAcceptCertificate (int accountId)
        {
            Log.Info (Log.LOG_UI, "AccountCredentialsViewController certificate rejected by user");
            IsSubmitting = false;
            StopListeningForApplicationStatus ();
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
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, true);
            LoginHelpers.UserInterventionStateChanged (accountId);
            DismissViewController (true, null);
        }


        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier == "cert-ask") {
                var vc = (CertAskViewController)segue.DestinationViewController;
                vc.Setup (Account, McAccount.AccountCapabilityEnum.EmailSender);
                vc.CertificateDelegate = this;
            }
        }

        public void AdvancedFieldsControllerDidChange (AccountAdvancedFieldsViewController vc)
        {
            UpdateSubmitEnabled ();
        }

        [Export("textFieldShouldReturn:")]
        public bool ShouldReturn (UITextField textField)
        {
            if (submitButton.Enabled) {
                textField.ResignFirstResponder ();
                Submit (textField);
                return true;
            }
            return false;
        }

        protected override void CreateViewHierarchy ()
        {
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }
           
    }
}
