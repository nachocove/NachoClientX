
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
using Xamarin.Auth;
using Android.Support.CustomTabs;

namespace NachoClient.AndroidClient
{
    public interface IAccountSettingsFragmentOwner
    {
        McAccount AccountToView { get; }
    }

    public class AccountSettingsFragment : Fragment
    {
        McAccount account;

        private const int SIGNATURE_REQUEST_CODE = 1;
        private const int DESCRIPTION_REQUEST_CODE = 2;
        private const int PASSWORD_REQUEST_CODE = 4;

        private const string SAVED_ACCOUNT_ID_KEY = "AccountSettingsFragment.accountId";
        private const string DAYS_TO_SYNC_FRAGMENT_TAG = "DaysToSyncFragment";
        private const string NOTIFICATIONS_FRAGMENT_TAG = "NotificationChooserFragment";

        ButtonBar buttonBar;

        ImageView accountIcon;
        TextView accountName;

        View accountDescriptionView;
        TextView accountDescription;

        View updatePasswordView;

        View advancedSettingsView;

        TextView accountSignature;
        View accountSignatureView;

        TextView daysToSync;
        View daysToSyncView;

        TextView notifications;
        View notificationsView;

        Switch fastNotifications;

        Switch defaultEmailSwitch;
        Switch defaultCalendarSwitch;

        View accountIssuesView;
        View accountIssuesSeparator;
        View accountIssuesViewSeparator;

        TextView accountIssue;
        View accountIssueView;

        TextView passwordExpires;
        View passwordExpiresView;

        TextView passwordRectification;
        View passwordRectificationView;

        View deleteAccountView;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            account = ((IAccountSettingsFragmentOwner)this.Activity).AccountToView;
            if (null != savedInstanceState) {
                var daysToSyncFragment = FragmentManager.FindFragmentByTag<DaysToSyncChooserFragment> (DAYS_TO_SYNC_FRAGMENT_TAG);
                if (null != daysToSyncFragment) {
                    daysToSyncFragment.OnDaysToSyncChanged += DaysToSyncFragment_OnDaysToSyncChanged;
                }
                var notificationsFragment = FragmentManager.FindFragmentByTag<NotificationChooserFragment> (NOTIFICATIONS_FRAGMENT_TAG);
                if (null != notificationsFragment) {
                    notificationsFragment.OnNotificationsChanged += NotificationsFragment_OnNotificationsChanged;
                }
            }
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AccountSettingsFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.account_settings);

            accountIcon = view.FindViewById<ImageView> (Resource.Id.account_icon);
            accountName = view.FindViewById<TextView> (Resource.Id.account_name);

            accountDescription = view.FindViewById<TextView> (Resource.Id.account_description);
            accountDescriptionView = view.FindViewById<View> (Resource.Id.account_description_view);
            accountDescriptionView.Click += AccountDescriptionView_Click;

            updatePasswordView = view.FindViewById<View> (Resource.Id.account_update_password_view);
            updatePasswordView.Click += UpdatePasswordView_Click;

            advancedSettingsView = view.FindViewById<View> (Resource.Id.account_advanced_settings_view);
            advancedSettingsView.Click += AdvancedSettingsView_Click;

            accountSignature = view.FindViewById<TextView> (Resource.Id.account_signature);
            accountSignatureView = view.FindViewById<View> (Resource.Id.account_signature_view);
            accountSignatureView.Click += AccountSignatureView_Click;

            daysToSync = view.FindViewById<TextView> (Resource.Id.account_days_to_sync);
            daysToSyncView = view.FindViewById<View> (Resource.Id.account_days_to_sync_view);
            daysToSyncView.Click += DaysToSyncView_Click;

            notifications = view.FindViewById<TextView> (Resource.Id.account_notifications);
            notificationsView = view.FindViewById<View> (Resource.Id.account_notifications_view);
            notificationsView.Click += NotificationsView_Click;

            fastNotifications = view.FindViewById<Switch> (Resource.Id.account_fast_notification);
            fastNotifications.CheckedChange += FastNotifications_CheckedChange;

            if (account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender)) {
                defaultEmailSwitch = view.FindViewById<Switch> (Resource.Id.default_email_switch);
                defaultEmailSwitch.CheckedChange += DefaultEmailAccount_CheckedChange;
            } else {
                var defaultEmailView = view.FindViewById<View> (Resource.Id.default_email_view);
                defaultEmailView.Visibility = ViewStates.Gone;
            }

            if (account.HasCapability (McAccount.AccountCapabilityEnum.CalReader)) {
                defaultCalendarSwitch = view.FindViewById<Switch> (Resource.Id.default_calendar_switch);
                defaultCalendarSwitch.CheckedChange += DefaultCalendarAccount_CheckedChange;
            } else {
                var defaultCalendarView = view.FindViewById<View> (Resource.Id.default_calendar_view);
                defaultCalendarView.Visibility = ViewStates.Gone;
            }

            accountIssuesView = view.FindViewById<View> (Resource.Id.account_issues_view);
            accountIssuesSeparator = view.FindViewById<View> (Resource.Id.account_issues_separator);
            accountIssuesViewSeparator = view.FindViewById<View> (Resource.Id.account_issues_view_separator);

            accountIssue = view.FindViewById<TextView> (Resource.Id.account_issue);
            accountIssueView = view.FindViewById<View> (Resource.Id.account_issue_view);
            accountIssueView.Click += AccountIssueView_Click;

            passwordExpires = view.FindViewById<TextView> (Resource.Id.account_password_expires);
            passwordExpiresView = view.FindViewById<View> (Resource.Id.account_password_expires_view);
            passwordExpiresView.Click += PasswordExpiresView_Click;

            passwordRectification = view.FindViewById<TextView> (Resource.Id.account_password_rectification);
            passwordRectificationView = view.FindViewById<View> (Resource.Id.account_password_rectification_view);
            passwordRectificationView.Click += PasswordRectificationView_Click;

            deleteAccountView = view.FindViewById<View> (Resource.Id.delete_account_view);
            deleteAccountView.Click += DeleteAccountView_Click;

            BindAccount ();

            return view;
        }

        void BindAccount ()
        {
            accountIcon.SetImageResource (Util.GetAccountServiceImageId (account.AccountService));
            accountName.Text = account.EmailAddr;

            accountDescription.Text = account.DisplayName;

            var creds = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if ((null != creds) && ((McCred.CredTypeEnum.Password == creds.CredType) || (McCred.CredTypeEnum.OAuth2 == creds.CredType))) {
                updatePasswordView.Visibility = ViewStates.Visible;
            } else {
                updatePasswordView.Visibility = ViewStates.Gone;
            }

            if ((McAccount.AccountServiceEnum.Exchange == account.AccountService) || (McAccount.AccountServiceEnum.IMAP_SMTP == account.AccountService)) {
                advancedSettingsView.Visibility = ViewStates.Visible;
            } else {
                advancedSettingsView.Visibility = ViewStates.Gone;
            }

            accountSignature.Text = account.Signature;

            daysToSync.Text = Pretty.MaxAgeFilter (account.DaysToSyncEmail);

            notifications.Text = Pretty.NotificationConfiguration (account.NotificationConfiguration);

            fastNotifications.Checked = account.FastNotificationEnabled;

            if (null != defaultEmailSwitch) {
                var defaultEmailAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
                bool isDefaultEmail = defaultEmailAccount != null && account.Id == defaultEmailAccount.Id;
                defaultEmailSwitch.Checked = isDefaultEmail;
                defaultEmailSwitch.Enabled = !isDefaultEmail;
            }

            if (null != defaultCalendarSwitch) {
                var defaultCalendarAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.CalWriter);
                bool isDefaultCalendar = defaultCalendarAccount != null && account.Id == defaultCalendarAccount.Id;
                defaultCalendarSwitch.Checked = isDefaultCalendar;
                defaultCalendarSwitch.Enabled = !isDefaultCalendar;
            }

            int issues = 0;

            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
                issues += 1;
                switch (serverIssue) {
                case BackEndStateEnum.CredWait:
                    accountIssue.SetText (Resource.String.update_password);
                    break;
                case BackEndStateEnum.CertAskWait:
                    accountIssue.SetText (Resource.String.certificate_issue);
                    break;
                case BackEndStateEnum.ServerConfWait:
                    if (null == serverWithIssue || !serverWithIssue.IsHardWired) {
                        accountIssue.SetText (Resource.String.update_password);
                    } else {
                        accountIssue.SetText (Resource.String.server_error);
                    }
                    break;
                }
                accountIssueView.Visibility = ViewStates.Visible;
            } else {
                accountIssueView.Visibility = ViewStates.Gone;
            }

            DateTime expiry;
            string rectificationUrl;
            if (LoginHelpers.PasswordWillExpire (account.Id, out expiry, out rectificationUrl)) {
                issues += 1;
                var fmt = GetString (Resource.String.password_expires);
                passwordExpires.Text = String.Format (fmt, Pretty.ReminderDate (expiry));
                if (!String.IsNullOrEmpty (rectificationUrl)) {
                    passwordRectification.Text = rectificationUrl;
                    passwordRectificationView.Visibility = ViewStates.Visible;
                    passwordExpiresView.Visibility = ViewStates.Gone;
                } else {
                    passwordRectificationView.Visibility = ViewStates.Gone;
                    passwordExpiresView.Visibility = ViewStates.Visible;
                }
            } else {
                passwordRectificationView.Visibility = ViewStates.Gone;
                passwordExpiresView.Visibility = ViewStates.Gone;
            }

            if (0 < issues) {
                accountIssuesView.Visibility = ViewStates.Visible;
                accountIssuesViewSeparator.Visibility = ViewStates.Visible;
            } else {
                accountIssuesView.Visibility = ViewStates.Gone;
                accountIssuesViewSeparator.Visibility = ViewStates.Gone;
            }

            if (1 < issues) {
                accountIssuesSeparator.Visibility = ViewStates.Visible;
            } else {
                accountIssuesSeparator.Visibility = ViewStates.Gone;
            }
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
            outState.PutInt (SAVED_ACCOUNT_ID_KEY, account.Id);
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult (requestCode, resultCode, data);

            switch (requestCode) {

            case SIGNATURE_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    account.Signature = NoteActivity.ModifiedNoteText (data);
                    account.Update ();
                    BindAccount ();
                }
                break;

            case DESCRIPTION_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    account.DisplayName = NoteActivity.ModifiedNoteText (data);
                    account.Update ();
                    BindAccount ();
                }
                break;
            case PASSWORD_REQUEST_CODE:
                if (Result.Ok == resultCode) {
                    account = McAccount.QueryById<McAccount> (account.Id);
                    BindAccount ();
                }
                break;
            }
        }

        void UpdatePasswordView_Click (object sender, EventArgs e)
        {
            if (!MaybeStartGmailAuth (account)) {
                StartActivityForResult (
                    ValidationActivity.ValidationIntent (this.Activity, account, showAdvanced: false),
                    PASSWORD_REQUEST_CODE);
            }
        }

        void DeleteAccountView_Click (object sender, EventArgs e)
        {
            // Deletes the account & returns to the main screen
            var intent = RemoveAccountActivity.RemoveAccountIntent (this.Activity, account);
            StartActivity (intent);
        }

        void PasswordExpiresView_Click (object sender, EventArgs e)
        {
            Log.Error (Log.LOG_SYS, "PasswordExpiresView_Click: NOT IMPLEMENTED");
        }

        void PasswordRectificationView_Click (object sender, EventArgs e)
        {
            Log.Error (Log.LOG_SYS, "PasswordRectificationView_Click: NOT IMPLEMENTED");
        }

        void AccountIssueView_Click (object sender, EventArgs e)
        {
            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
                if (null == serverWithIssue || !serverWithIssue.IsHardWired) {
                    Log.Error (Log.LOG_SYS, "AccountIssueView_Click: needs to go to AdvancedSettings here");
                } else {
                    BackEnd.Instance.ServerConfResp (serverWithIssue.AccountId, serverWithIssue.Capabilities, false);
                    this.Activity.Finish ();
                }
            } else {
                Log.Error (Log.LOG_SYS, "AccountIssueView_Click: NOT IMPLEMENTED");
            }
        }

        void FastNotifications_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            // This is called when the UI object is programmatically set to its initial value.
            // So check that the value has actually changed before doing anything.
            if (account.FastNotificationEnabled != e.IsChecked) {
                account.FastNotificationEnabled = e.IsChecked;
                account.Update ();
                BindAccount ();
                NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_FastNotificationChanged);
            }
        }

        void DefaultCalendarAccount_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked) {
                var defaultCalendarAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.CalWriter);
                if (account.Id != defaultCalendarAccount.Id) {
                    var deviceAccount = McAccount.GetDeviceAccount ();
                    var mutablesModule = "DefaultAccounts";
                    var mutablesKey = String.Format ("Capability.{0}", (int)McAccount.AccountCapabilityEnum.CalWriter);
                    McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, account.Id);
                    defaultCalendarSwitch.Enabled = false;
                }
            }
        }

        void DefaultEmailAccount_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            if (e.IsChecked) {
                var defaultEmailAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
                if (account.Id != defaultEmailAccount.Id) {
                    var deviceAccount = McAccount.GetDeviceAccount ();
                    var mutablesModule = "DefaultAccounts";
                    var mutablesKey = String.Format ("Capability.{0}", (int)McAccount.AccountCapabilityEnum.EmailSender);
                    McMutables.SetInt (deviceAccount.Id, mutablesModule, mutablesKey, account.Id);
                    defaultEmailSwitch.Enabled = false;
                }
            }
        }

        void NotificationsView_Click (object sender, EventArgs e)
        {
            var notificationsFragment = NotificationChooserFragment.newInstance (account.NotificationConfiguration);
            notificationsFragment.OnNotificationsChanged += NotificationsFragment_OnNotificationsChanged;
            notificationsFragment.Show (FragmentManager, NOTIFICATIONS_FRAGMENT_TAG);
        }

        void NotificationsFragment_OnNotificationsChanged (object sender, McAccount.NotificationConfigurationEnum e)
        {
            account.NotificationConfiguration = e;
            account.Update ();
            BindAccount ();
        }

        void DaysToSyncView_Click (object sender, EventArgs e)
        {
            var daysToSyncFragment = DaysToSyncChooserFragment.newInstance (account.DaysToSyncEmail);
            daysToSyncFragment.OnDaysToSyncChanged += DaysToSyncFragment_OnDaysToSyncChanged;
            daysToSyncFragment.Show (FragmentManager, DAYS_TO_SYNC_FRAGMENT_TAG);
        }

        void DaysToSyncFragment_OnDaysToSyncChanged (object sender, NachoCore.ActiveSync.Xml.Provision.MaxAgeFilterCode e)
        {
            account.DaysToSyncEmail = e;
            account.Update ();
            BindAccount (); 
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_DaysToSyncChanged);
        }

        void AccountSignatureView_Click (object sender, EventArgs e)
        {
            var title = GetString (Resource.String.signature);
            var instructions = GetString (Resource.String.signature_instructions);
            StartActivityForResult (
                NoteActivity.EditNoteIntent (this.Activity, title, instructions, account.Signature, insertDate: false),
                SIGNATURE_REQUEST_CODE);
        }

        void AdvancedSettingsView_Click (object sender, EventArgs e)
        {
            StartActivityForResult (
                ValidationActivity.ValidationIntent (this.Activity, account, showAdvanced: true),
                PASSWORD_REQUEST_CODE);
        }

        void AccountDescriptionView_Click (object sender, EventArgs e)
        {
            var title = GetString (Resource.String.description);
            var instructions = GetString (Resource.String.description_instructions);
            StartActivityForResult (
                NoteActivity.EditNoteIntent (this.Activity, title, instructions, account.DisplayName, insertDate: false),
                DESCRIPTION_REQUEST_CODE);
        }

        bool MaybeStartGmailAuth (McAccount account)
        {
            if (McAccount.AccountServiceEnum.GoogleDefault != account.AccountService) {
                return false;
            }
            var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
            if (null == cred) {
                return false;
            }
            if (McCred.CredTypeEnum.OAuth2 != cred.CredType) {
                return false;
            }

            StartGoogleLogin ();

            return true;
        }


        public void StartGoogleLogin ()
        {
            GoogleOAuth2Authenticator.Create (account.EmailAddr, ContinueGoogleLogin);
        }

        public void ContinueGoogleLogin (GoogleOAuth2Authenticator auth)
        {
            auth.AllowCancel = true;

            // If authorization succeeds or is canceled, .Completed will be fired.
            auth.Completed += (s, e) => {
                if (e.IsAuthenticated) {


                    string access_token;
                    e.Account.Properties.TryGetValue ("access_token", out access_token);

                    string refresh_token;
                    e.Account.Properties.TryGetValue ("refresh_token", out refresh_token);

                    string expiresString = "0";
                    uint expirationSecs = 0;
                    if (e.Account.Properties.TryGetValue ("expires_in", out expiresString)) {
                        if (!uint.TryParse (expiresString, out expirationSecs)) {
                            Log.Info (Log.LOG_UI, "StartGoogleLogin: Could not convert expires value {0} to int", expiresString);
                        }
                    }

                    var url = String.Format ("https://www.googleapis.com/oauth2/v1/userinfo?access_token={0}", access_token);

                    Newtonsoft.Json.Linq.JObject userInfo;
                    try {
                        var userInfoString = new System.Net.WebClient ().DownloadString (url);
                        userInfo = Newtonsoft.Json.Linq.JObject.Parse (userInfoString);
                    } catch (Exception ex) {
                        Log.Info (Log.LOG_UI, "AuthCompleted: exception fetching user info {0}", ex);
                        NcAlertView.ShowMessage (Activity, "Nacho Mail", "We could not complete your account authentication.  Please try again.");
                        return;
                    }

                    if (!String.Equals (account.EmailAddr, (string)userInfo ["email"], StringComparison.OrdinalIgnoreCase)) {
                        // Can't change your email address
                        NcAlertView.ShowMessage (this.Activity, "Settings", "You may not change your email address.  Create a new account to use a new email address.");
                        return;
                    }

                    var cred = McCred.QueryByAccountId<McCred> (account.Id).SingleOrDefault ();
                    cred.UpdateOauth2 (access_token, refresh_token, expirationSecs);

                    BackEnd.Instance.CredResp (account.Id);
                }
            };

            auth.Error += (object sender, AuthenticatorErrorEventArgs e) => {

            };
            var intent = auth.GetUI (Activity) as CustomTabsIntent;
            // var intent = builder.Build ();
            var authUrl = auth.GetInitialUrlAsync ().Result;
            intent.LaunchUrl (Activity, Android.Net.Uri.Parse (authUrl.AbsoluteUri));
        }
    }
}

