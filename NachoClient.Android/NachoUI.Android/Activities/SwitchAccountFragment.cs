
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
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public interface AccountListDelegate
    {
        void AddAccount ();

        void ConnectToSalesforce ();

        void AccountSelected (McAccount account);

        void AccountShortcut (int destination);
    }

    public class SwitchAccountFragment : Fragment
    {
        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        AccountAdapter accountAdapter;

        public static SwitchAccountFragment newInstance ()
        {
            var fragment = new SwitchAccountFragment ();
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.SwitchAccountFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookSwitchAccountView (view);

            var accountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            accountButton.SetImageResource (Resource.Drawable.gen_avatar_backarrow);

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.AccountSwitcher, false);
            accountAdapter.AddAccount += AccountAdapter_AddAccount;
            accountAdapter.AccountShortcut += AccountAdapter_AccountShortcut;
            accountAdapter.AccountSelected += AccountAdapter_AccountSelected;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            accountAdapter.Refresh ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void AccountAdapter_AccountSelected (object sender, McAccount account)
        {
            NcApplication.Instance.Account = account;
            var parent = (AccountListDelegate)Activity;
            parent.AccountSelected (account);
        }

        void AccountAdapter_AccountShortcut (object sender, int shortcut)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AccountShortcut (shortcut);
        }

        void AccountAdapter_AddAccount (object sender, EventArgs e)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AddAccount ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_AccountSetChanged:
                accountAdapter.Refresh ();
                break;
            }
        }

    }

    public class AccountAdapter : RecyclerView.Adapter
    {
        const int HEADER_TYPE = 1;
        const int FOOTER_TYPE = 2;
        const int ROW_TYPE = 3;

        public enum DisplayMode
        {
            AccountSwitcher,
            SettingsListview,
        };

        public event EventHandler AddAccount;
        public event EventHandler ConnectToSalesforce;
        public event EventHandler<int> AccountShortcut;
        public event EventHandler<McAccount> AccountSelected;

        bool showUnified;
        bool showSalesforce;
        public DisplayMode displayMode;

        List<McAccount> accounts;

        public  AccountAdapter (DisplayMode displayMode, bool showUnified = true, bool showSalesforce = false)
        {
            this.displayMode = displayMode;
            this.showUnified = showUnified;
            this.showSalesforce = showSalesforce;

            Refresh ();
        }

        public void Refresh ()
        {
            accounts = new List<McAccount> ();

            foreach (var account in McAccount.GetAllConfiguredNormalAccounts()) {
                if (McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress) {
                    accounts.Add (account);
                }
            }

            // Remove the device account (for now)
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null != deviceAccount) {
                accounts.RemoveAll ((McAccount account) => (account.Id == deviceAccount.Id));
            }

            if (showUnified && accounts.Count > 1) {
                var unifiedAccount = McAccount.GetUnifiedAccount ();
                accounts.Insert (0, unifiedAccount);
            }

            if (showSalesforce) {
                var salesforceAccount = McAccount.GetSalesForceAccount ();
                if (null != salesforceAccount) {
                    accounts.Add (salesforceAccount);
                }
            }

            // Remove the current account from the switcher view.
            if (DisplayMode.AccountSwitcher == displayMode) {
                accounts.RemoveAll ((McAccount account) => (account.Id == NcApplication.Instance.Account.Id));
            }

            NotifyDataSetChanged ();
        }

        class AccountHolder : RecyclerView.ViewHolder
        {
            public AccountHolder (View view, Action<int, int, int> listener, int viewType) : base (view)
            {
                if (ROW_TYPE == viewType) {
                    view.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, 0);
                }

                if (HEADER_TYPE == viewType) {
                    var settingsButton = view.FindViewById<Android.Widget.Button> (Resource.Id.account_settings);
                    settingsButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.account_settings);
                    var inboxButton = view.FindViewById<View> (Resource.Id.go_to_inbox);
                    inboxButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.go_to_inbox);
                    var deferredButton = view.FindViewById<View> (Resource.Id.go_to_deferred);
                    deferredButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.go_to_deferred);
                    var deadlinesButton = view.FindViewById<View> (Resource.Id.go_to_deadlines);
                    deadlinesButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.go_to_deadlines);
                }

                if (FOOTER_TYPE == viewType) {
                    var addAccountButton = view.FindViewById (Resource.Id.add_account);
                    addAccountButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.add_account);
                    var connectToSalesForceButton = view.FindViewById (Resource.Id.connect_to_salesforce);
                    connectToSalesForceButton.Click += (object sender, EventArgs e) => listener (AdapterPosition, viewType, Resource.Id.connect_to_salesforce);
                }
            }
        }

        public override int GetItemViewType (int position)
        {
            // Switcher has a header
            if (DisplayMode.AccountSwitcher == displayMode) {
                if (0 == position) {
                    return HEADER_TYPE;
                }
            }
            if ((ItemCount - 1) == position) {
                return FOOTER_TYPE;
            }
            return ROW_TYPE;
        }

        public override int ItemCount {
            get {
                switch (displayMode) {
                case DisplayMode.AccountSwitcher:
                    return accounts.Count + 2; // header and footer
                case DisplayMode.SettingsListview:
                    return accounts.Count + 1; // plus 1 for footer
                }
                return 0;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            int resId = 0;

            switch (viewType) {
            case HEADER_TYPE:
                resId = Resource.Layout.account_header;
                break;
            case ROW_TYPE:
                resId = Resource.Layout.account_row;
                break;
            case FOOTER_TYPE:
                resId = Resource.Layout.account_footer;
                break;
            }
            var view = LayoutInflater.From (parent.Context).Inflate (resId, parent, false);
            return new AccountHolder (view, OnClick, viewType);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            McAccount account = null;
            switch (holder.ItemViewType) {
            case FOOTER_TYPE:
                var salesforceView = holder.ItemView.FindViewById (Resource.Id.connect_to_salesforce);
                if (showSalesforce && (null == McAccount.GetSalesForceAccount ())) {
                    salesforceView.Visibility = ViewStates.Visible;
                } else {
                    salesforceView.Visibility = ViewStates.Gone;
                }
                return;
            case HEADER_TYPE:
                account = NcApplication.Instance.Account;
                var settingsButton = holder.ItemView.FindViewById (Resource.Id.account_settings);
                if (McAccount.GetUnifiedAccount ().Id == account.Id) {
                    settingsButton.Visibility = ViewStates.Gone;
                } else {
                    settingsButton.Visibility = ViewStates.Visible;
                }
                break;
            case ROW_TYPE:
                int accountIndex = (DisplayMode.AccountSwitcher == displayMode ? position - 1 : position);
                account = accounts [accountIndex];
                break;
            }

            var icon = holder.ItemView.FindViewById<Android.Widget.ImageView> (Resource.Id.account_icon);
            var name = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_name);
            var email = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_email);

            icon.SetImageResource (Util.GetAccountServiceImageId (account.AccountService));
            name.Text = Pretty.AccountName (account);
            email.Text = account.EmailAddr;

            if (ROW_TYPE == holder.ItemViewType) {
                var alert = holder.ItemView.FindViewById<Android.Widget.ImageView> (Resource.Id.account_alert);
                if ((DisplayMode.SettingsListview == displayMode) && LoginHelpers.ShouldAlertUser (account.Id)) {
                    alert.Visibility = ViewStates.Visible;
                } else {
                    alert.Visibility = ViewStates.Gone;
                }
            }
        }

        void OnClick (int position, int viewType, int resourceId)
        {
            switch (viewType) {
            case HEADER_TYPE:
                switch (resourceId) {
                case 0:
                    break;
                case Resource.Id.go_to_inbox:
                case Resource.Id.go_to_deferred:
                case Resource.Id.go_to_deadlines:
                case Resource.Id.account_settings:
                    if (null != AccountShortcut) {
                        AccountShortcut (this, resourceId);
                    }
                    break;
                }
                break;
            case ROW_TYPE:
                int accountIndex = (DisplayMode.AccountSwitcher == displayMode ? position - 1 : position);
                if (AccountSelected != null) {
                    AccountSelected (this, accounts [accountIndex]);
                }
                break;
            case FOOTER_TYPE:
                switch (resourceId) {
                case Resource.Id.add_account:
                    if (AddAccount != null) {
                        AddAccount (this, null);
                    }
                    break;
                case Resource.Id.connect_to_salesforce:
                    if (ConnectToSalesforce != null) {
                        ConnectToSalesforce (this, null);
                    }
                    break;
                }
                break;
            }
        }
    }
}

