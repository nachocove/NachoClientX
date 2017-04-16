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

        #endregion

        #region Private Helpers

        void ShowUnredCountSelector ()
        {
        }

        void ShowAbout ()
        {
            var intent = AboutActivity.BuildIntent (Activity);
            StartActivity (intent);
        }

        void ShowAccountSettings (McAccount account) 
        {
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

        List<McAccount> Accounts;
        WeakReference<Listener> WeakListener;

        enum ViewType {
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

    #endregion

        /*

        RecyclerView recyclerView;
        RecyclerView.LayoutManager layoutManager;
        AccountAdapter accountAdapter;

        public static SettingsFragment newInstance ()
        {
            var fragment = new SettingsFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SettingsFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            accountAdapter = new AccountAdapter (AccountAdapter.DisplayMode.SettingsListview, false, true);
            accountAdapter.AddAccount += AccountAdapter_AddAccount;
            accountAdapter.ConnectToSalesforce += AccountAdapter_ConnectToSalesforce;
            accountAdapter.AccountSelected += AccountAdapter_AccountSelected;

            recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (accountAdapter);

            layoutManager = new LinearLayoutManager (this.Activity);
            recyclerView.SetLayoutManager (layoutManager);

            var hotSwitch = view.FindViewById<Switch> (Resource.Id.show_hot_cards);
            hotSwitch.Checked = LoginHelpers.ShowHotCards ();
            hotSwitch.CheckedChange += HotSwitch_CheckedChange;

            var unreadSpinner = view.FindViewById<Spinner> (Resource.Id.unread_spinner);
            var unreadSpinnerAdapter = ArrayAdapter.CreateFromResource (this.Activity, Resource.Array.unread_count, Resource.Layout.spinner_item);
            unreadSpinnerAdapter.SetDropDownViewResource (Android.Resource.Layout.SimpleSpinnerDropDownItem);
            unreadSpinner.Adapter = unreadSpinnerAdapter;

            // Map to string array
            switch (EmailHelper.HowToDisplayUnreadCount ()) {
            case EmailHelper.ShowUnreadEnum.AllMessages:
                unreadSpinner.SetSelection (0);
                break;
            case EmailHelper.ShowUnreadEnum.RecentMessages:
                unreadSpinner.SetSelection(1);
                break;
            case EmailHelper.ShowUnreadEnum.TodaysMessages:
                unreadSpinner.SetSelection(2);
                break;
            }

            unreadSpinner.ItemSelected += UnreadSpinner_ItemSelected;

//            if (BuildInfoHelper.IsDev || BuildInfoHelper.IsAlpha) {
//                var crashButton = view.FindViewById<Button> (Resource.Id.crash_button);
//                crashButton.Visibility = ViewStates.Visible;
//                crashButton.Click += CrashButton_Click;
//            }

            return view;
        }

        void UnreadSpinner_ItemSelected (object sender, AdapterView.ItemSelectedEventArgs e)
        {
            switch (e.Position) {
            case 0:
                EmailHelper.SetHowToDisplayUnreadCount (EmailHelper.ShowUnreadEnum.AllMessages);
                break;
            case 1:
                EmailHelper.SetHowToDisplayUnreadCount (EmailHelper.ShowUnreadEnum.RecentMessages);
                break;
            case 2:
                EmailHelper.SetHowToDisplayUnreadCount (EmailHelper.ShowUnreadEnum.TodaysMessages);
                break;
            }
        }

        void HotSwitch_CheckedChange (object sender, CompoundButton.CheckedChangeEventArgs e)
        {
            LoginHelpers.SetShowHotCards (e.IsChecked); 
        }

        void CrashButton_Click (object sender, EventArgs e)
        {
            throw new Exception ("CRASH SIMULATION");  
        }

        public override void OnResume ()
        {
            base.OnResume ();

            accountAdapter.Refresh ();

            // Highlight the tab bar icon of this activity
            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);

            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more_active);
            }

            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }

        void AccountAdapter_AccountSelected (object sender, McAccount account)
        {
            var parent = (SettingsActivity)Activity;
            parent.AccountSettingsSelected (account);
        }

        void AccountAdapter_AddAccount (object sender, EventArgs e)
        {
            var parent = (AccountListDelegate)Activity;
            parent.AddAccount ();
        }

        void AccountAdapter_ConnectToSalesforce (object sender, EventArgs e)
        {
            var parent = (SettingsActivity)Activity;
            parent.ConnectToSalesforce ();
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_AccountSetChanged:
                accountAdapter.Refresh ();
                break;
            case NcResult.SubKindEnum.Info_AccountChanged:
                var activity = (NcTabBarActivity)this.Activity;
                activity.SetSwitchAccountButtonImage (View);
                break;
            }
        }

        */

}

