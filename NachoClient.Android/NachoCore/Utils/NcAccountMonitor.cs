//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;

using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore
{
    public class NcAccountMonitor
    {

        public class AccountInfo
        {
            public McAccount Account;
            public int UnreadCount;
            public int RecentUnreadCount;
        }

        private static NcAccountMonitor _Instance;
        public static NcAccountMonitor Instance {
            get {
                if (_Instance == null) {
                    _Instance = new NcAccountMonitor ();
                }
                return _Instance;
            }
        }

        McAccount Account;
        bool IsReloading;
        bool NeedsReload;
        public List<AccountInfo> Accounts { get; private set; }
        public event EventHandler AccountSetChanged;
        public event EventHandler AccountSwitched;

        private NcAccountMonitor () : base ()
        {
            Accounts = new List<AccountInfo> ();
            ReloadAccounts ();
            Account = NcApplication.Instance.Account;
            NcApplication.Instance.StatusIndEvent += StatusInd;
        }

        void StatusInd (object sender, EventArgs e)
        {
            var s = e as StatusIndEventArgs;
            switch (s.Status.SubKind) {
            case NcResult.SubKindEnum.Info_AccountSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageSetChanged:
            case NcResult.SubKindEnum.Info_EmailMessageMarkedReadSucceeded:
            case NcResult.SubKindEnum.Info_ChatMessageAdded:
                ReloadAccounts ();
                break;
            case NcResult.SubKindEnum.Info_AccountChanged:
                AccountChanged ();
                break;
            }
        }

        void ReloadAccounts ()
        {
            if (IsReloading) {
                NeedsReload = true;
            } else {
                NeedsReload = false;
                NcTask.Run (LoadAccountsTask, "AccountSource_Reload");
            }
        }

        void LoadAccountsTask ()
        {
            var accounts = McAccount.GetAllConfiguredNormalAccounts ();
            var infos = new List<AccountInfo> (accounts.Count);
            var unreadCountType = EmailHelper.HowToDisplayUnreadCount ();
            DateTime unreadCutoff = DateTime.Now;
            DateTime recentCutoff = DateTime.Now;
            if (unreadCountType != EmailHelper.ShowUnreadEnum.RecentMessages) {
                unreadCutoff = EmailHelper.GetNewSincePreference (0, unreadCountType);
            }
            foreach (var account in accounts) {
                var info = new AccountInfo ();
                info.Account = account;
                var inboxFolder = NcEmailManager.InboxFolder (account.Id);
                if (unreadCountType == EmailHelper.ShowUnreadEnum.RecentMessages) {
                    unreadCutoff = recentCutoff = EmailHelper.GetNewSincePreference (account.Id);
                } else {
                    recentCutoff = EmailHelper.GetNewSincePreference (account.Id, EmailHelper.ShowUnreadEnum.RecentMessages);
                }
                if (inboxFolder != null) {
                    info.UnreadCount += McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id, unreadCutoff);
                }
                info.UnreadCount += McChat.UnreadMessageCountForAccountSince (account.Id, unreadCutoff);
                if (unreadCountType == EmailHelper.ShowUnreadEnum.RecentMessages) {
                    info.RecentUnreadCount = info.UnreadCount;
                } else {
                    if (inboxFolder != null) {
                        info.RecentUnreadCount += McEmailMessage.CountOfUnreadMessageItems (inboxFolder.AccountId, inboxFolder.Id, recentCutoff);
                    }
                    info.RecentUnreadCount += McChat.UnreadMessageCountForAccountSince (account.Id, recentCutoff);
                }
                infos.Add (info);
            }
            infos.Sort ((AccountInfo x, AccountInfo y) => {
                return x.Account.Id - y.Account.Id;
            });
            NachoPlatform.InvokeOnUIThread.Instance.Invoke (() => {
                if (NeedsReload){
                    IsReloading = false;
                    ReloadAccounts ();
                }else{
                    HandleReloadResults (infos);
                    IsReloading = false;
                }
            });
        }

        void HandleReloadResults (List<AccountInfo> accounts)
        {
            Accounts = accounts;
            if (AccountSetChanged != null) {
                AccountSetChanged.Invoke (this, null);
            }
        }

        void AccountChanged ()
        {
            if (NcApplication.Instance.Account != null) {
                ChangeAccount (NcApplication.Instance.Account);
            }
        }

        // This method exists so NcApplication.Instance.Account can be updated and the AccountSwitched
        // callback can be invoked all at once.  It's useful for the account switch control to update
        // the selected account and update views all in an animation block.
        public void ChangeAccount (McAccount account)
        {
            if (Account == null || (Account.Id != account.Id)) {
                Account = account;
                // this will cause an echo, but we'll ingore it because we've already updated Account
                NcApplication.Instance.Account = Account;
                if (AccountSwitched != null) {
                    AccountSwitched.Invoke (this, null);
                }
            }
        }

    }
}

