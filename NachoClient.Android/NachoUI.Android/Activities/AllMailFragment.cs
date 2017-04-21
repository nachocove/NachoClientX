//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Support.V4.App;
using Android.Support.Design.Widget;
using Android.Support.V7.Widget;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore.Model;
using NachoCore.Utils;
using NachoCore;
using NachoCore.ActiveSync;

namespace NachoClient.AndroidClient
{
    public class AllMailFragment : Fragment, MainTabsActivity.Tab, FoldersAdapter.Listener
    {

        public McAccount Account {
            get {
                return Folders.Account;
            }
            set {
                Folders.Account = value;
            }
        }
        NachoMailFolders Folders;
        FoldersAdapter FoldersAdapter;

        #region Tab Interface

        public int TabMenuResource {
            get {
                return Resource.Menu.allmail;
            }
        }

        public void OnTabSelected (MainTabsActivity tabActivity)
        {
            if (Account.Id != NcApplication.Instance.Account.Id) {
                OnAccountSwitched (tabActivity);
            }
            tabActivity.ShowActionButton (Resource.Drawable.floating_action_new_mail_filled, ActionButtonClicked);
        }

        public void OnTabUnselected (MainTabsActivity tabActivity)
        {
        }

        public void OnAccountSwitched (MainTabsActivity tabActivity)
        {
            Account = NcApplication.Instance.Account;
            ReloadFolders ();
        }

        #endregion

        #region Subviews

        RecyclerView ListView;

        void FindSubviews (View view)
        {
            ListView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            ListView.SetLayoutManager (new LinearLayoutManager (view.Context));
        }

        void ClearSubviews ()
        {
            ListView = null;
        }

        #endregion

        #region Fragment Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.AllMailFragment, container, false);
            FindSubviews (view);
            Folders = new NachoMailFolders (NcApplication.Instance.Account);
            FoldersAdapter = new FoldersAdapter (this, Folders);
            ListView.SetAdapter (FoldersAdapter);
            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            ReloadFolders ();
        }

        public override void OnDestroyView ()
        {
            ClearSubviews ();
            base.OnDestroyView ();
        }

        #endregion

        #region Data Loading

        void ReloadFolders ()
        {
            FoldersAdapter.Reload ();
        }

        #endregion

        #region User Actions

        public void OnFolderSelected (McFolder folder)
        {
            ShowFolder (folder);
        }

        void ActionButtonClicked (object sender, EventArgs args)
        {
            ShowMessageCompose ();
        }

        #endregion

        #region Private Helpers 

        void ShowMessageCompose ()
        {
        	var intent = MessageComposeActivity.NewMessageIntent (Activity, NcApplication.Instance.Account.Id);
        	StartActivity (intent);
        }

        void ShowFolder (McFolder folder)
        {
            folder.UpdateSet_LastAccessed (DateTime.UtcNow);
            if (folder.IsClientOwnedDraftsFolder () || folder.IsClientOwnedOutboxFolder ()) {
				ShowDrafts (folder);
            } else {
				ShowMessages (folder);
            }
        }

        void ShowDrafts (McFolder folder)
        {
            var intent = MessageListActivity.BuildDraftsIntent (Activity, folder);
			StartActivity (intent);
        }

        void ShowMessages (McFolder folder)
        {
            var intent = MessageListActivity.BuildFolderIntent (Activity, folder);
            StartActivity (intent);
        }

        #endregion

    }

    public class FoldersAdapter : GroupedListRecyclerViewAdapter
    {

        public interface Listener
        {
            void OnFolderSelected (McFolder folder);
        }

        WeakReference<Listener> WeakListener;
        public NachoMailFolders Folders;

        int _GroupCount = 0;
        int RecentGroupPosition = -1;
        int FirstAccountGroupPosition = -1;

        public FoldersAdapter (Listener listener, NachoMailFolders folders) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
            Folders = folders;
        }

        public void Reload ()
        {
            if (Folders == null) {
                _GroupCount = 0;
                return;
            }
            Folders.Reload ();
            RecentGroupPosition = -1;
            FirstAccountGroupPosition = -1;
            var groupPosition = 0;
            if (Folders.RecentCount > 0) {
                RecentGroupPosition = groupPosition++;
            }
            FirstAccountGroupPosition = groupPosition++;
            _GroupCount = FirstAccountGroupPosition + Folders.AccountCount;
            NotifyDataSetChanged ();
        }

        public override int GroupCount {
            get {
                return _GroupCount;
            }
        }

        public override int GroupItemCount (int groupPosition)
        {
            if (groupPosition == RecentGroupPosition) {
                return Folders.RecentCount;
            } else {
                var accountIndex = groupPosition - FirstAccountGroupPosition;
                if (accountIndex < Folders.AccountCount) {
                    return Folders.EntryCountAtAccountIndex (accountIndex);
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AllMailFragment.GroupHeaderValue unexpected group position: {0}", groupPosition));
        }

        public override string GroupHeaderValue (Context context, int groupPosition)
        {
            if (groupPosition == RecentGroupPosition) {
                return context.GetString (Resource.String.folders_recent);
            }
            if (groupPosition >= FirstAccountGroupPosition) {
                if (groupPosition == 0 && Folders.AccountCount == 1) {
                    return null;
                }
                var accountIndex = groupPosition - FirstAccountGroupPosition;
                if (accountIndex < Folders.AccountCount) {
                    var account = Folders.AccountAtIndex (accountIndex);
                    if (!String.IsNullOrEmpty (account.DisplayName)) {
                        return account.DisplayName;
                    }
                    return account.EmailAddr;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AllMailFragment.GroupHeaderValue unexpected group position: {0}", groupPosition));
        }

        public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
        {
            return FolderViewHolder.Create (parent);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            if (groupPosition == RecentGroupPosition) {
                if (position < Folders.RecentCount) {
                    var folderHolder = (holder as FolderViewHolder);
                    var folder = Folders.RecentAtIndex (position);
                    McAccount account = null;
                    if (Folders.AccountCount > 1) {
                        account = Folders.AccountForId (folder.AccountId);
                    }
                    folderHolder.SetFolder (folder, account);
                    folderHolder.CanSelect = true;
                    folderHolder.IntentLevel = 0;
                    return;
                }
            } else {
                var accountIndex = groupPosition - FirstAccountGroupPosition;
                if (accountIndex < Folders.AccountCount) {
                    var folderHolder = (holder as FolderViewHolder);
                    var entry = Folders.EntryAtIndex (accountIndex, position);
                    folderHolder.SetFolder (entry.Folder);
                    folderHolder.CanSelect = !entry.Folder.ImapNoSelect;
                    folderHolder.IntentLevel = entry.IndentLevel;
                    return;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("AllMailFragment.OnBindViewHolder unexpected position: {0}.{1}", groupPosition, position));
        }

        public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
            var folderHolder = (holder as FolderViewHolder);
            McFolder folder = null;;
            if (folderHolder.CanSelect){
                if (groupPosition == RecentGroupPosition) {
                    if (position < Folders.RecentCount) {
                        folder = Folders.RecentAtIndex (position);
                    }
                } else {
                    var accountIndex = groupPosition - FirstAccountGroupPosition;
                    if (accountIndex < Folders.AccountCount) {
                        var entry = Folders.EntryAtIndex (accountIndex, position);
                        folder = entry.Folder;
                    }
                }
            }
            if (folder != null) {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnFolderSelected (folder);
                }
            }
        }

        class FolderViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
        {

            View IndentView;
            TextView NameLabel;
            TextView AccountLabel;
            ImageView IconView;

            float IndentWidth = 40.0f;

            public bool CanSelect {
                get {
                    return ItemView.Clickable;
                }
                set {
                    ItemView.Clickable = value;
                }
            }

            int _IntentLevel = 0;
            public int IntentLevel {
                get {
                    return _IntentLevel;
                }
                set {
                    _IntentLevel = value;
                    float indentDevicePixels = IndentWidth * _IntentLevel;
                    IndentView.LayoutParameters = new LinearLayout.LayoutParams ((int)(indentDevicePixels * ItemView.Context.Resources.DisplayMetrics.Density), 0);
                }
            }

            public static FolderViewHolder Create (ViewGroup parent)
            {
                var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.FolderListItem, parent, false);
                return new FolderViewHolder (view);
            }

            public FolderViewHolder (View view) : base (view)
            {
                NameLabel = view.FindViewById (Resource.Id.folder_name) as TextView;
                AccountLabel = view.FindViewById (Resource.Id.folder_account) as TextView;
                IconView = view.FindViewById (Resource.Id.folder_icon) as ImageView;
                IndentView = view.FindViewById (Resource.Id.folder_indent);
            }

            public void SetFolder (McFolder folder, McAccount account = null)
            {
                NameLabel.Text = PrettyMailboxName (folder.DisplayName);
                IconView.SetImageResource (IconResourceForFolder (folder));
                if (account == null) {
                    AccountLabel.Visibility = ViewStates.Gone;
                } else {
                    if (!String.IsNullOrEmpty (account.DisplayName)) {
                        AccountLabel.Text = account.DisplayName;
                    } else {
                        AccountLabel.Text = account.EmailAddr;
                    }
                    AccountLabel.Visibility = ViewStates.Visible;
                }
            }

            string PrettyMailboxName (string name)
            {
                if (name == "INBOX") {
                    return "Inbox";
                }
                return name;
            }

            int IconResourceForFolder (McFolder folder)
            {
                var iconResource = Resource.Drawable.folder_icon_default;
                if (folder.IsClientOwnedOutboxFolder ()) {
                    iconResource = Resource.Drawable.folder_icon_outbox;
                } else if (folder.IsClientOwnedDraftsFolder ()) {
                    iconResource = Resource.Drawable.folder_icon_drafts;
                } else if ((folder.ServerId == "[Gmail]/All Mail") || (folder.DisplayName == "Archive")) {
                    iconResource = Resource.Drawable.folder_icon_archive;
                } else {
                    switch (folder.Type) {
                        case Xml.FolderHierarchy.TypeCode.DefaultInbox_2:
                        iconResource = Resource.Drawable.folder_icon_inbox;
                        break;
                    case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                        iconResource = Resource.Drawable.folder_icon_drafts;
                        break;
                    case Xml.FolderHierarchy.TypeCode.DefaultDeleted_4:
                        iconResource = Resource.Drawable.folder_icon_trash;
                        break;
                    case Xml.FolderHierarchy.TypeCode.DefaultSent_5:
                        iconResource = Resource.Drawable.folder_icon_sent;
                        break;
                    case Xml.FolderHierarchy.TypeCode.DefaultOutbox_6:
                        iconResource = Resource.Drawable.folder_icon_outbox;
                        break;
                    default:
                        iconResource = Resource.Drawable.folder_icon_default;
                        break;
                    }
                }
                return iconResource;
            }

        }

    }
}
