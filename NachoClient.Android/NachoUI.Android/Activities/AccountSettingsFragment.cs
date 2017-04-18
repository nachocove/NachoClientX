
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
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoPlatform;
using Xamarin.Auth;
using Android.Support.CustomTabs;

namespace NachoClient.AndroidClient
{

    public class AccountSettingsFragment : Fragment, AccountSettingsAdapter.Listener
    {

        private const string FRAGMENT_NAME_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_NAME_DIALOG";
        private const string FRAGMENT_SIGNATURE_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_SIGNATURE_DIALOG";
        private const string FRAGMENT_SYNC_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_SYNC_DIALOG";
        private const string FRAGMENT_NOTIFICATIONS_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_NOTIFICATIONS_DIALOG";
        private const string FRAGMENT_PASSWORD_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_PASSWORD_DIALOG";
        private const string FRAGMENT_PASSWORD_NOTICE_DIALOG = "NachoClient.AndroidClient.AccountSettingsFragment.FRAGMENT_PASSWORD_NOTICE_DIALOG";

        public McAccount Account;

        #region Subviews

        RecyclerView RecyclerView;
        AccountSettingsAdapter ItemsAdapter;

        void FindSubviews (View view)
        {
            RecyclerView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
        }

        void ClearSubviews ()
        {
            RecyclerView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SettingsFragment, container, false);
            FindSubviews (view);

            var context = RecyclerView.Context;
            RecyclerView.SetLayoutManager (new LinearLayoutManager (Context));
            ItemsAdapter = new AccountSettingsAdapter (this, Account);
            RecyclerView.SetAdapter (ItemsAdapter);

            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Public API

        public void Refresh ()
        {
            ItemsAdapter.Account = Account;
            ItemsAdapter.Refresh ();
        }

        #endregion

        #region Listener

        public void OnNameSelected ()
        {
            ShowNameEditor ();
        }

        public void OnCredIssueSelected ()
        {
            ShowPasswordUpdate ();
        }

        public void OnCertIssueSelected ()
        {
            ShowCertAccept ();
        }

        public void OnServerIssueSelected ()
        {
            AttemptServerIssueFix ();
        }

        public void OnPasswordNoticeSelected ()
        {
            ShowPasswordExpiryNotice ();
        }

        public void OnPasswordUpdateSelected ()
        {
            ShowPasswordUpdate ();
        }

        public void OnAdvancedSelected ()
        {
            ShowAdvancedSettings ();
        }

        public void OnSignatureSelected ()
        {
            ShowSignatureEditor ();
        }

        public void OnSyncSelected ()
        {
            ShowSyncSelector ();
        }

        public void OnNotificationsSelected ()
        {
            ShowNotificationsSelector ();
        }

        #endregion

        #region Private Helpers

        private void ShowNameEditor ()
        {
            var dialog = new SimpleTextDialog (Resource.String.account_name, Resource.String.account_name_hint, Account.DisplayName, (text) => {
                Account.DisplayName = text;
                Account.Update ();
            });
            dialog.Show (FragmentManager, FRAGMENT_NAME_DIALOG, () => {
                ItemsAdapter.NotifyNameChanged ();
            });
        }

        private void ShowSignatureEditor ()
        {
            var dialog = new SimpleTextDialog (Resource.String.account_signature, Resource.String.account_signature_hint, ItemsAdapter.SignatureText (), (text) => {
                Account.HtmlSignature = null;
                Account.Signature = text;
                Account.Update ();
            });
            dialog.Show (FragmentManager, FRAGMENT_SIGNATURE_DIALOG, () => {
                ItemsAdapter.NotifySignatureChanged ();
            });
        }

        private void ShowSyncSelector ()
        {
            var dialog = new DaysToSyncPickerDialog (Account);
            dialog.Show (FragmentManager, FRAGMENT_SYNC_DIALOG, () => {
                ItemsAdapter.NotifySyncChanged ();
            });
        }

        private void ShowNotificationsSelector ()
        {
            var dialog = new NotificationsPickerDialog (Account);
            dialog.Show (FragmentManager, FRAGMENT_NOTIFICATIONS_DIALOG, () => {
                ItemsAdapter.NotifyNotificationsChanged ();
            });
        }

        private void ShowPasswordUpdate ()
        {
            var authType = Account.GetAuthType ();
            switch (authType) {
            case McAccount.AuthType.UserPass:
                ShowBasicPasswordUpdate ();
                break;
            case McAccount.AuthType.GoogleOAuth:
                ShowGooglePasswordUpdate ();
                break;
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AccountSettingsFragment.ShowPasswordUpdate: unknown auth type: {0}", authType));
            }
        }

        private void ShowBasicPasswordUpdate ()
        {
            var dialog = new PasswordUpdateDialog (Account);
            dialog.Show (FragmentManager, FRAGMENT_PASSWORD_DIALOG);
        }

        private void ShowGooglePasswordUpdate ()
        {
            GoogleOAuth2Authenticator.Create (Account.EmailAddr, (auth) => {
                auth.AllowCancel = true;
                auth.Completed += GoogleAuthCompleted;
                auth.Error += (object sender, AuthenticatorErrorEventArgs e) => {
                    ReturnToOurActivity ();
                };
                var intent = auth.GetUI (Activity) as CustomTabsIntent;
                // var intent = builder.Build ();
                var authUrl = auth.GetInitialUrlAsync ().Result;
                intent.LaunchUrl (Activity, Android.Net.Uri.Parse (authUrl.AbsoluteUri));
            });
        }

        private void GoogleAuthCompleted (object sender, AuthenticatorCompletedEventArgs e){
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

                if (!String.Equals (Account.EmailAddr, (string)userInfo ["email"], StringComparison.OrdinalIgnoreCase)) {
                    // Can't change your email address
                    NcAlertView.ShowMessage (this.Activity, "Settings", "You may not change your email address.  Create a new account to use a new email address.");
                    return;
                }

                var cred = Account.GetCred ();
                cred.UpdateOauth2 (access_token, refresh_token, expirationSecs);

                BackEnd.Instance.CredResp (Account.Id);
            }
            ReturnToOurActivity ();
        }

        private void ReturnToOurActivity ()
        {
            if (Activity is AccountSettingsActivity) {
                (Activity as AccountSettingsActivity).Show ();
            }
        }

        private void ShowPasswordExpiryNotice ()
        {
            var dialog = new PasswordNoticeDialog (Account, ItemsAdapter.PasswordExpiry, ItemsAdapter.PasswordRectifyUrl);
            dialog.Show (FragmentManager, FRAGMENT_PASSWORD_NOTICE_DIALOG, () => {
                ItemsAdapter.Refresh ();
            });
        }

        private void ShowAdvancedSettings ()
        {
            // FIXME: needs implementation
            // Old UI went to validate activity and showed advanced fields
            // Consider rewriting validate activity as AdvancedAccountSettingsActivity
        }

        private void ShowCertAccept ()
        {
            // FIXME: needs implementation
            // Should show the cert and allow the user to accept/decline
            // Not previously implemented in old UI
        }

        private void AttemptServerIssueFix ()
        {
            if (ItemsAdapter.ServerWithIssue.IsHardWired) {
                BackEnd.Instance.ServerConfResp (Account.Id, ItemsAdapter.ServerWithIssue.Capabilities, false);
                Activity.Finish ();
            } else {
                ShowAdvancedSettings ();
            }
        }

        #endregion
    }

    #region Adapter

    public class AccountSettingsAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener 
        {
            void OnNameSelected ();
            void OnCredIssueSelected ();
            void OnCertIssueSelected ();
            void OnServerIssueSelected ();
            void OnPasswordNoticeSelected ();
            void OnPasswordUpdateSelected ();
            void OnAdvancedSelected ();
            void OnSignatureSelected ();
            void OnSyncSelected ();
            void OnNotificationsSelected ();
        }

        public McAccount Account;
        WeakReference<Listener> WeakListener;
        public string PasswordRectifyUrl { get; private set; }
        public DateTime PasswordExpiry { get; private set; }
        public BackEndStateEnum ServerIssue { get; private set; }
        public McServer ServerWithIssue { get; private set; }

        int _GroupCount;

        int NameGroupPosition = 0;
        int NameGroupItemCount = 2;
        int AddrItemPosition = 0;
        int NameItemPosition = 1;

        int IssueGroupPosition = -1;
        int IssueGroupItemCount = 0;
        int PasswordIssuePosition = -1;
        int CertIssuePosition = -1;
        int ServerIssuePosition = -1;

        int AdvancedGroupPosition = -1;
        int AdvancedGroupItemCount = 0;
        int PasswordNoticePosition = -1;
        int UpdatePasswordPosition = -1;
        int AdvancedSettingsPosition = -1;

        int MiscGroupPosition = -1;
        int MiscGroupItemCount = 4;
        int SignaturePosition = 0;
        int SyncPosition = 1;
        int NotificationsPosition = 2;
        int FastNotifyPosition = 3;

        int DefaultGroupPosition = -1;
        int DefaultGroupItemCount = 0;
        int DefaultEmailPosition = -1;
        int DefaultCalendarPosition = -1;

        enum ViewType {
            Account,
            Basic,
            Issue,
            Switch
        }

        public AccountSettingsAdapter (Listener listener, McAccount account)
        {
            WeakListener = new WeakReference<Listener> (listener);
            Account = account;
            Refresh ();
        }

        public void Refresh ()
        {
            int groupPosition = 0;
            int position = 0;

            NameGroupPosition = -1;
            IssueGroupPosition = -1;
            AdvancedGroupPosition = -1;
            MiscGroupPosition = -1;
            DefaultGroupPosition = -1;

            position = 0;
            NameGroupPosition = groupPosition++;
            AddrItemPosition = position++;
            NameItemPosition = position++;
            NameGroupItemCount = position;

            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (Account.Id, out serverWithIssue, out serverIssue)) {
                ServerWithIssue = serverWithIssue;
                ServerIssue = serverIssue;
                position = 0;
                ServerIssuePosition = -1;
                PasswordIssuePosition = -1;
                CertIssuePosition = -1;
                IssueGroupPosition = groupPosition++;
                switch (ServerIssue) {
                case BackEndStateEnum.CredWait:
                    PasswordIssuePosition = position++;
                    break;
                case BackEndStateEnum.CertAskWait:
                    CertIssuePosition = position++;
                    break;
                case BackEndStateEnum.ServerConfWait:
                    ServerIssuePosition = position++;
                    break;
                }
                IssueGroupItemCount = position;
            } else {
                ServerWithIssue = null;
                ServerIssue = BackEndStateEnum.Running;
            }

            PasswordRectifyUrl = null;
            var creds = McCred.QueryByAccountId<McCred> (Account.Id).SingleOrDefault ();
            DateTime expriry;
            string rectifyUrl;
            bool showPasswordNotice = LoginHelpers.PasswordWillExpire (Account.Id, out expriry, out rectifyUrl);
            bool showUpdatePassword = creds != null && (creds.CredType == McCred.CredTypeEnum.Password || creds.CredType == McCred.CredTypeEnum.OAuth2) && ServerIssue != BackEndStateEnum.CredWait;
            bool showAdvanced = Account.AccountService == McAccount.AccountServiceEnum.Exchange || Account.AccountService == McAccount.AccountServiceEnum.IMAP_SMTP;
            PasswordExpiry = expriry;
            PasswordRectifyUrl = rectifyUrl;
            if (showPasswordNotice || showUpdatePassword || showAdvanced) {
                position = 0;
                PasswordNoticePosition = -1;
                UpdatePasswordPosition = -1;
                AdvancedSettingsPosition = -1;
                AdvancedGroupPosition = groupPosition++;
                if (showPasswordNotice) {
                    PasswordNoticePosition = position++;
                }
                if (showUpdatePassword) {
                    UpdatePasswordPosition = position++;
                }
                if (showAdvanced) {
                    AdvancedSettingsPosition = position++;
                }
                AdvancedGroupItemCount = position;
            }

            position = 0;
            MiscGroupPosition = groupPosition++;
            SignaturePosition = position++;
            SyncPosition = position++;
            NotificationsPosition = position++;
            FastNotifyPosition = position++;
            MiscGroupItemCount = position;

            bool isEmailSender = Account.HasCapability (McAccount.AccountCapabilityEnum.EmailSender);
            bool isCalWriter = Account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter);
            if (isEmailSender || isCalWriter) {
                position = 0;
                DefaultEmailPosition = -1;
                DefaultCalendarPosition = -1;
                DefaultGroupPosition = groupPosition++;
                if (isEmailSender) {
                    DefaultEmailPosition = position++;
                }
                if (isCalWriter) {
                    DefaultCalendarPosition = position++;
                }
                DefaultGroupItemCount = position;
            }

            _GroupCount = groupPosition;

            NotifyDataSetChanged ();
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == NameGroupPosition) {
                return NameGroupItemCount;
            } else if (groupPosition == IssueGroupPosition) {
                return IssueGroupItemCount;
            } else if (groupPosition == AdvancedGroupPosition) {
                return AdvancedGroupItemCount;
            } else if (groupPosition == MiscGroupPosition) {
                return MiscGroupItemCount;
            } else if (groupPosition == DefaultGroupPosition) {
                return DefaultGroupItemCount;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AccountSettingsFragment.GroupItemCount: Unexpecetd group position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            return null;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == NameGroupPosition) {
                if (position == AddrItemPosition) {
                    return (int)ViewType.Account;
                } else if (position == NameItemPosition) {
                    return (int)ViewType.Basic;
                }
            } else if (groupPosition == IssueGroupPosition) {
                if (position < IssueGroupItemCount) {
                    return (int)ViewType.Issue;
                }
            } else if (groupPosition == AdvancedGroupPosition) {
                if (position < AdvancedGroupItemCount) {
                    return (int)ViewType.Basic;
                }
            } else if (groupPosition == MiscGroupPosition) {
                if (position == SignaturePosition) {
                    return (int)ViewType.Basic;
                } else if (position == SyncPosition) {
                    return (int)ViewType.Basic;
                } else if (position == NotificationsPosition) {
                    return (int)ViewType.Basic;
                } else if (position == FastNotifyPosition) {
                    return (int)ViewType.Switch;
                }
            } else if (groupPosition == DefaultGroupPosition) {
                if (position == DefaultEmailPosition) {
                    return (int)ViewType.Switch;
                } else if (position == DefaultCalendarPosition) {
                    return (int)ViewType.Switch;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AccountSettingsFragment.GetItemViewType: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.Account:
                return AccountViewHolder.Create (parent);
            case ViewType.Basic:
                return SettingsBasicItemViewHolder.Create (parent);
            case ViewType.Issue:
                return IssueViewHolder.Create (parent);
            case ViewType.Switch:
                return SettingsSwitchItemViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AccountSettingsFragment.OnCreateGroupedViewHolder: Unexpecetd viewType: {0}", viewType));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var context = holder.ItemView.Context;
            if (groupPosition == NameGroupPosition) {
                if (position == AddrItemPosition) {
                    (holder as AccountViewHolder).SetAccount (Account);
                    return;
                } else if (position == NameItemPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_name), Account.DisplayName);
                    return;
                }
            } else if (groupPosition == IssueGroupPosition) {
                if (position == PasswordIssuePosition) {
                    (holder as IssueViewHolder).SetLabels (context.GetString (Resource.String.account_issue_cred));
                    return;
                }else if (position == CertIssuePosition){
                    (holder as IssueViewHolder).SetLabels (context.GetString (Resource.String.account_issue_cert));
                    return;
                }else if (position == ServerIssuePosition){
                    (holder as IssueViewHolder).SetLabels (context.GetString (Resource.String.account_issue_server));
                    return;
                }
            } else if (groupPosition == AdvancedGroupPosition) {
                if (position == PasswordNoticePosition){
                    (holder as SettingsBasicItemViewHolder).SetLabels (String.Format (context.GetString (Resource.String.account_password_expires), Pretty.ReminderDate (PasswordExpiry)));
                    return;
                } else if (position == UpdatePasswordPosition){
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_update_password));
                    return;
                }else if (position == AdvancedSettingsPosition){
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_advanced));
                    return;
                }
            } else if (groupPosition == MiscGroupPosition) {
                if (position == SignaturePosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_signature), SignatureText ());
                    return;
                } else if (position == SyncPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_sync), SyncText ());
                    return;
                } else if (position == NotificationsPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.account_notifications), NotificationsText ());
                    return;
                } else if (position == FastNotifyPosition) {
                    var switchHolder = (holder as SettingsSwitchItemViewHolder);
                    switchHolder.SetLabels (context.GetString (Resource.String.fast_notification));
                    switchHolder.Switch.Checked = Account.FastNotificationEnabled;
                    switchHolder.SetChangeHandler ((sender, e) => {
                        Account.FastNotificationEnabled = e.IsChecked;
                        Account.Update ();
                    });
                    return;
                }
            } else if (groupPosition == DefaultGroupPosition) {
                if (position == DefaultEmailPosition) {
                    var defaultAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailSender);
                    var switchHolder = (holder as SettingsSwitchItemViewHolder);
                    switchHolder.SetLabels (context.GetString (Resource.String.default_email_account));
                    switchHolder.Switch.Checked = defaultAccount != null && defaultAccount.Id == Account.Id;
                    switchHolder.Switch.Enabled = !switchHolder.Switch.Checked;
                    switchHolder.SetChangeHandler ((sender, e) => {
                        McAccount.SetDefaultAccount (Account.Id, McAccount.AccountCapabilityEnum.EmailSender);
                        (sender as Switch).Enabled = false;
                    });
                    return;
                } else if (position == DefaultCalendarPosition) {
                    var defaultAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.CalWriter);
                    var switchHolder = (holder as SettingsSwitchItemViewHolder);
                    switchHolder.SetLabels (context.GetString (Resource.String.account_default_calendar));
                    switchHolder.Switch.Checked = defaultAccount != null && defaultAccount.Id == Account.Id;
                    switchHolder.Switch.Enabled = !switchHolder.Switch.Checked;
                    switchHolder.SetChangeHandler ((sender, e) => {
                        McAccount.SetDefaultAccount (Account.Id, McAccount.AccountCapabilityEnum.CalWriter);
                        (sender as Switch).Enabled = false;
                    });
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AccountSettingsFragment.OnBindViewHolder: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == NameGroupPosition) {
                    if (position == NameItemPosition) {
                        listener.OnNameSelected ();
                    }
                } else if (groupPosition == IssueGroupPosition) {
                    if (position == PasswordIssuePosition) {
                        listener.OnCredIssueSelected ();
                    }else if (position == CertIssuePosition){
                        listener.OnCertIssueSelected ();
                    }else if (position == ServerIssuePosition){
                        listener.OnServerIssueSelected ();
                    }
                } else if (groupPosition == AdvancedGroupPosition) {
                    if (position == PasswordNoticePosition){
                        listener.OnPasswordNoticeSelected ();
                    } else if (position == UpdatePasswordPosition){
                        listener.OnPasswordUpdateSelected ();
                    }else if (position == AdvancedSettingsPosition){
                        listener.OnAdvancedSelected ();
                    }
                } else if (groupPosition == MiscGroupPosition) {
                    if (position == SignaturePosition) {
                        listener.OnSignatureSelected ();
                    } else if (position == SyncPosition) {
                        listener.OnSyncSelected ();
                    } else if (position == NotificationsPosition) {
                        listener.OnNotificationsSelected ();
                    }
                }
            }
        }

        public string SignatureText ()
        {
            if (!string.IsNullOrEmpty (Account.HtmlSignature)) {
                var serializer = new HtmlTextSerializer (Account.HtmlSignature);
                var text = serializer.Serialize ();
                return text;
            }
            return Account.Signature;
        }

        private string SyncText ()
        {
            return Pretty.MaxAgeFilter (Account.DaysToSyncEmail);
        }

        private string NotificationsText ()
        {
            return Pretty.NotificationConfiguration (Account.NotificationConfiguration);
        }

        public void NotifyNameChanged ()
        {
            NotifyItemChanged (NameGroupPosition, NameItemPosition);
        }

        public void NotifySignatureChanged ()
        {
            NotifyItemChanged (MiscGroupPosition, SignaturePosition);
        }

        public void NotifySyncChanged ()
        {
            NotifyItemChanged (MiscGroupPosition, SyncPosition);
        }

        public void NotifyNotificationsChanged ()
        {
            NotifyItemChanged (MiscGroupPosition, NotificationsPosition);
        }

        class AccountViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            ImageView AvatarImageView;
            TextView AddressTextView;

            public static AccountViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.AccountSettingsListAccountItem, parent, false);
                return new AccountViewHolder (view);
            }

            public AccountViewHolder (View view) : base (view)
            {
                AvatarImageView = view.FindViewById (Resource.Id.account_icon) as ImageView;
                AddressTextView = view.FindViewById (Resource.Id.account_email) as TextView;
            }

            public void SetAccount (McAccount account)
            {
                AvatarImageView.SetImageDrawable (Util.GetAccountImage (AvatarImageView.Context, account));
                AddressTextView.Text = account.EmailAddr;
            }
        }

        class IssueViewHolder : SettingsBasicItemViewHolder
        {

            public new static IssueViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.AccountSettingsListIssueItem, parent, false);
                return new IssueViewHolder (view);
            }

            public IssueViewHolder (View view) : base (view)
            {
            }

        }

    };

    #endregion

    /*

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

    */
}

