
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

        public void SwitchAccount (McAccount account)
        {
            if (null != folderListAdapter) {
                folderListAdapter.SwitchAccount (account);
            }
        }
    }

    public class FolderListAdapter : RecyclerView.Adapter
    {
        public event EventHandler<McFolder> OnFolderSelected;

        static int ROW_TYPE = 1;
        static int LAST_ROW_TYPE = 2;
        static int HEADER_ROW_TYPE = 3;

        public override int GetItemViewType (int position)
        {
            var d = folderLists.displayList [position];
            if (null == d.node) {
                return HEADER_ROW_TYPE;
            } else {
                return d.lastInSection ? LAST_ROW_TYPE : ROW_TYPE;
            }
        }


        public class FolderViewHolder : RecyclerView.ViewHolder
        {
            public TextView name { get; private set; }

            public TextView header { get; private set; }

            public ImageView folder { get; private set; }

            public ImageView toggle { get; private set; }

            public View separator { get; private set; }

            public View listHeader { get; private set; }

            public View body { get; private set; }

            public ImageView icon { get; private set; }

            public FolderViewHolder (View itemView, Action<int> listener, Action<int> headerListener, Action<int> toggleListener) : base (itemView)
            {
                name = itemView.FindViewById<TextView> (Resource.Id.name);
                header = itemView.FindViewById<TextView> (Resource.Id.header);
                folder = itemView.FindViewById<ImageView> (Resource.Id.folder);
                toggle = itemView.FindViewById<ImageView> (Resource.Id.toggle);
                separator = itemView.FindViewById<View> (Resource.Id.separator);
                listHeader = itemView.FindViewById<View> (Resource.Id.list_header);
                body = itemView.FindViewById<View> (Resource.Id.body);
                icon = itemView.FindViewById<ImageView> (Resource.Id.folder);

                itemView.Click += (object sender, EventArgs e) => listener (base.Position);
                header.Click += (object sender, EventArgs e) => headerListener (base.Position);
                toggle.Click += (object sender, EventArgs e) => toggleListener (base.Position);
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
            var itemView = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FolderCell, parent, false);
            var vh = new FolderViewHolder (itemView, OnClick, OnHeader, OnToggle);
            return vh;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var vh = holder as FolderViewHolder;

            var d = folderLists.displayList [position];
            var node = d.node;

            if (0 == position) {
                vh.listHeader.Visibility = ViewStates.Visible;
            } else {
                vh.listHeader.Visibility = ViewStates.Gone;
            }

            switch (d.header) {
            case FolderLists.Header.Accounts:
                vh.header.Visibility = ViewStates.Visible;
                vh.body.Visibility = ViewStates.Gone;
                vh.header.Text = "Accounts";
                break;
            case FolderLists.Header.Default:
                vh.header.Visibility = ViewStates.Visible;
                vh.body.Visibility = ViewStates.Gone;
                vh.header.Text = "Default Folders";
                break;
            case FolderLists.Header.Folders:
                vh.header.Visibility = ViewStates.Visible;
                vh.body.Visibility = ViewStates.Gone;
                vh.header.Text = "Your Folders";
                break;
            case FolderLists.Header.Recents:
                vh.header.Visibility = ViewStates.Visible;
                vh.body.Visibility = ViewStates.Gone;
                vh.header.Text = "Recent Folders";
                break;
            case FolderLists.Header.None:
                vh.header.Visibility = ViewStates.Gone;
                vh.body.Visibility = ViewStates.Visible;
                if (null != node.folder) {
                    vh.name.Text = node.folder.DisplayName;
                    vh.folder.SetImageResource (Resource.Drawable.folder_folder);
                } else if (null != node.account) {
                    vh.name.Text = node.account.EmailAddr;
                    vh.folder.SetImageResource (Util.GetAccountServiceImageId (node.account.AccountService));
                }
                break;
            }

            var marginParams = (Android.Views.ViewGroup.MarginLayoutParams)vh.folder.LayoutParameters;
            marginParams.LeftMargin = dp2px (16) * d.level;
            vh.folder.LayoutParameters = marginParams;

            if (null != node) {
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
            }

            if (vh.ItemViewType == LAST_ROW_TYPE) {
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
                    OnToggle (position);
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

        void OnHeader (int position)
        {
            // ignore clicks on the header
        }
    }

}

