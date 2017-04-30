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
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class SettingsFragment : Fragment, SettingsAdapter.Listener
    {

        private const string FRAGMENT_UNREAD_COUNT_PICKER = "NachoClient.AndroidClient.SettingsFragment.FRAGMENT_UNREAD_COUNT_PICKER";
        private const int REQUEST_ACCOUNT_SETTINGS = 1;

        private int RequestedAccountId;

        #region Subviews

        RecyclerView RecyclerView;
        SettingsAdapter ItemsAdapter;

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
            ItemsAdapter = new SettingsAdapter (this);
            RecyclerView.SetAdapter (ItemsAdapter);

            return view;
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region User Actions

        public void OnUnreadCountSelected ()
        {
            ShowUnredCountSelector ();
        }

        public void OnAccountSelected (McAccount account)
        {
            ShowAccountSettings (account);
        }

        public void OnAboutSelected ()
        {
            ShowAbout ();
        }

        public override void OnActivityResult (int requestCode, Result resultCode, Intent data)
        {
            switch (requestCode) {
            case REQUEST_ACCOUNT_SETTINGS:
                HandleAccountSettingsResult (resultCode, RequestedAccountId);
                break;
            default:
                base.OnActivityResult (requestCode, resultCode, data);
                break;
            }
        }

        #endregion

        #region Public API

        public void Refresh ()
        {
            ItemsAdapter.Refresh ();
        }

        #endregion

        #region Private Helpers

        void ShowUnredCountSelector ()
        {
            var dialog = new UnreadCountPickerDialog ();
            dialog.Show (FragmentManager, FRAGMENT_UNREAD_COUNT_PICKER, () => {
                ItemsAdapter.NotifyUnreadCountChanged ();
            });
        }

        void ShowAbout ()
        {
            var intent = AboutActivity.BuildIntent (Activity);
            StartActivity (intent);
        }

        void ShowAccountSettings (McAccount account)
        {
            RequestedAccountId = account.Id;
            var intent = AccountSettingsActivity.BuildIntent (Activity, RequestedAccountId);
            StartActivityForResult (intent, REQUEST_ACCOUNT_SETTINGS);
        }

        void HandleAccountSettingsResult (Result resultCode, int accountId)
        {
            if (resultCode == AccountSettingsActivity.RESULT_DELETED) {
                ItemsAdapter.NotifyAccountRemoved (accountId);
                if (ItemsAdapter.Accounts.Count == 0) {
                    MainTabsActivity.ShowSetup (Activity);
                }
            } else {
                if (accountId == NcApplication.Instance.Account.Id) {
                    NcApplication.Instance.Account = McAccount.QueryById<McAccount> (accountId);
                }
                ItemsAdapter.NotifyAccountChanged (accountId);
            }
        }

        #endregion

    }

    #region Item Adapter

    class SettingsAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnUnreadCountSelected ();
            void OnAccountSelected (McAccount account);
            void OnAboutSelected ();
        }

        int GeneralGroupPosition = 0;
        int GeneralItemCount = 1;
        int UnreadCountPosition = 0;

        int AccountGroupPosition = 1;

        int AboutGroupPosition = 2;
        int AboutItemCount = 1;
        int AboutPosition = 0;

        public List<McAccount> Accounts { get; private set; }
        WeakReference<Listener> WeakListener;

        enum ViewType
        {
            Basic,
            Account
        }

        public SettingsAdapter (Listener listener)
        {
            WeakListener = new WeakReference<Listener> (listener);
            Refresh ();
        }

        public void Refresh ()
        {
            Accounts = McAccount.GetAllConfiguredNormalAccounts ();
            NotifyDataSetChanged ();
        }

        public void NotifyUnreadCountChanged ()
        {
            NotifyItemChanged (GeneralGroupPosition, UnreadCountPosition);
        }

        public void NotifyAccountChanged (int accountId)
        {
            for (var i = 0; i < Accounts.Count; ++i) {
                if (Accounts [i].Id == accountId) {
                    Accounts [i] = McAccount.QueryById<McAccount> (accountId);
                    NotifyItemChanged (AccountGroupPosition, i);
                    break;
                }
            }
        }

        public void NotifyAccountRemoved (int accountId)
        {
            for (var i = 0; i < Accounts.Count; ++i) {
                if (Accounts [i].Id == accountId) {
                    Accounts.RemoveAt (i);
                    NotifyItemRemoved (AccountGroupPosition, i);
                    break;
                }
            }
        }

        public override int GroupCount {
            get {
                return 3;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == GeneralGroupPosition) {
                return GeneralItemCount;
            } else if (groupPosition == AccountGroupPosition) {
                return Accounts.Count;
            } else if (groupPosition == AboutGroupPosition) {
                return AboutItemCount;
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.GroupItemCount: Unexpecetd group position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == GeneralGroupPosition) {
                return null;
            } else if (groupPosition == AccountGroupPosition) {
                return context.GetString (Resource.String.settings_accounts);
            } else if (groupPosition == AboutGroupPosition) {
                return null;
            }
            return null;
        }

        public override int GetItemViewType (int groupPosition, int position)
        {
            if (groupPosition == GeneralGroupPosition) {
                if (position == UnreadCountPosition) {
                    return (int)ViewType.Basic;
                }
            } else if (groupPosition == AccountGroupPosition) {
                if (position < Accounts.Count) {
                    return (int)ViewType.Account;
                }
            } else if (groupPosition == AboutGroupPosition) {
                if (position == AboutPosition) {
                    return (int)ViewType.Basic;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.GetItemViewType: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            switch ((ViewType)viewType) {
            case ViewType.Basic:
                return SettingsBasicItemViewHolder.Create (parent);
            case ViewType.Account:
                return AccountViewHolder.Create (parent);
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.OnCreateGroupedViewHolder: Unexpecetd viewType: {0}", viewType));
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var context = holder.ItemView.Context;
            if (groupPosition == GeneralGroupPosition) {
                if (position == UnreadCountPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.settings_unread_count), ValueForUnreadCount (holder.ItemView.Context));
                    return;
                }
            } else if (groupPosition == AccountGroupPosition) {
                if (position < Accounts.Count) {
                    (holder as AccountViewHolder).SetAccount (Accounts [position]);
                    return;
                }
            } else if (groupPosition == AboutGroupPosition) {
                if (position == AboutPosition) {
                    (holder as SettingsBasicItemViewHolder).SetLabels (context.GetString (Resource.String.settings_about));
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.OnBindViewHolder: Unexpecetd position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            Listener listener;
            if (WeakListener.TryGetTarget (out listener)) {
                if (groupPosition == GeneralGroupPosition) {
                    if (position == UnreadCountPosition) {
                        listener.OnUnreadCountSelected ();
                    }
                } else if (groupPosition == AccountGroupPosition) {
                    if (position < Accounts.Count) {
                        listener.OnAccountSelected (Accounts [position]);
                    }
                } else if (groupPosition == AboutGroupPosition) {
                    if (position == AboutPosition) {
                        listener.OnAboutSelected ();
                    }
                }
            }
        }

        protected string ValueForUnreadCount (Context context)
        {
            switch (EmailHelper.HowToDisplayUnreadCount ()) {
            case EmailHelper.ShowUnreadEnum.AllMessages:
                return context.GetString (Resource.String.settings_unread_count_all);
            case EmailHelper.ShowUnreadEnum.RecentMessages:
                return context.GetString (Resource.String.settings_unread_count_recent);
            case EmailHelper.ShowUnreadEnum.TodaysMessages:
                return context.GetString (Resource.String.settings_unread_count_today);
            default:
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("SettingsFragment.ValueForUnreadCount: Unexpecetd unread setting: {0}", EmailHelper.HowToDisplayUnreadCount ()));
            }
        }

        class AccountViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            ImageView AvatarImageView;
            TextView NameTextView;
            TextView AddressTextView;
            View ErrorIndicatorView;

            public static AccountViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.SettingsListAccountItem, parent, false);
                return new AccountViewHolder (view);
            }

            public AccountViewHolder (View view) : base (view)
            {
                AvatarImageView = view.FindViewById (Resource.Id.account_icon) as ImageView;
                NameTextView = view.FindViewById (Resource.Id.account_name) as TextView;
                AddressTextView = view.FindViewById (Resource.Id.account_email) as TextView;
                ErrorIndicatorView = view.FindViewById (Resource.Id.account_error_indicator);
            }

            public void SetAccount (McAccount account)
            {
                AvatarImageView.SetImageDrawable (Util.GetAccountImage (AvatarImageView.Context, account));
                if (String.IsNullOrEmpty (account.DisplayName)) {
                    NameTextView.Text = account.EmailAddr;
                    AddressTextView.Visibility = ViewStates.Gone;
                } else {
                    NameTextView.Text = account.DisplayName;
                    AddressTextView.Text = account.EmailAddr;
                    AddressTextView.Visibility = ViewStates.Visible;
                }
                if (LoginHelpers.ShouldAlertUser (account.Id)) {
                    ErrorIndicatorView.Visibility = ViewStates.Visible;
                } else {
                    ErrorIndicatorView.Visibility = ViewStates.Gone;
                }
            }
        }
    }

    public class SettingsBasicItemViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
    {

        TextView NameTextView;
        TextView DetailTextView;

        public static SettingsBasicItemViewHolder Create (ViewGroup parent)
        {
            var inflater = LayoutInflater.From (parent.Context);
            var view = inflater.Inflate (Resource.Layout.SettingsListBasicItem, parent, false);
            return new SettingsBasicItemViewHolder (view);
        }

        public SettingsBasicItemViewHolder (View view) : base (view)
        {
            NameTextView = view.FindViewById (Resource.Id.setting_name) as TextView;
            DetailTextView = view.FindViewById (Resource.Id.setting_detail) as TextView;
        }

        public void SetLabels (string name, string detail = null)
        {
            NameTextView.Text = name;
            if (String.IsNullOrEmpty (detail)) {
                DetailTextView.Visibility = ViewStates.Gone;
            } else {
                DetailTextView.Visibility = ViewStates.Visible;
                DetailTextView.Text = detail;
            }
        }

    }

    public class SettingsSwitchItemViewHolder : SettingsBasicItemViewHolder
    {

        public Switch Switch { get; private set; }
        EventHandler<Android.Widget.Switch.CheckedChangeEventArgs> ChangeHandler;

        public new static SettingsSwitchItemViewHolder Create (ViewGroup parent)
        {
            var inflater = LayoutInflater.From (parent.Context);
            var view = inflater.Inflate (Resource.Layout.SettingsListSwitchItem, parent, false);
            return new SettingsSwitchItemViewHolder (view);
        }

        public SettingsSwitchItemViewHolder (View view) : base (view)
        {
            Switch = view.FindViewById (Resource.Id.toggle_switch) as Switch;
        }

        public void SetChangeHandler (EventHandler<Android.Widget.Switch.CheckedChangeEventArgs> changeHandler)
        {
            if (ChangeHandler != null) {
                Switch.CheckedChange -= ChangeHandler;
            }
            ChangeHandler = changeHandler;
            if (changeHandler != null) {
                Switch.CheckedChange += changeHandler;
            }
        }

    }

    #endregion

}

