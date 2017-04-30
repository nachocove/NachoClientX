
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public interface AccountAdvancedFieldsViewControllerDelegate
    {
        void AdvancedFieldsControllerDidChange (AccountAdvancedFieldsViewController vc);
    }

    public abstract class AccountAdvancedFieldsViewController
    {
        public AccountAdvancedFieldsViewControllerDelegate AccountDelegate;

        public AccountAdvancedFieldsViewController (View view)
        {
        }

        public abstract String IssueWithFields ();

        public abstract bool CanSubmitFields ();

        public abstract void PopulateFieldsWithAccount (McAccount account);

        public abstract void PopulateAccountWithFields (McAccount account);

        public abstract void UnpopulateAccount (McAccount account);

        public abstract void SetFieldsEnabled (bool enabled);

        public virtual void LockFieldsForMDMConfig (NcMdmConfig config)
        {
        }
    }

    public interface CredentialsFragmentDelegate
    {
        void CredentialsValidated (McAccount account);
        void CredentialsValidationFailed ();
    }

    public class CredentialsFragment : NcFragment, ILoginEvents, AccountAdvancedFieldsViewControllerDelegate, CertAskDelegate
    {
        private const string CERT_ASK_DIALOG_TAG = "CertAskDialogFragment";
        private const string ACCOUNT_ID_KEY = "accountId";
        private const string SERVICE_KEY = "service";
        private const string IS_SUBMITTING_KEY = "isSubmitting";
        private const string IS_SHOWING_ADVANCED_KEY = "isShowingAdvanced";
        private const string ACCEPT_CERT_KEY = "acceptCertOnNextReq";

        McAccount Account;
        McAccount.AccountServiceEnum service;

        private bool IsSubmitting;
        private bool IsShowingAdvanced = false;
        private bool HideAdvancedButton = false;
        private bool AcceptCertOnNextReq = false;
        private bool LockEmailField = false;

        private bool WasShowingAdvanced = false;

        AccountAdvancedFieldsViewController advancedFieldsViewController;

        EditText emailField;
        EditText passwordField;
        Button submitButton;
        Button supportButton;
        Button advancedButton;
        TextView statusLabel;
        ProgressBar activityIndicatorView;

        View advancedSubview;
        View advancedImapSubview;
        View advancedExchangeSubview;

        public static CredentialsFragment newInstance (McAccount.AccountServiceEnum service, McAccount account)
        {
            var fragment = new CredentialsFragment ();
            fragment.service = service;
            fragment.Account = account;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            if (null != savedInstanceState) {
                var certAskFragment = FragmentManager.FindFragmentByTag<CertAskFragment> (CERT_ASK_DIALOG_TAG);
                if (null != certAskFragment) {
                    certAskFragment.SetListener (this);
                }
                int accountId = savedInstanceState.GetInt (ACCOUNT_ID_KEY, 0);
                if (0 != accountId) {
                    Account = McAccount.QueryById<McAccount> (accountId);
                }
                service = (McAccount.AccountServiceEnum)savedInstanceState.GetInt (SERVICE_KEY, (int)McAccount.AccountServiceEnum.None);
                IsSubmitting = savedInstanceState.GetBoolean (IS_SUBMITTING_KEY, false);
                WasShowingAdvanced = savedInstanceState.GetBoolean (IS_SHOWING_ADVANCED_KEY, false);
                AcceptCertOnNextReq = savedInstanceState.GetBoolean (ACCEPT_CERT_KEY, false);
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.CredentialsFragment, container, false);

            var imageview = view.FindViewById<RoundedImageView> (Resource.Id.service_image);
            var labelview = view.FindViewById<TextView> (Resource.Id.service_prompt);

            imageview.SetImageResource (Util.GetAccountServiceImageId (service));

            var serviceFormat = GetString (Resource.String.get_credentials);
            labelview.Text = String.Format (serviceFormat, NcServiceHelper.AccountServiceName (service));

            emailField = view.FindViewById<EditText> (Resource.Id.email);
            passwordField = view.FindViewById<EditText> (Resource.Id.password);
            submitButton = view.FindViewById<Button> (Resource.Id.submit);
            supportButton = view.FindViewById<Button> (Resource.Id.support);
            advancedButton = view.FindViewById<Button> (Resource.Id.advanced);
            statusLabel = view.FindViewById<TextView> (Resource.Id.service_prompt);

            emailField.TextChanged += TextFieldChanged;
            passwordField.TextChanged += TextFieldChanged;

            submitButton.Click += SubmitButton_Click;
            supportButton.Click += SupportButton_Click;
            advancedButton.Click += AdvancedButton_Click;

            advancedImapSubview = view.FindViewById<View> (Resource.Id.advanced_imap_view);
            advancedImapSubview.Visibility = ViewStates.Gone;

            advancedExchangeSubview = view.FindViewById<View> (Resource.Id.advanced_exchange_view);
            advancedExchangeSubview.Visibility = ViewStates.Gone;

            activityIndicatorView = view.FindViewById<ProgressBar> (Resource.Id.spinner);
            activityIndicatorView.Visibility = ViewStates.Invisible;

            if (Account != null) {
                emailField.Text = Account.EmailAddr;
                var creds = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
                try {
                    passwordField.Text = creds.GetPassword ();
                } catch (KeychainItemNotFoundException ex) {
                    Log.Error (Log.LOG_UI, "KeychainItemNotFoundException {0}", ex.Message);
                }
            }

            UpdateSubmitEnabled ();
            HideAdvancedButton = service != McAccount.AccountServiceEnum.Exchange;
            if (HideAdvancedButton) {
                advancedButton.Visibility = ViewStates.Gone;
            }
            if (service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                ToggleAdvancedFields ();
            } else if (service == McAccount.AccountServiceEnum.Exchange && Account != null) {
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
            if (WasShowingAdvanced && !IsShowingAdvanced) {
                ToggleAdvancedFields ();
            }
            if (Account != null && Account.IsMdmBased) {
                LockFieldsForMDMConfig (NcMdmConfig.Instance);
            }

            if (IsSubmitting) {
                UpdateForSubmitting ();
                StartReceivingLoginEvents ();
                LoginEvents.CheckBackendState ();
            }
                
            return view;
        }

        void LockFieldsForMDMConfig (NcMdmConfig config)
        {
            if (!String.IsNullOrEmpty (config.EmailAddr)) {
                LockEmailField = true;
                emailField.Enabled = false;
                emailField.Alpha = 0.6f;
            }
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            if (null != Account) {
                outState.PutInt (ACCOUNT_ID_KEY, Account.Id);
            }
            outState.PutInt (SERVICE_KEY, (int)service);
            outState.PutBoolean (IS_SUBMITTING_KEY, IsSubmitting);
            outState.PutBoolean (IS_SHOWING_ADVANCED_KEY, IsShowingAdvanced);
            outState.PutBoolean (ACCEPT_CERT_KEY, AcceptCertOnNextReq);
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (!IsSubmitting && !LockEmailField) {
                emailField.RequestFocus ();
                var imm = (Android.Views.InputMethods.InputMethodManager)this.Activity.GetSystemService (Context.InputMethodService);
                imm.ShowSoftInput (emailField, Android.Views.InputMethods.ShowFlags.Implicit);
            }
        }

        public override void OnPause ()
        {
            base.OnPause ();
            activityIndicatorView.Visibility = ViewStates.Invisible;
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
            LoginEvents.Owner = null;
        }

        public override void OnBackPressed ()
        {
            base.OnBackPressed ();
            if (Account != null) {
                NcAccountHandler.Instance.RemoveAccount (Account.Id);
            }
        }

        void SubmitButton_Click (object sender, EventArgs e)
        {
            var email = emailField.Text.Trim ();
            var password = passwordField.Text;

            Log.Info (Log.LOG_UI, "CredentialsFragment submitting");

            var issue = IssueWithCredentials (email, password);
            if (issue == null) {
                statusLabel.Text = "Verifying your information...";
                IsSubmitting = true;
                AcceptCertOnNextReq = false;
                UpdateForSubmitting ();
                if (Account == null) {
                    Account = NcAccountHandler.Instance.CreateAccount (service, email, password);
                    Log.Info (Log.LOG_UI, "CredentialsFragment created account ID{0}", Account.Id);
                    if (IsShowingAdvanced) {
                        advancedFieldsViewController.PopulateAccountWithFields (Account);
                    }
                    NcAccountHandler.Instance.MaybeCreateServersForIMAP (Account, service);
                } else {
                    Log.Info (Log.LOG_UI, "CredentialsFragment updating account ID{0}", Account.Id);
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
                Log.Info (Log.LOG_UI, "CredentialsFragment start ID{0}", Account.Id);
                BackEnd.Instance.Start (Account.Id);
            } else {
                Log.Info (Log.LOG_UI, "CredentialsFragment issue found: {0}", issue);
                NcAlertView.ShowMessage (Activity, "Nacho Mail", issue);
            }
        }


        void SupportButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "SupportButton_Click");
            // FIXME: NEWUI
            //StartActivity (SupportActivity.IntentWithoutToolbar(this.Activity));
        }

        void AdvancedButton_Click (object sender, EventArgs e)
        {
            Log.Info (Log.LOG_UI, "CredentialsFragment advanced toggle from {0} requested", IsShowingAdvanced);
            ToggleAdvancedFields ();
        }

        void TextFieldChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            UpdateSubmitEnabled ();
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
                return String.Format ("Please use your {0} email address instead.", NcServiceHelper.AccountServiceName (service));
            }
            if (!NcServiceHelper.DoesAddressMatchService (email, service)) {
                return String.Format ("The email address does not match the service. Please use your {0} email address instead.", NcServiceHelper.AccountServiceName (service));
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

        void ToggleAdvancedFields ()
        {
            if (!IsShowingAdvanced) {
                if (service == McAccount.AccountServiceEnum.Exchange || service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                    if (advancedFieldsViewController == null) {
                        if (service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                            advancedSubview = advancedImapSubview;
                            advancedFieldsViewController = new AdvancedImapView (advancedSubview); 
                        } else if (service == McAccount.AccountServiceEnum.Exchange) {
                            advancedSubview = advancedExchangeSubview;
                            advancedFieldsViewController = new AdvancedExchangeView (advancedSubview);
                        }
                        if (advancedFieldsViewController != null) {
                            advancedFieldsViewController.AccountDelegate = this;
                            if (Account != null && Account.IsMdmBased) {
                                advancedFieldsViewController.LockFieldsForMDMConfig (NcMdmConfig.Instance);
                            }
                        }
                    }
                    if (advancedSubview != null) {
                        IsShowingAdvanced = true;
                        advancedSubview.Visibility = ViewStates.Visible;
                        advancedButton.SetText (Resource.String.hide_advanced_signin);
                        advancedFieldsViewController.PopulateFieldsWithAccount (Account);
                    }
                }
            } else {
                if (service == McAccount.AccountServiceEnum.Exchange) {
                    IsShowingAdvanced = false;
                    advancedSubview.Visibility = ViewStates.Gone;
                    advancedButton.SetText (Resource.String.advanced_signin);
                    advancedFieldsViewController.UnpopulateAccount (Account);
                }
            }
            UpdateSubmitEnabled ();
        }

        void UpdateForSubmitting ()
        {
            if (IsSubmitting) {
                activityIndicatorView.Visibility = ViewStates.Visible;
                if (!LockEmailField) {
                    emailField.Enabled = false;
                }
                passwordField.Enabled = false;
                submitButton.Enabled = false;
                submitButton.Alpha = 0.5f;
                supportButton.Visibility = ViewStates.Gone;
                advancedButton.Visibility = ViewStates.Gone;
                if (IsShowingAdvanced) {
                    advancedFieldsViewController.SetFieldsEnabled (false);
                }
            } else {
                activityIndicatorView.Visibility = ViewStates.Invisible;
                if (!LockEmailField) {
                    emailField.Enabled = true;
                }
                passwordField.Enabled = true;
                supportButton.Visibility = ViewStates.Visible;
                advancedButton.Visibility = (HideAdvancedButton ? ViewStates.Gone : ViewStates.Visible);
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
            // Correction:  On android, we got here without PostAutoDPreIboxSync!
            StopRecevingLoginEvents ();
            HandleAccountVerified ();
        }

        public void ServerIndTooManyDevices (int acccountId)
        {
            StopRecevingLoginEvents ();
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "CredentialsFragment got too many devices while verifying");
            ShowCredentialsError ("You are already using the maximum number of devices for this account.  Please contact your system administrator.");
        }

        public void ServerIndServerErrorRetryLater (int acccountId)
        {
            StopRecevingLoginEvents ();
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "CredentialsFragment got server error while verifying");
            ShowCredentialsError ("The server is currently unavailable. Please try again later.");
        }

        private void HandleNetworkUnavailableError ()
        {
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            Log.Info (Log.LOG_UI, "CredentialsFragment got network unavailable while verifying");
            ShowCredentialsError ("We were unable to verify your information because your device is offline.  Please try again when your device is online");
        }

        /// <summary>
        /// Get the server errors for this account (if any), and format them into a string for the user.
        /// TODO: Note that this is mostly an example function with minimal user-readability. We could add
        /// a switch to create a better string from serverErrors[server].SslPolicyError, for example,
        /// and could provide an 'advanced' or 'more info' section int he UI where we outline more
        /// stuff (for example from serverErrors[server].Chain or serverErrors[server].Cert) to help the
        /// user in some useful way.
        /// </summary>
        /// <returns>The server errors.</returns>
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
            var certErrors = ServerCertificatePeek.ServerErrors (Account.Id);
            if (certErrors.Count > 0) {
                var certErrorStrings = new List<string> ();
                foreach (var server in certErrors.Keys) {
                    certErrorStrings.Add (String.Format ("{0}: {1}", server, certErrors [server].SslPolicyError));
                }
                string errorText = String.Format ("We were unable to connect because of a server certificate issue, which can only be fixed by altering the server's configuration.  {0}", String.Join ("; ", certErrorStrings));
                Log.Info (Log.LOG_UI, "CredentialsFragment got ServerConfWait with cert errors for {0}", service);
                ShowCredentialsError (errorText);
            }else if (service == McAccount.AccountServiceEnum.GoogleExchange || service == McAccount.AccountServiceEnum.Office365Exchange) {
                string errorText = "We were unable to verify your information.  Please confirm it is correct and try again.";
                Log.Info (Log.LOG_UI, "CredentialsFragment got ServerConfWait for known exchange service {0}, not showing advanced", service);
                ShowCredentialsError (errorText);
            } else if (service == McAccount.AccountServiceEnum.Exchange || service == McAccount.AccountServiceEnum.IMAP_SMTP) {
                Log.Info (Log.LOG_UI, "CredentialsFragment got ServerConfWait for service {0}, showing advanced", service);
                string errorText = "We were unable to verify your information.  Please confirm or enter advanced configuration information.";
                UpdateForSubmitting ();
                if (!IsShowingAdvanced) {
                    statusLabel.Text = errorText;
                    ToggleAdvancedFields ();
                } else {
                    statusLabel.Text = errorText;
                }
            } else {
                string errorText = "We were unable to verify your information.  Please confirm it is correct and try again.";
                Log.Info (Log.LOG_UI, "CredentialsFragment got unexpected ServerConfWait for service {0}", service);
                ShowCredentialsError (errorText);
            }
        }

        private void HandleCredentialError ()
        {
            Log.Info (Log.LOG_UI, "CredentialsFragment got CredWait for service {0}", service);
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            ShowCredentialsError ("Invalid username or password.  Please adjust and try again.");
        }

        private void HandleCertificateAsk (McAccount.AccountCapabilityEnum capability)
        {
            if (NcApplication.Instance.CertAskReqPreApproved (Account.Id, capability)) {
                Log.Info (Log.LOG_UI, "CredentialsFragment got CertAskWait for service {0}, but cert is pre approved, so continuting on", service);
                NcApplication.Instance.CertAskResp (Account.Id, capability, true);
            } else {
                Log.Info (Log.LOG_UI, "CredentialsFragment got CertAskWait for service {0}, user must approve", service);
                StopRecevingLoginEvents ();
                var certAskDialog = CertAskFragment.newInstance (Account.Id, capability, this);
                certAskDialog.Show (FragmentManager, CERT_ASK_DIALOG_TAG);
            }
        }

        private void HandleAccountVerified ()
        {
            IsSubmitting = false;
            Log.Info (Log.LOG_UI, "CredentialsFragment PostAutoDPreInboxSync for reader or writer");
            var parent = (CredentialsFragmentDelegate)Activity;
            parent.CredentialsValidated (Account);
        }

        #endregion

        #region Cert Ask Interface
       
        public void AcceptCertificate (int accountId)
        {
            Log.Info (Log.LOG_UI, "CredentialsFragment certificate accepted by user");
            LoginHelpers.UserInterventionStateChanged (accountId);
            StartReceivingLoginEvents ();
            // Checking the backend state should either result in a network down callback, in which case
            // we stop, or a cert wait callback, in which case we'll accept the cert.
            AcceptCertOnNextReq = true;
            LoginEvents.CheckBackendState ();
        }

        public void DontAcceptCertificate (int accountId)
        {
            Log.Info (Log.LOG_UI, "CredentialsFragment certificate rejected by user");
            IsSubmitting = false;
            BackEnd.Instance.Stop (Account.Id);
            NcApplication.Instance.CertAskResp (accountId, McAccount.AccountCapabilityEnum.EmailSender, false);
            LoginHelpers.UserInterventionStateChanged (accountId);
            UpdateForSubmitting ();
            statusLabel.Text = "Account not created because the certificate was not accepted";
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

