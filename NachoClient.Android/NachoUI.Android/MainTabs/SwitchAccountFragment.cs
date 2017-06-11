
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

}

