
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

    public class ValidationFragment : NcFragment, AccountAdvancedFieldsViewControllerDelegate
    {
        McAccount Account;
        McAccount.AccountServiceEnum service;

        private bool ShowAdvanced;
        private bool AcceptCertOnNextReq = false;
        private bool LockEmailField = false;

        AccountAdvancedFieldsViewController advancedFieldsViewController;

        EditText passwordField;
        Button submitButton;
        TextView statusLabel;
        ProgressBar activityIndicatorView;

        View advancedSubview;
        View advancedImapSubview;
        View advancedExchangeSubview;

        public static ValidationFragment newInstance (McAccount.AccountServiceEnum service, McAccount account, bool showAdvanced)
        {
            var fragment = new ValidationFragment ();
            fragment.service = service;
            fragment.Account = account;
            fragment.ShowAdvanced = showAdvanced;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.CredentialsFragment, container, false);

            var title = view.FindViewById<TextView> (Resource.Id.title);

            if (ShowAdvanced) {
                title.SetText (Resource.String.update_settings);
            } else {
                title.SetText (Resource.String.update_password);
            }

            var imageview = view.FindViewById<RoundedImageView> (Resource.Id.service_image);
            var labelview = view.FindViewById<TextView> (Resource.Id.service_prompt);

            imageview.SetImageResource (Util.GetAccountServiceImageId (service));

            var serviceFormat = GetString (Resource.String.get_credentials);
            labelview.Text = String.Format (serviceFormat, NcServiceHelper.AccountServiceName (service));

            var emailField = view.FindViewById<EditText> (Resource.Id.email);
            emailField.Visibility = ViewStates.Gone;

            var supportButton = view.FindViewById<Button> (Resource.Id.support);
            supportButton.Visibility = ViewStates.Gone;

            var advancedButton = view.FindViewById<Button> (Resource.Id.advanced);
            advancedButton.Visibility = ViewStates.Gone;

            passwordField = view.FindViewById<EditText> (Resource.Id.password);
            submitButton = view.FindViewById<Button> (Resource.Id.submit);
            statusLabel = view.FindViewById<TextView> (Resource.Id.service_prompt);

            passwordField.TextChanged += TextFieldChanged;

            submitButton.Click += SubmitButton_Click;

            advancedImapSubview = view.FindViewById<View> (Resource.Id.advanced_imap_view);
            advancedImapSubview.Visibility = ViewStates.Gone;

            advancedExchangeSubview = view.FindViewById<View> (Resource.Id.advanced_exchange_view);
            advancedExchangeSubview.Visibility = ViewStates.Gone;

            activityIndicatorView = view.FindViewById<ProgressBar> (Resource.Id.spinner);
            activityIndicatorView.Visibility = ViewStates.Invisible;

            var creds = McCred.QueryByAccountId<McCred> (Account.Id).Single ();
            try {
                passwordField.Text = creds.GetPassword ();
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_UI, "KeychainItemNotFoundException {0}", ex.Message);
            }
                
            if ((McAccount.AccountServiceEnum.IMAP_SMTP == service) || (McAccount.AccountServiceEnum.Exchange == service)) {
                if (ShowAdvanced) {
                    ShowAdvancedFields ();
                }
            }

            UpdateSubmitEnabled ();

            return view;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            activityIndicatorView.Visibility = ViewStates.Invisible;
        }

        public override void OnDestroy ()
        {
            base.OnDestroy ();
        }

        void SubmitButton_Click (object sender, EventArgs e)
        {
            var email = Account.EmailAddr;
            var password = passwordField.Text;

            Log.Info (Log.LOG_UI, "ValidationFragment submitting");

            var issue = IssueWithCredentials (email, password);
            if (null != issue) {
                Log.Info (Log.LOG_UI, "ValidationFragment issue found: {0}", issue);
                NcAlertView.ShowMessage (Activity, "Apollo Mail", issue);
                return;
            }

            ServerList = McServer.QueryByAccountId<McServer> (Account.Id).ToList ();

            if ((null == ServerList) || (0 == ServerList.Count ())) {
                Log.Error (Log.LOG_UI, "AccountValidationViewcontroller: no servers");
                return;
            }
                
            UpdateForSubmitting (true);
            StartNextValidation ();

        }

        List<McServer> ServerList;

        bool StartNextValidation ()
        {
            var server = ServerList.FirstOrDefault ();
            if (null == server) {
                return false;
            }
            ServerList.RemoveAt (0);

            var creds = McCred.QueryByAccountId<McCred> (Account.Id).SingleOrDefault ();
            if (null == creds) {
                Log.Error (Log.LOG_UI, "AccountValidationViewcontroller: no creds");
                return false;
            }
            var testCred = new McCred ();
            testCred.SetTestPassword (passwordField.Text);
            Account.LogHashedPassword (Log.LOG_HTTP, "AccountValidationViewcontroller - Testing new password", passwordField.Text);

            testCred.Username = creds.Username;
            testCred.UserSpecifiedUsername = creds.UserSpecifiedUsername;

            if (ShowAdvanced) {
                advancedFieldsViewController.PopulateAccountWithFields (Account);
            }

            if (!BackEnd.Instance.ValidateConfig (Account.Id, server, testCred).isOK ()) {
                HandleAccountIssue ("Network Error", "A network issue is preventing your changes from being validated. Would you like to save your changes anyway?");
                return false;
            }

            return true;
        }

        void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if ((null == s.Account) || (s.Account.Id != Account.Id)) {
                return;
            }

            if (NcResult.SubKindEnum.Info_ValidateConfigSucceeded == s.Status.SubKind) {
                if (!StartNextValidation ()) {
                    SavePasswordAndExit ();
                }
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedComm == s.Status.SubKind) {
                HandleAccountIssue ("Validation Failed", "This account may not be able to send or receive emails. Save anyway?");
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth == s.Status.SubKind) {
                HandleAccountIssue ("Invalid Credentials", "User name or password is incorrect. No emails can be sent or received. Save anyway?");
            }
            if (NcResult.SubKindEnum.Error_ValidateConfigFailedUser == s.Status.SubKind) {
                HandleAccountIssue ("Invalid Username", "User name is incorrect. No emails can be sent or received. Save anyway?");
            }
        }

        void HandleAccountIssue (string title, string message)
        {
            UpdateForSubmitting (false);
            NcAlertView.Show (this.Activity, title, message,
                () => {
                    SavePasswordAndExit ();
                },
                () => {
                });
        }

        void SavePasswordAndExit ()
        {
            UpdateForSubmitting (false);
            var creds = McCred.QueryByAccountId<McCred> (Account.Id).SingleOrDefault ();
            if ((null != creds) && (McCred.CredTypeEnum.Password == creds.CredType)) {
                Account.LogHashedPassword (Log.LOG_HTTP, "AccountValidationViewcontroller - Saving new password", passwordField.Text);
                creds.UpdatePassword (passwordField.Text);
                creds.Update ();
                BackEnd.Instance.CredResp (Account.Id);
            }
            var parent = (CredentialsFragmentDelegate)Activity;
            parent.CredentialsValidated (Account);
        }

        void TextFieldChanged (object sender, Android.Text.TextChangedEventArgs e)
        {
            UpdateSubmitEnabled ();
        }

        String IssueWithCredentials (String email, String password)
        {
            if (ShowAdvanced) {
                return advancedFieldsViewController.IssueWithFields ();
            }
            return null;
        }

        void ShowAdvancedFields ()
        {
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
                    advancedSubview.Visibility = ViewStates.Visible;
                    advancedFieldsViewController.PopulateFieldsWithAccount (Account);
                }
            }
            UpdateSubmitEnabled ();
        }

        void UpdateForSubmitting (bool IsSubmitting)
        {
            if (IsSubmitting) {
                NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
                activityIndicatorView.Visibility = ViewStates.Visible;
                passwordField.Enabled = false;
                submitButton.Enabled = false;
                submitButton.Alpha = 0.5f;
                if (ShowAdvanced) {
                    advancedFieldsViewController.SetFieldsEnabled (false);
                }
            } else {
                NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
                activityIndicatorView.Visibility = ViewStates.Invisible;
                passwordField.Enabled = true;
                UpdateSubmitEnabled ();
                if (ShowAdvanced) {
                    advancedFieldsViewController.SetFieldsEnabled (true);
                }
            }
        }

        void ShowCredentialsError (String statusText)
        {
            UpdateForSubmitting (false);
            statusLabel.Text = statusText;
        }

        void UpdateSubmitEnabled ()
        {
            bool isAdvancedComplete = true;
            if (ShowAdvanced && (null != advancedFieldsViewController)) {
                isAdvancedComplete = advancedFieldsViewController.CanSubmitFields ();
            }
            isAdvancedComplete = true; // FIXME
            if (isAdvancedComplete && passwordField.Text.Length > 0) {
                submitButton.Alpha = 1.0f;
                submitButton.Enabled = true;
            } else {
                submitButton.Alpha = 0.5f;
                submitButton.Enabled = false;
            }
        }

        #region Advanced Fields Delegate

        public void AdvancedFieldsControllerDidChange (AccountAdvancedFieldsViewController vc)
        {
            UpdateSubmitEnabled ();
        }

        #endregion
      


  
    }
}

