
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

        View accountIssuesView;

        TextView accountIssue;
        View accountIssueView;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            account = ((IAccountSettingsFragmentOwner)this.Activity).AccountToView;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SalesforceSettingsFragment, container, false);

            buttonBar = new ButtonBar (view);

            buttonBar.SetTitle (Resource.String.salesforce_settings);

            accountIcon = view.FindViewById<ImageView> (Resource.Id.account_icon);
            accountName = view.FindViewById<TextView> (Resource.Id.account_name);

            addBccSwitch = view.FindViewById<Switch> (Resource.Id.add_bcc_switch);
            addBccSwitch.CheckedChange += AddBccSwitch_CheckedChange;

            refreshContactsView = view.FindViewById <View> (Resource.Id.refresh_contacts_view);
            refreshContactsView.Click += RefreshContactsView_Click;

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

            BindAccount ();

            return view;
        }

        void BindAccount ()
        {
            accountIcon.SetImageResource (Util.GetAccountServiceImageId (account.AccountService));
            accountName.Text = account.EmailAddr;

            addBccSwitch.Checked = SalesForceProtoControl.ShouldAddBccToEmail (account.Id);
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
            BackEnd.Instance.SyncContactsCmd (account.Id);
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
    }
}

