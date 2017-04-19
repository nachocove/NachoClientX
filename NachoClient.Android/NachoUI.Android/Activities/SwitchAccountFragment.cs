
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Support.V7.App;
using Android.Support.V7.Widget;
using Android.Support.Design.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoClient.AndroidClient
{

    public class SwitchAccountFragment : Fragment, SwitchAccountAdapter.Listener
    {

        #region Subviews

        RecyclerView ListView;
        SwitchAccountAdapter AccountsAdapter;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (Activity));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            var view = inflater.Inflate (Resource.Layout.SwitchAccountFragment, container, false);
            FindSubviews (view);
            AccountsAdapter = new SwitchAccountAdapter (this);
            ListView.SetAdapter (AccountsAdapter);
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
            AccountsAdapter.Refresh ();
        }

        #endregion

        #region Listern

        public void OnAccountSelected (McAccount account)
        {
            (Activity as MainTabsActivity).SwitchToAccount (account);
        }

        public void OnAddAccount ()
        {
            (Activity as MainTabsActivity).AddAccount ();
        }

        #endregion
    }

    public class SwitchAccountAdapter : RecyclerView.Adapter
    {

        public interface Listener
        {
            void OnAccountSelected (McAccount account);
            void OnAddAccount ();
        }

        enum ViewType
        {
            Account,
            Action
        }

        List<NcAccountMonitor.AccountInfo> Accounts;
        WeakReference<Listener> WeakListener;

        public SwitchAccountAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Refresh ();
        }

        public void Refresh ()
        {
            Accounts = new List<NcAccountMonitor.AccountInfo> ();
            if (NcAccountMonitor.Instance.Accounts.Count > 1) {
                Accounts.Add (new NcAccountMonitor.AccountInfo () {
                    Account = McAccount.GetUnifiedAccount ()
                });
            }
            Accounts.AddRange (NcAccountMonitor.Instance.Accounts);
            NotifyDataSetChanged ();
        }

        public override int ItemCount {
            get {
                return Accounts.Count + 1;
            }
        }

        public override int GetItemViewType (int position)
        {
            if (position < Accounts.Count) {
                return (int)ViewType.Account;
            }
            position -= Accounts.Count;
            if (position == 0) {
                return (int)ViewType.Action;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SwitchAccountFragment.GetItemViewType unknown position: {0}", position));
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.Account:
                return AccountViewHolder.Create (parent);
            case ViewType.Action:
                return ActionViewHolder.Create (parent);
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SwitchAccountFragment.OnCreateViewHolder unknown view type: {0}", viewType));
            }
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            if (position < Accounts.Count) {
                var accountInfo = Accounts [position];
                (holder as AccountViewHolder).SetAccountInfo (accountInfo, (sender, e) => {
                    Listener listener;
                    if (WeakListener.TryGetTarget (out listener)) {
                        listener.OnAccountSelected (accountInfo.Account);
                    }
                });
                return;
            }
            position -= Accounts.Count;
            if (position == 0) {
                (holder as ActionViewHolder).SetAction (Resource.Drawable.switcher_action_add_account, Resource.String.switcher_action_add_account, (sender, e) => {
                    Listener listener;
                    if (WeakListener.TryGetTarget (out listener)) {
                        listener.OnAddAccount ();
                    }
                });
                return;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SwitchAccountFragment.OnBindViewHolder unknown position: {0}", position));
        }

        class AccountViewHolder : RecyclerView.ViewHolder
        {

            ImageView AvatarImageView;
            TextView NameTextView;
            TextView AddressTextView;
            TextView UnreadTextView;
            EventHandler ClickHandler;

            public static AccountViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.SwitchAccountListAccountItem, parent, false);
                return new AccountViewHolder (view);
            }

            public AccountViewHolder (View view) : base (view)
            {
                AvatarImageView = view.FindViewById (Resource.Id.account_icon) as ImageView;
                NameTextView = view.FindViewById (Resource.Id.account_name) as TextView;
                AddressTextView = view.FindViewById (Resource.Id.account_email) as TextView;
                UnreadTextView = view.FindViewById (Resource.Id.account_unread) as TextView;
            }

            public void SetAccountInfo (NcAccountMonitor.AccountInfo accountInfo, EventHandler clickHandler)
            {
                var account = accountInfo.Account;
                AvatarImageView.SetImageDrawable (Util.GetAccountImage (AvatarImageView.Context, account));
                if (String.IsNullOrEmpty (account.DisplayName)) {
                    NameTextView.Text = account.EmailAddr;
                    AddressTextView.Visibility = ViewStates.Gone;
                } else {
                    NameTextView.Text = account.DisplayName;
                    if (String.IsNullOrEmpty (account.EmailAddr)) {
                        AddressTextView.Visibility = ViewStates.Gone;
                    }else {
                        AddressTextView.Text = account.EmailAddr;
                        AddressTextView.Visibility = ViewStates.Visible;
                    }
                }
                UnreadTextView.Text = Pretty.LimitedBadgeCount (accountInfo.UnreadCount);
                if (accountInfo.UnreadCount == 0) {
                    UnreadTextView.Visibility = ViewStates.Gone;
                } else {
                    UnreadTextView.Visibility = ViewStates.Visible;
                }
                if (ClickHandler != null) {
                    ItemView.Click -= ClickHandler;
                }
                ClickHandler = clickHandler;
                if (ClickHandler != null){
                    ItemView.Click += ClickHandler;
                }
            }
        }

        class ActionViewHolder : RecyclerView.ViewHolder
        {

        	ImageView IconView;
        	TextView LabelTextView;
            EventHandler ClickHandler;

            public static ActionViewHolder Create (ViewGroup parent)
        	{
        		var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.SwitchAccountListActionItem, parent, false);
                return new ActionViewHolder (view);
        	}

            public ActionViewHolder (View view) : base (view)
        	{
                IconView = view.FindViewById (Resource.Id.action_icon) as ImageView;
                LabelTextView = view.FindViewById (Resource.Id.action_label) as TextView;
        	}

            public void SetAction (int iconResource, int actionResource, EventHandler clickHandler)
            {
                IconView.SetImageResource (iconResource);
                LabelTextView.SetText (actionResource);
                if (ClickHandler != null) {
                    ItemView.Click -= ClickHandler;
                }
                ClickHandler = clickHandler;
                if (ClickHandler != null){
                    ItemView.Click += ClickHandler;
                }
            }
        }
    }

    /*
    public interface AccountListDelegate
    {
        void AddAccount ();

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

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.AccountSwitcher);
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
            LoginHelpers.SetSwitchAwayTime (NcApplication.Instance.Account.Id);
            LoginHelpers.SetMostRecentAccount (account.Id);
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
                UpdateUnreadCounts (account, holder.ItemView);
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
                var count = holder.ItemView.FindViewById<Android.Widget.TextView> (Resource.Id.unread_count);
                if ((DisplayMode.SettingsListview == displayMode) && LoginHelpers.ShouldAlertUser (account.Id)) {
                    alert.Visibility = ViewStates.Visible;
                    count.Visibility = ViewStates.Gone;
                } else {
                    alert.Visibility = ViewStates.Gone;
                    if (DisplayMode.AccountSwitcher == displayMode) {
                        count.Visibility = ViewStates.Visible;
                        UpdateUnreadMessageCount (account, count);
                    } else {
                        count.Visibility = ViewStates.Gone;
                    }
                }
            }
        }

        public void UpdateUnreadCounts (McAccount account, View view)
        {
            NcTask.Run (() => {
                int unreadCount;
                int likelyCount;
                EmailHelper.GetMessageCounts (account, out unreadCount, out likelyCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    var unreadView = view.FindViewById<Android.Widget.TextView> (Resource.Id.to_inbox);
                    unreadView.Text = String.Format ("Go to Inbox ({0:N0} unread)", unreadCount);
                    // FIMXE LTR.
                });
            }, "UpdateUnreadMessageView");
        }

        void UpdateUnreadMessageCount (McAccount account, Android.Widget.TextView unreadView)
        {
            NcTask.Run (() => {
                int unreadMessageCount;
                EmailHelper.GetUnreadMessageCount (account, out unreadMessageCount);
                InvokeOnUIThread.Instance.Invoke (() => {
                    unreadView.Text = String.Format ("({0:N0})", unreadMessageCount);
                });
            }, "UpdateUnreadMessageCount");
        }

        void OnClick (int position, int viewType, int resourceId)
        {
            switch (viewType) {
            case HEADER_TYPE:
                switch (resourceId) {
                case 0:
                    break;
                case Resource.Id.go_to_inbox:
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
    */
}

