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
    public class SettingsFragment : Fragment
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
            RecyclerView.SetLayoutManager(new LinearLayoutManager (Context));
            ItemsAdapter = new SettingsAdapter ();
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

        void ItemClicked (object sender, AdapterView.ItemClickEventArgs e)
        {
        }

        #endregion

        #region Item Adapter

        enum SettingsViewTypes
        {
        	Basic,
        	Account
        }

        class BasicItemViewHolder : RecyclerView.ViewHolder
        {

            TextView NameTextView;
            TextView DetailTextView;

            public static BasicItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.SettingsListBasicItem, parent, false);
                return new BasicItemViewHolder (view);
            }

            public BasicItemViewHolder (View view) : base (view)
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

        class AccountViewHolder : RecyclerView.ViewHolder
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

        class SettingsAdapter : RecyclerView.Adapter
        {

            int GeneralSettingsCount = 1;
            int UnreadCountPosition = 0;

            int AboutSettingsCount = 1;
            int AboutPosition = 0;

            List<McAccount> Accounts;

            public SettingsAdapter ()
            {
                Refresh ();
            }

            public void Refresh ()
            {
                Accounts = McAccount.GetAllConfiguredNormalAccounts ();
                NotifyDataSetChanged ();
            }

            public override int ItemCount {
                get {
                    return GeneralSettingsCount + AboutSettingsCount + Accounts.Count;
                }
            }

            public override int GetItemViewType (int position)
            {
                if (position >= GeneralSettingsCount && position < GeneralSettingsCount + Accounts.Count) {
                    return (int)SettingsViewTypes.Account;
                }
                return (int)SettingsViewTypes.Basic;
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
            {
                switch ((SettingsViewTypes)viewType) {
                case SettingsViewTypes.Account:
                    return AccountViewHolder.Create (parent);
                case SettingsViewTypes.Basic:
                    return BasicItemViewHolder.Create (parent);
                }
                NcAssert.CaseError ();
                return null;
            }

            public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
            {
                if (position < GeneralSettingsCount) {
                    if (position == UnreadCountPosition) {
                        (holder as BasicItemViewHolder).SetLabels ("Unread Count", ValueForUnreadCount ());
                    } else {
                        NcAssert.CaseError ();
                    }
                    return;
                }
                position -= GeneralSettingsCount;
                if (position < Accounts.Count) {
                    (holder as AccountViewHolder).SetAccount (Accounts [position]);
                    return;
                }
                position -= Accounts.Count;
                if (position == AboutPosition) {
                    (holder as BasicItemViewHolder).SetLabels ("About Nacho Mail");
                } else {
                    NcAssert.CaseError ();
                }
            }

            protected string ValueForUnreadCount ()
            {
                switch (EmailHelper.HowToDisplayUnreadCount ()) {
                case EmailHelper.ShowUnreadEnum.AllMessages:
                    return "All Messages";
                case EmailHelper.ShowUnreadEnum.RecentMessages:
                    return "Recent Messages";
                case EmailHelper.ShowUnreadEnum.TodaysMessages:
                    return "Today's Messages";
                default:
                    NcAssert.CaseError ();
                    return "";
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
}

