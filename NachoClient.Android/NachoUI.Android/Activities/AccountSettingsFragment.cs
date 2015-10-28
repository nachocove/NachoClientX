
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;

namespace NachoClient.AndroidClient
{
    public class AccountSettingsFragment : Fragment
    {
        McAccount account;

        private const int SIGNATURE_REQUEST_CODE = 1;
        private const int DESCRIPTION_REQUEST_CODE = 2;
        private const int PASSWORD_REQUEST_CODE = 4;

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

        public static AccountSettingsFragment newInstance (McAccount account)
        {
            var fragment = new AccountSettingsFragment ();
            fragment.account = account;
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AccountSettingsFragment, container, false);

            // Buttonbar title
            var title = view.FindViewById<TextView> (Resource.Id.title);
            title.SetText (Resource.String.account_settings);
            title.Visibility = ViewStates.Visible;

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
            passwordRectificationView= view.FindViewById<View> (Resource.Id.account_password_rectification_view);
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
            if ((null != creds) && (McCred.CredTypeEnum.Password == creds.CredType)) {
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
                    accountIssue.SetText (Resource.String.update_password);
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
                } else {
                    passwordRectificationView.Visibility = ViewStates.Gone;
                }
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
            StartActivityForResult (
                ValidationActivity.ValidationIntent(this.Activity, account),
                PASSWORD_REQUEST_CODE);
        }

        void DeleteAccountView_Click (object sender, EventArgs e)
        {
            // Deletes the account & returns to the main screen
            var intent = RemoveAccountActivity.RemoveAccountIntent (this.Activity, account);
            StartActivity (intent);
        }

        void PasswordExpiresView_Click (object sender, EventArgs e)
        {
            
        }

        void PasswordRectificationView_Click (object sender, EventArgs e)
        {

        }

        void AccountIssueView_Click (object sender, EventArgs e)
        {
            
        }

        void FastNotifications_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            account.FastNotificationEnabled = e.IsChecked;
            account.Update ();
            BindAccount ();
            NcApplication.Instance.InvokeStatusIndEventInfo (account, NcResult.SubKindEnum.Info_FastNotificationChanged);
        }

        void NotificationsView_Click (object sender, EventArgs e)
        {
            var notificationsFragment = NotificationChooserFragment.newInstance (account.NotificationConfiguration);
            notificationsFragment.OnNotificationsChanged += NotificationsFragment_OnNotificationsChanged;
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            notificationsFragment.Show (ft, "dialog");
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
            var ft = FragmentManager.BeginTransaction ();
            ft.AddToBackStack (null);
            daysToSyncFragment.Show (ft, "dialog");
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
            
        }

        void AccountDescriptionView_Click (object sender, EventArgs e)
        {
            var title = GetString (Resource.String.description);
            var instructions = GetString (Resource.String.description_instructions);
            StartActivityForResult (
                NoteActivity.EditNoteIntent (this.Activity, title, instructions, account.DisplayName, insertDate: false),
                DESCRIPTION_REQUEST_CODE);
        }
      

    }
}

