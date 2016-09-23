//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.ActiveSync;

namespace NachoCore.Utils
{
    public class NachoMailFolders
    {
        
        public static readonly Xml.FolderHierarchy.TypeCode [] EmailFolderTypes = {
            Xml.FolderHierarchy.TypeCode.UserCreatedGeneric_1,
            Xml.FolderHierarchy.TypeCode.DefaultInbox_2,
            Xml.FolderHierarchy.TypeCode.DefaultDrafts_3,
            Xml.FolderHierarchy.TypeCode.DefaultDeleted_4,
            Xml.FolderHierarchy.TypeCode.DefaultSent_5,
            Xml.FolderHierarchy.TypeCode.DefaultOutbox_6,
            Xml.FolderHierarchy.TypeCode.UserCreatedMail_12,
            Xml.FolderHierarchy.TypeCode.Unknown_18,
        };

        public McAccount Account;
        List<McAccount> ResultAccounts;
        List<List<Entry>> AccountEntries;
        List<McFolder> Recents;
        Dictionary<int, McAccount> AccountsById;

        public class Entry
        {
            public readonly McFolder Folder;
            public readonly int IndentLevel = 0;

            public Entry (McFolder folder, int indentLevel)
            {
                Folder = folder;
                IndentLevel = indentLevel;
            }
        }

        public NachoMailFolders (McAccount account)
        {
            Account = account;
            ResultAccounts = new List<McAccount> ();
            AccountEntries = new List<List<Entry>> ();
            Recents = new List<McFolder> ();
            AccountsById = new Dictionary<int, McAccount> ();
        }

        public void Reload ()
        {
            ResultAccounts.Clear ();

            if (Account.Id == McAccount.GetUnifiedAccount ().Id) {
                var foldersByAccountId = new Dictionary<int, List<McFolder>> ();
                AccountsById.Clear ();
                var folders = McFolder.QueryNonHiddenFoldersOfTypeUnified (EmailFolderTypes);
                foreach (var folder in folders) {
                    if (!AccountsById.ContainsKey (folder.AccountId)) {
                        AccountsById.Add (folder.AccountId, McAccount.QueryById<McAccount> (folder.AccountId));
                        foldersByAccountId.Add (folder.AccountId, new List<McFolder> ());
                    }
                    foldersByAccountId [folder.AccountId].Add (folder);
                }

                ResultAccounts.AddRange (AccountsById.Values);
                ResultAccounts.Sort ((x, y) => {
                    return x.Id - y.Id;
                });

                foreach (var account in ResultAccounts) {
                    CreateEntriesForAccount (account, foldersByAccountId[account.Id]);
                }
                Recents = McFolder.QueryByMostRecentlyAccessedVisibleFoldersUnified ();
            } else {
                var folders = McFolder.QueryNonHiddenFoldersOfType (Account.Id, EmailFolderTypes);
                CreateEntriesForAccount (Account, folders);
                Recents = McFolder.QueryByMostRecentlyAccessedVisibleFolders (Account.Id);
                ResultAccounts.Add (Account);
            }
        }

        public int AccountCount {
            get {
                return ResultAccounts.Count;
            }
        }

        public int RecentCount {
            get {
                return Recents.Count;
            }
        }

        public int EntryCountAtAccountIndex (int index)
        {
            return AccountEntries [index].Count;
        }

        public McFolder RecentAtIndex (int index)
        {
            return Recents [index];
        }

        public McAccount AccountAtIndex (int index)
        {
            return ResultAccounts [index];
        }

        public McAccount AccountForId (int accountId)
        {
            return AccountsById [accountId];
        }

        public Entry EntryAtIndex (int accountIndex, int entryIndex)
        {
            return AccountEntries [accountIndex][entryIndex];
        }

        void CreateEntriesForAccount (McAccount account, List<McFolder> folders)
        {
            var childrenByParentId = new Dictionary<string, List<McFolder>> ();
            childrenByParentId.Add ("0", new List<McFolder> ());
            foreach (var folder in folders) {
                if (!childrenByParentId.ContainsKey (folder.ParentId)) {
                    childrenByParentId.Add (folder.ParentId, new List<McFolder> ());
                }
                childrenByParentId [folder.ParentId].Add (folder);
            }
            childrenByParentId ["0"].Sort (CompareFolders);
            var entries = new List<Entry> ();
            AddToEntries (entries, childrenByParentId ["0"], childrenByParentId, 0);
            AccountEntries.Add (entries);
        }

        void AddToEntries (List<Entry> entries, List<McFolder> folders, Dictionary<string, List<McFolder>> childrenByParentId, int level)
        {
            foreach (var folder in folders) {
                entries.Add (new Entry (folder, level));
                List<McFolder> children;
                if (childrenByParentId.TryGetValue(folder.ServerId, out children)){
                    AddToEntries (entries, children, childrenByParentId, level + 1);
                }
            }
        }

        int CompareFolders (McFolder x, McFolder y)
        {
            if (x.Type != y.Type) {
                if (x.Type == Xml.FolderHierarchy.TypeCode.DefaultInbox_2) {
                    return -1;
                }
                if (y.Type == Xml.FolderHierarchy.TypeCode.DefaultInbox_2) {
                    return 1;
                }
                if (x.Type == Xml.FolderHierarchy.TypeCode.DefaultSent_5) {
                    return -1;
                }
                if (y.Type == Xml.FolderHierarchy.TypeCode.DefaultSent_5) {
                    return -1;
                }
                if (x.Type == Xml.FolderHierarchy.TypeCode.DefaultDrafts_3) {
                    return -1;
                }
                if (y.Type == Xml.FolderHierarchy.TypeCode.DefaultDrafts_3) {
                    return -1;
                }
                if (x.Type == Xml.FolderHierarchy.TypeCode.DefaultOutbox_6) {
                    return -1;
                }
                if (y.Type == Xml.FolderHierarchy.TypeCode.DefaultOutbox_6) {
                    return -1;
                }
                if (x.Type == Xml.FolderHierarchy.TypeCode.DefaultDeleted_4) {
                    return -1;
                }
                if (y.Type == Xml.FolderHierarchy.TypeCode.DefaultDeleted_4) {
                    return -1;
                }
            }
            if (x.IsClientOwnedDraftsFolder () && !y.IsClientOwnedDraftsFolder ()) {
                return -1;
            }
            if (y.IsClientOwnedDraftsFolder () && !x.IsClientOwnedDraftsFolder ()) {
                return 1;
            }
            if (x.IsClientOwnedOutboxFolder () && !y.IsClientOwnedOutboxFolder ()) {
                return -1;
            }
            if (y.IsClientOwnedOutboxFolder () && !x.IsClientOwnedOutboxFolder ()) {
                return 1;
            }
            var result = String.Compare (x.DisplayName, y.DisplayName, StringComparison.InvariantCultureIgnoreCase);
            if (result != 0) {
                return result;
            }
            return x.Id - y.Id;
        }

    }
}
