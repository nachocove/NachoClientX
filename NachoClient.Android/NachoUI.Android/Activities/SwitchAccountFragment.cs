
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

        void AccountSelected (McAccount account);
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

            var activity = (NcActivity)this.Activity;
            activity.HookSwitchAccountView (view);

            var accountButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.account);
            accountButton.SetImageResource (Resource.Drawable.gen_avatar_backarrow);

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.AccountSwitcher);
            accountAdapter.AddAccount += AccountAdapter_AddAccount;
            accountAdapter.AccountSelected += AccountAdapter_AccountSelected;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            return view;
        }

        void AccountAdapter_AccountSelected (object sender, McAccount account)
        {
            NcApplication.Instance.Account = account;
            var parent = (AccountListDelegate)Activity;
            parent.AccountSelected (account);
        }

        void AccountAdapter_AddAccount (object sender, EventArgs e)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AddAccount ();
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
        public event EventHandler<McAccount> AccountSelected;

        public DisplayMode displayMode;

        List<McAccount> accounts;

        public  AccountAdapter (DisplayMode displayMode)
        {
            this.displayMode = displayMode;

            Refresh ();
        }

        public void Refresh ()
        {
            accounts = new List<McAccount> ();

            foreach (var account in NcModel.Instance.Db.Table<McAccount> ()) {
                if (McAccount.ConfigurationInProgressEnum.Done == account.ConfigurationInProgress) {
                    accounts.Add (account);
                }
            }

            // Remove the device account (for now)
            var deviceAccount = McAccount.GetDeviceAccount ();
            if (null != deviceAccount) {
                accounts.RemoveAll ((McAccount account) => (account.Id == deviceAccount.Id));
            }

            // Remove the current account from the switcher view.
            if (DisplayMode.AccountSwitcher == displayMode) {
                accounts.RemoveAll ((McAccount account) => (account.Id == NcApplication.Instance.Account.Id));
            }
        }

        class AccountHolder : RecyclerView.ViewHolder
        {
            public AccountHolder (View view, Action<int> listener) : base (view)
            {
                view.Click += (object sender, EventArgs e) => listener (AdapterPosition);
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
            return new AccountHolder (view, OnClick);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            McAccount account = null;
            switch (holder.ItemViewType) {
            case FOOTER_TYPE:
                return;
            case HEADER_TYPE:
                account = NcApplication.Instance.Account;
                break;
            case ROW_TYPE:
                account = accounts [position - 1];
                break;
            }

            var icon = holder.ItemView.FindViewById<Android.Widget.ImageView> (Resource.Id.account_icon);
            var name = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_name);
            var email = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.account_email);

            icon.SetImageResource (Util.GetAccountServiceImageId (account.AccountService));
            name.Text = Pretty.AccountName (account);
            email.Text = account.EmailAddr;
        }

        void OnClick (int position)
        {
            if (0 == position) {
                // Header
                return;
            } else if ((ItemCount - 1) == position) {
                // Footer
                if (AddAccount != null) {
                    AddAccount (this, null);
                }
            } else {
                // Account selected, do we need to adjust position on account of the header?
                int accountIndex = (DisplayMode.AccountSwitcher == displayMode ? position - 1 : position);
                if (AccountSelected != null) {
                    AccountSelected (this, accounts [accountIndex]);
                }
            }
        }

    }
}

