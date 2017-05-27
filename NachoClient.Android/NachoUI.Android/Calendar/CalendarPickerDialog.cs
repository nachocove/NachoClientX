//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.App;
using NachoCore.Model;
using System.Collections.Generic;
using NachoCore;
using Android.Widget;
using Android.Views;
using Android.OS;
using NachoPlatform;
using NachoCore.Utils;
using Android.Support.V7.Widget;

namespace NachoClient.AndroidClient
{
    public class CalendarPickerDialog : NcDialogFragment
    {

        public McAccount SelectedAccount { get; private set; }
        public McFolder SelectedFolder { get; private set; }

        private CalendarPickerAdapter Adapter;

        public CalendarPickerDialog () : base ()
        {
            RetainInstance = true;
        }

        public override Dialog OnCreateDialog (Bundle savedInstanceState)
        {
            var accounts = McAccount.GetAllConfiguredNormalAccounts ();
            Adapter = new CalendarPickerAdapter (accounts, SelectedAccount, SelectedFolder);
            Adapter.FolderSelected += FolderSelected;

            var builder = new AlertDialog.Builder (this.Activity);
            var inflater = LayoutInflater.From (Activity);
            var view = inflater.Inflate (Resource.Layout.CalendarPickerDialog, null, false);
            var listView = view.FindViewById (Resource.Id.list_view) as RecyclerView;
            listView.SetLayoutManager (new LinearLayoutManager (Activity));
            listView.SetAdapter (Adapter);
            builder.SetView (view);
            return builder.Create ();
        }

        private void FolderSelected (object sender, CalendarPickerEventArgs e)
        {
            SelectedAccount = e.Account;
            SelectedFolder = e.Folder;
            Adapter.NotifyDataSetChanged ();
            Dismiss ();
        }

        public void Show (FragmentManager manager, string tag, McAccount selectedAccount, McFolder selectedFolder, Action dismissAction)
        {
            SelectedAccount = selectedAccount;
            SelectedFolder = selectedFolder;
            Show (manager, tag, dismissAction);
        }

        public override void OnDismiss (Android.Content.IDialogInterface dialog)
        {
            Adapter.FolderSelected -= FolderSelected;
            base.OnDismiss (dialog);
        }

        private class CalendarPickerEventArgs : EventArgs
        {

            public McAccount Account { get; private set; }
            public McFolder Folder { get; private set; }

            public CalendarPickerEventArgs (McAccount account, McFolder folder)
            {
                Account = account;
                Folder = folder;
            }
        }

        private class CalendarPickerAdapter : GroupedListRecyclerViewAdapter
        {
            private List<McAccount> Accounts;
            private Dictionary<int, NachoFolders> FoldersByAccountId;
            private McAccount SelectedAccount;
            private McFolder SelectedFolder;

            public event EventHandler<CalendarPickerEventArgs> FolderSelected;

            enum ViewType
            {
                Account,
                Folder
            }

            public CalendarPickerAdapter (List<McAccount> accounts, McAccount selectedAccount, McFolder selectedFolder)
            {
                SelectedAccount = selectedAccount;
                SelectedFolder = selectedFolder;

                FoldersByAccountId = new Dictionary<int, NachoFolders> ();
                Accounts = new List<McAccount> ();
                foreach (var account in accounts) {
                    if (account.AccountType != McAccount.AccountTypeEnum.Unified && account.AccountType != McAccount.AccountTypeEnum.Device) {
                        if (account.HasCapability (McAccount.AccountCapabilityEnum.CalWriter)) {
                            var folders = new NachoFolders (account.Id, NachoFolders.FilterForCalendars);
                            if (folders.Count () > 0) {
                                Accounts.Add (account);
                                FoldersByAccountId.Add (account.Id, folders);
                            }
                        }
                    }
                }
            }

            public override int GroupCount {
                get {
                    return Accounts.Count;
                }
            }

            public override int GroupItemCount (int groupPosition)
            {
                var account = Accounts [groupPosition];
                return FoldersByAccountId [account.Id].Count ();
            }

            public override int GetHeaderViewType (int groupPosition)
            {
                return (int)ViewType.Account;
            }

            public override int GetItemViewType (int groupPosition, int position)
            {
                return (int)ViewType.Folder;
            }

            public override RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType)
            {
                switch ((ViewType)viewType) {
                case ViewType.Account:
                    return AccountViewHolder.Create (parent);
                case ViewType.Folder:
                    return FolderViewHolder.Create (parent);
                }
                throw new NcAssert.NachoDefaultCaseFailure (String.Format ("CalendarPickerDialog.OnCreateGroupedViewHolder unknown viewType: {0}", viewType));
            }

            public override void OnBindHeaderViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
            {
                var account = Accounts [groupPosition];
                (holder as AccountViewHolder).SetAccount (account);
            }

            public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position)
            {
                var account = Accounts [groupPosition];
                var folder = FoldersByAccountId [account.Id].GetFolder (position);
                var folderHolder = (holder as FolderViewHolder);
                folderHolder.SetFolder (folder);
                folderHolder.SetSelected (SelectedFolder != null && folder.Id == SelectedFolder.Id);
            }

            public override void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
            {
                var account = Accounts [groupPosition];
                var folder = FoldersByAccountId [account.Id].GetFolder (position);
                FolderSelected (this, new CalendarPickerEventArgs (account, folder));
            }

            class AccountViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
            {

                ImageView ImageView;
                TextView NameLabel;

                public static AccountViewHolder Create (ViewGroup parent)
                {
                    var inflater = LayoutInflater.From (parent.Context);
                    var view = inflater.Inflate (Resource.Layout.CalendarPickerAccountItem, parent, false);
                    return new AccountViewHolder (view);
                }

                public AccountViewHolder (View view) : base (view)
                {
                    ImageView = view.FindViewById (Resource.Id.image) as ImageView;
                    NameLabel = view.FindViewById (Resource.Id.name) as TextView;
                }

                public void SetAccount (McAccount account)
                {
                    ImageView.SetImageDrawable (Util.GetAccountImage (ImageView.Context, account));
                    if (!String.IsNullOrEmpty (account.DisplayName)) {
                        NameLabel.Text = account.DisplayName;
                    } else {
                        NameLabel.Text = account.EmailAddr;
                    }
                }
            }

            class FolderViewHolder : GroupedListRecyclerViewAdapter.ViewHolder
            {

                public View BackgroundView { get; private set; }
                View ContentView;
                TextView NameLabel;

                public override View ClickTargetView {
                    get {
                        return ContentView;
                    }
                }

                public static FolderViewHolder Create (ViewGroup parent)
                {
                    var inflater = LayoutInflater.From (parent.Context);
                    var view = inflater.Inflate (Resource.Layout.CalendarPickerFolderItem, parent, false);
                    return new FolderViewHolder (view);
                }

                public FolderViewHolder (View view) : base (view)
                {
                    BackgroundView = view.FindViewById (Resource.Id.background);
                    ContentView = view.FindViewById (Resource.Id.content);
                    NameLabel = view.FindViewById (Resource.Id.name) as TextView;
                }

                public void SetFolder (McFolder folder)
                {
                    NameLabel.Text = folder.DisplayName;
                }

                public void SetSelected (bool isSelected)
                {
                    if (isSelected) {
                        BackgroundView.SetBackgroundColor (new Android.Graphics.Color (0x11000000));
                    } else {
                        BackgroundView.Background = null;
                    }
                }
            }
        }
    }
}

