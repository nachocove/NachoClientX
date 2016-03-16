
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
using NachoCore.SFDC;

namespace NachoClient.AndroidClient
{
    public class SalesforceSettingsFragment : Fragment
    {
        McAccount account;

        ButtonBar buttonBar;

        ImageView accountIcon;
        TextView accountName;
               
        Switch addBccSwitch;

        View refreshContactsView;
        View deleteAccountView;
        TextView refreshLabel;

        View accountIssuesView;

        TextView accountIssue;
        View accountIssueView;

        TextView statusLabel;

        bool IsRefreshing = false;
        int ContactCount = 0;
        bool IsListeningForStatusInd;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            account = ((IAccountSettingsFragmentOwner)this.Activity).AccountToView;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            RequeryContactCount ();
            CheckForRefreshStatus ();

            var view = inflater.Inflate (Resource.Layout.SalesforceSettingsFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.salesforce_settings);

            statusLabel = view.FindViewById<TextView> (Resource.Id.salesforce_label);

            accountIcon = view.FindViewById<ImageView> (Resource.Id.account_icon);
            accountName = view.FindViewById<TextView> (Resource.Id.account_name);

            addBccSwitch = view.FindViewById<Switch> (Resource.Id.add_bcc_switch);
            addBccSwitch.CheckedChange += AddBccSwitch_CheckedChange;

            refreshContactsView = view.FindViewById <View> (Resource.Id.refresh_contacts_view);
            refreshContactsView.Click += RefreshContactsView_Click;
            refreshLabel = view.FindViewById<TextView> (Resource.Id.refresh_contacts_label);

            accountIssuesView = view.FindViewById<View> (Resource.Id.account_issues_view);

            accountIssue = view.FindViewById<TextView> (Resource.Id.account_issue);
            accountIssueView = view.FindViewById<View> (Resource.Id.account_issue_view);
            accountIssueView.Click += AccountIssueView_Click;

            McServer serverWithIssue;
            BackEndStateEnum serverIssue;
            if (LoginHelpers.IsUserInterventionRequired (account.Id, out serverWithIssue, out serverIssue)) {
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
                accountIssuesView.Visibility = ViewStates.Gone;
            }

            deleteAccountView = view.FindViewById<View> (Resource.Id.delete_account_view);
            deleteAccountView.Click += DeleteAccountView_Click;

            UpdateView ();

            return view;
        }

        public override void OnStart ()
        {
            base.OnStart ();
            StartListeningForStatusInd ();
        }

        public override void OnStop ()
        {
            StopListeningForStatusInd ();
            base.OnStop ();
        }

        void UpdateView ()
        {
            accountIcon.SetImageResource (Util.GetAccountServiceImageId (account.AccountService));
            accountName.Text = account.EmailAddr;

            addBccSwitch.Checked = SalesForceProtoControl.ShouldAddBccToEmail (account.Id);

            statusLabel.Text = InfoText ();

            if (IsRefreshing) {
                refreshLabel.Text = "Fetching Contacts...";
                refreshLabel.Alpha = 0.5f;
            } else {
                refreshLabel.Text = "Refresh Contacts";
                refreshLabel.Alpha = 1.0f;
            }
        }

        public override void OnSaveInstanceState (Bundle outState)
        {
            base.OnSaveInstanceState (outState);
        }

        void AccountIssueView_Click (object sender, EventArgs e)
        {
            ConnectToSalesforce ();
        }

        Intent SFDCSignInIntent;
        public void ConnectToSalesforce ()
        {
            SFDCSignInIntent = new Intent (this.Activity, typeof(SalesforceSignInActivity));
            StartActivity (SFDCSignInIntent);
        }

        void RefreshContactsView_Click (object sender, EventArgs e)
        {
            if (!IsRefreshing) {
                BackEnd.Instance.SyncContactsCmd (account.Id);
                CheckForRefreshStatus ();
                UpdateView ();
            }
        }

        void DeleteAccountView_Click (object sender, EventArgs e)
        {
            // Deletes the account & returns to the main screen
            var intent = RemoveAccountActivity.RemoveAccountIntent (this.Activity, account);
            StartActivity (intent);
        }

        void AddBccSwitch_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            SalesForceProtoControl.SetShouldAddBccToEmail (account.Id, e.IsChecked);
        }

        void CheckForRefreshStatus ()
        {
            IsRefreshing = false;
            var pendings = McPending.QueryByOperation (account.Id, McPending.Operations.Sync);
            foreach (var pending in pendings) {
                if (pending.State == McPending.StateEnum.Failed) {
                    pending.Delete ();
                } else {
                    IsRefreshing = true;
                }
            }
            var status = BackEnd.Instance.BackEndState (account.Id, SalesForceProtoControl.SalesForceCapabilities);
            bool isServerReady = status == BackEndStateEnum.PostAutoDPostInboxSync;
            bool isServerWaiting = status == BackEndStateEnum.CertAskWait || status == BackEndStateEnum.CredWait || status == BackEndStateEnum.ServerConfWait;
            IsRefreshing = !isServerWaiting && (IsRefreshing || !isServerReady);
        }

        void RequeryContactCount ()
        {
            ContactCount = McContact.CountByAccountId (account.Id);
        }

        string InfoText ()
        {
            if (ContactCount == 0 && IsRefreshing) {
                return String.Format ("Connected to your Salesforce account\nsyncing contacts...", ContactCount);
            } else {
                return String.Format ("Connected to your Salesforce account\n{0} contacts synced", ContactCount);
            }
        }

        void StartListeningForStatusInd ()
        {
            if (!IsListeningForStatusInd) {
                IsListeningForStatusInd = true;
                NcApplication.Instance.StatusIndEvent += StatusIndCallback;
            }
        }

        void StopListeningForStatusInd ()
        {
            if (IsListeningForStatusInd) {
                NcApplication.Instance.StatusIndEvent -= StatusIndCallback;
                IsListeningForStatusInd = false;
            }
        }

        void StatusIndCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (s.Status.SubKind == NcResult.SubKindEnum.Info_BackEndStateChanged) {
                if (s.Account != null && s.Account.Id == account.Id) {
                    CheckForRefreshStatus ();
                    RequeryContactCount ();
                    UpdateView ();
                }
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Error_SyncFailed) {
                IsRefreshing = false;
                RequeryContactCount ();
                UpdateView ();
                NcAlertView.ShowMessage (Activity, "Contact Fetch Failed", "Sorry, the contact fetch failed.  Please try again");
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Info_SyncSucceeded) {
                IsRefreshing = false;
                RequeryContactCount ();
                UpdateView ();
            }
            if (s.Status.SubKind == NcResult.SubKindEnum.Error_AuthFailBlocked) {
                IsRefreshing = false;
                RequeryContactCount ();
                UpdateView ();
                // TODO: auth
            }
        }

    }
}

