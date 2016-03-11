
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

        void RefreshContactsView_Click (object sender, EventArgs e)
        {
            BackEnd.Instance.SyncCmd (account.Id, 0);
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

