
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
using NachoCore;
using NachoCore.Model;
using Android.Support.V7.Widget;
using Android.Support.V4.Widget;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class FolderListFragment : Fragment
    {
        public event EventHandler<McFolder> OnFolderSelected;

        int accountId;
        FolderListAdapter folderListAdapter;

        public static FolderListFragment newInstance (int accountId)
        {
            var fragment = new FolderListFragment ();
            fragment.accountId = accountId;
            return fragment;
        }

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.FolderListFragment, container, false);

            var activity = (NcTabBarActivity)this.Activity;
            activity.HookNavigationToolbar (view);

            var SwipeRefreshLayout = view.FindViewById<SwipeRefreshLayout> (Resource.Id.swipe_refresh_layout);
            SwipeRefreshLayout.Enabled = false;

            folderListAdapter = new FolderListAdapter (accountId, hideFakeFolders: false);
            var layoutManager = new LinearLayoutManager (Activity);

            var recyclerView = view.FindViewById<RecyclerView> (Resource.Id.recyclerView);
            recyclerView.SetAdapter (folderListAdapter);
            recyclerView.SetLayoutManager (layoutManager);

            folderListAdapter.OnFolderSelected += FolderListAdapter_OnFolderSelected;
            folderListAdapter.OnAccountSelected += FolderListAdapter_OnAccountSelected;

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (null != folderListAdapter) {
                folderListAdapter.SwitchAccount (NcApplication.Instance.Account);
            }

            var moreImage = View.FindViewById<Android.Widget.ImageView> (Resource.Id.more_image);
            if (LoginHelpers.ShouldAlertUser ()) {
                moreImage.SetImageResource (Resource.Drawable.gen_avatar_alert);
            } else {
                moreImage.SetImageResource (Resource.Drawable.nav_more);
            }
        }

        void FolderListAdapter_OnFolderSelected (object sender, McFolder folder)
        {
            if (null != OnFolderSelected) {
                OnFolderSelected (this, folder);
            }
        }

        void FolderListAdapter_OnAccountSelected (object sender, McAccount account)
        {
            if (null != folderListAdapter) {
                FolderLists.SetDefaultAccount (account.Id);
                folderListAdapter.Refresh (accountId);
            }
        }

        public void SwitchAccount (McAccount account)
        {
            if (null != folderListAdapter) {
                accountId = account.Id;
                folderListAdapter.SwitchAccount (account);
            }
        }
    }

    public class FolderListAdapter : RecyclerView.Adapter
    {
        public event EventHandler<McFolder> OnFolderSelected;
        public event EventHandler<McAccount> OnAccountSelected;

        const int ROW_TYPE = 1;
        const int HEADER_ROW_TYPE = 2;
        const int ACCOUNT_ROW_TYPE = 3;

        public override int GetItemViewType (int position)
        {
            var d = folderLists.displayList [position];
            if (null == d.node) {
                return HEADER_ROW_TYPE;
            } else {
                if (null == d.node.account) {
                    return ROW_TYPE;
                } else {
                    return ACCOUNT_ROW_TYPE;
                }
            }
        }

        public class HeaderViewHolder : RecyclerView.ViewHolder
        {
            public TextView header { get; private set; }

            public View listHeader { get; private set; }

            public HeaderViewHolder (View itemView) : base (itemView)
            {
                header = itemView.FindViewById<TextView> (Resource.Id.header);
                listHeader = itemView.FindViewById<View> (Resource.Id.list_header);
            }
        }

        public class AccountViewHolder : RecyclerView.ViewHolder
        {
            public TextView name { get; private set; }

            public ImageView accountSelector { get; private set; }

            public ImageView accountImage { get; private set; }

            public View separator { get; private set; }

            public AccountViewHolder (View itemView, Action<int> listener) : base (itemView)
            {
                name = itemView.FindViewById<TextView> (Resource.Id.name);
                accountSelector = itemView.FindViewById<ImageView> (Resource.Id.account_selector);
                accountImage = itemView.FindViewById<ImageView> (Resource.Id.account_image);
                separator = itemView.FindViewById<View> (Resource.Id.separator);

                itemView.Click += (object sender, EventArgs e) => listener (base.AdapterPosition);
            }
        }

        public class FolderViewHolder : RecyclerView.ViewHolder
        {
            public TextView name { get; private set; }

            public ImageView folder { get; private set; }

            public ImageView toggle { get; private set; }

            public View separator { get; private set; }

            public FolderViewHolder (View itemView, Action<int> listener, Action<int> toggleListener) : base (itemView)
            {
                name = itemView.FindViewById<TextView> (Resource.Id.name);
                toggle = itemView.FindViewById<ImageView> (Resource.Id.toggle);
                separator = itemView.FindViewById<View> (Resource.Id.separator);
                folder = itemView.FindViewById<ImageView> (Resource.Id.folder);

                itemView.Click += (object sender, EventArgs e) => listener (base.AdapterPosition);
                toggle.Click += (object sender, EventArgs e) => toggleListener (base.AdapterPosition);
            }
        }

        bool hideFakeFolders;
        FolderLists folderLists;

        public FolderListAdapter (int accountId, bool hideFakeFolders)
        {
            HasStableIds = true;
            this.hideFakeFolders = hideFakeFolders;
            folderLists = new FolderLists (accountId, hideFakeFolders);
        }

        public void SwitchAccount (McAccount account)
        {
            folderLists = new FolderLists (account.Id, hideFakeFolders);
            NotifyDataSetChanged ();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            switch (viewType) {
            case HEADER_ROW_TYPE:
                var headerView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FolderCellHeader, parent, false);
                var headerHolder = new HeaderViewHolder (headerView);
                return headerHolder;
            case ACCOUNT_ROW_TYPE:
                var accountView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FolderCellAccount, parent, false);
                var accountHolder = new AccountViewHolder (accountView, OnClick);
                return accountHolder;
            default:
                var rowView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FolderCell, parent, false);
                var rowHolder = new FolderViewHolder (rowView, OnClick, OnToggle);
                return rowHolder;
            }
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            switch (holder.ItemViewType) {
            case HEADER_ROW_TYPE:
                BindHeader (holder, position);
                break;
            case ACCOUNT_ROW_TYPE:
                BindAccount (holder, position);
                break;
            default:
                BindRow (holder, position);
                break;
            }
        }

        void BindHeader (RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as HeaderViewHolder;
            var d = folderLists.displayList [position];

            if (0 == position) {
                vh.header.Visibility = ViewStates.Gone;
                vh.listHeader.Visibility = ViewStates.Visible;
                return;
            }

            vh.header.Visibility = ViewStates.Visible;
            vh.listHeader.Visibility = ViewStates.Gone;

            switch (d.header) {
            case FolderLists.Header.None:
                vh.header.Text = "";
                break;
            case FolderLists.Header.Accounts:
                vh.header.Text = "Accounts";
                break;
            case FolderLists.Header.Default:
                vh.header.Text = "Default Folders";
                break;
            case FolderLists.Header.Folders:
                vh.header.Text = "Your Folders";
                break;
            case FolderLists.Header.Recents:
                vh.header.Text = "Recent Folders";
                break;
            }
        }

        void BindAccount (RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as AccountViewHolder;
            var d = folderLists.displayList [position];
            var node = d.node;

            vh.name.Text = node.account.EmailAddr;
            vh.accountImage.SetImageResource (Util.GetAccountServiceImageId (node.account.AccountService));

            if (node.opened) {
                vh.accountSelector.SetImageResource (Resource.Drawable.gen_checkbox_checked);
            } else {
                vh.accountSelector.SetImageResource (Resource.Drawable.gen_checkbox);
            }

            if (d.lastInSection) {
                vh.separator.Visibility = ViewStates.Gone;
            } else {
                vh.separator.Visibility = ViewStates.Visible;
            }
        }

        void BindRow (RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as FolderViewHolder;

            var d = folderLists.displayList [position];
            var node = d.node;

            vh.name.Text = node.folder.DisplayName;

            var marginParams = (Android.Views.ViewGroup.MarginLayoutParams)vh.folder.LayoutParameters;
            marginParams.LeftMargin = dp2px (16) * d.level;
            vh.folder.LayoutParameters = marginParams;

            if (0 == node.children.Count) {
                vh.toggle.Visibility = ViewStates.Invisible;
            } else {
                vh.toggle.Visibility = ViewStates.Visible;
                if (folderLists.IsOpen (node)) {
                    vh.toggle.SetImageResource (Resource.Drawable.gen_readmore_active);
                } else {
                    vh.toggle.SetImageResource (Resource.Drawable.gen_readmore);
                }
            }

            if (d.lastInSection) {
                vh.separator.Visibility = ViewStates.Gone;
            } else {
                vh.separator.Visibility = ViewStates.Visible;
            }

        }

        private int dp2px (int dp)
        {
            return (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, (float)dp, MainApplication.Instance.Resources.DisplayMetrics);
        }

        public override int ItemCount {
            get {
                return folderLists.displayList.Count;
            }
        }

        public override long GetItemId (int position)
        {
            var displayItem = folderLists.displayList [position];

            if (displayItem.header != FolderLists.Header.None) {
                return ((int)displayItem.header) - 100;
            } else {
                return displayItem.node.UniqueId;
            }
        }

        void OnClick (int position)
        {
            var node = folderLists.displayList [position].node;

            if (null != node) {
                if (null != node.account) {
                    OnAccountSelected (this, node.account);
                } else if (null != OnFolderSelected) {
                    OnFolderSelected (this, folderLists.displayList [position].node.folder);
                }
            }
        }

        void OnToggle (int position)
        {
            folderLists.Toggle (position);
            NotifyDataSetChanged ();
        }

        public void Refresh (int accountId)
        {
            folderLists.Create (accountId, hideFakeFolders);
            NotifyDataSetChanged ();
        }

    }
}

