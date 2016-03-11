//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoCore.Utils
{
    public class FolderLists
    {
        public class Node
        {
            public string id;
            public McFolder folder;
            public McAccount account;
            public List<Node> children;
            public bool opened;
            public int order;

            public Node ()
            {
                this.children = new List<Node> ();
            }

            public Node (McAccount account, bool opened) : this ()
            {
                this.account = account;
                this.opened = opened;
            }

            public Node Copy (McFolder folder)
            {
                this.id = folder.ServerId;
                this.folder = folder;
                this.opened = false;
                return this;
            }

            public int UniqueId {
                get {
                    if (null != folder) {
                        return folder.Id;
                    }
                    if (null != account) {
                        return -100 - account.Id;
                    }
                    NcAssert.CaseError ();
                    return 0;
                }
            }
        }

        public enum Header
        {
            None,
            Common,
            Recents,
            Accounts,
            Default,
            Folders,
        };

        public class DisplayElement
        {
            public Node node;
            public int level;
            public Header header;
            public bool lastInSection;
            public bool allowDuplicate;

            public DisplayElement (Node node, int level, bool allowDuplicate)
            {
                this.node = node;
                this.level = level;
                this.header = Header.None;
                this.lastInSection = false;
                this.allowDuplicate = allowDuplicate;
            }

            public DisplayElement (Header header)
            {
                this.header = header;
            }

        }

        public List<DisplayElement> displayList;

        HashSet<int> openList;

        public FolderLists (int accountId, bool hideFakeFolders)
        {
            openList = new HashSet<int> ();
            Create (accountId, hideFakeFolders);
        }

        public void Create (int accountId, bool hideFakeFolders)
        {
            displayList = new List<DisplayElement> ();

            var displayAccountId = GetDisplayAccountId (accountId);
            if (0 == displayAccountId) {
                Log.Error (Log.LOG_EMAIL, "FolderList:  display account is 0 for {0}", accountId);
                return;
            }

            displayList.Add (new DisplayElement (Header.Common));

            // Well-known folders
            if (!hideFakeFolders) {
                if (McAccount.GetUnifiedAccount ().Id == accountId) {
                    displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetInboxFakeFolder ()), 0, true));
                } else {
                    var inbox = McFolder.GetDefaultInboxFolder (displayAccountId);
                    if (null != inbox) {
                        displayList.Add (new DisplayElement (new Node ().Copy (inbox), 0, true));
                    }
                }
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetDeferredFakeFolder ()), 0, true));
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetDeadlineFakeFolder ()), 0, true));
            }
            MarkLastInSection ();

            // Add accounts in unified mode
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                displayList.Add (new DisplayElement (Header.Accounts));
                foreach (var account in McAccount.GetAllConfiguredNormalAccounts()) {
                    displayList.Add (new DisplayElement (new Node (account, account.Id == displayAccountId), 0, false));
                }
                MarkLastInSection ();
            }

            var recents = McFolder.QueryByMostRecentlyAccessedVisibleFolders (displayAccountId);
            if (0 < recents.Count) {
                displayList.Add (new DisplayElement (Header.Recents));
                for (int i = 0; i < 3; i++) {
                    if (i < recents.Count) {
                        displayList.Add (new DisplayElement (new Node ().Copy (recents [i]), 0, true));
                    }
                }
                MarkLastInSection ();
            }

            AddAccount (displayAccountId);
        }

        void AddAccount (int accountId)
        {
            List<Node> rootList;
            List<Node> defaultList;
            CreateAccountList (accountId, out defaultList, out rootList);

            if (0 == (defaultList.Count + rootList.Count)) {
                return;
            }

            displayList.Add (new DisplayElement (Header.Default));
            foreach (var node in defaultList) {
                AddToDisplayList (node, 0);
            }
            MarkLastInSection ();

            displayList.Add (new DisplayElement (Header.Folders));
            foreach (var node in rootList) {
                AddToDisplayList (node, 0);
            }
            MarkLastInSection ();
        }

        void CreateAccountList (int accountId, out List<Node> defaultList, out List<Node> rootList)
        {
            rootList = new List<Node> ();
            defaultList = new List<Node> ();

            var nodeDictionary = new Dictionary<string, Node> ();

            // sort list of folders
            var folders = new NachoFolders (accountId, NachoFolders.FilterForEmail);

            for (int i = 0; i < folders.Count (); i++) {
                var folder = folders.GetFolder (i);
                var me = AddNode (nodeDictionary, folder);
                if (McFolder.AsRootServerId == folder.ParentId) {
                    rootList.Add (me);
                } else {
                    var parent = FindParent (nodeDictionary, folder);
                    parent.children.Add (me);
                }
                if (openList.Contains (folder.Id)) {
                    me.opened = true;
                }
                if (ShowInDefaults (folder)) {
                    defaultList.Add (me);
                }
            }

            // Defaults, like inbox
            SortDefaultList (defaultList);
        }

        void MarkLastInSection ()
        {
            if (0 < displayList.Count) {
                displayList [displayList.Count - 1].lastInSection = true;
            }
        }

        // Fill in placeholders too
        Node AddNode (Dictionary<string, Node> nodeDictionary, McFolder folder)
        {
            Node node;
            if (!nodeDictionary.TryGetValue (folder.ServerId, out node)) {
                node = new Node ();
                nodeDictionary.Add (folder.ServerId, node);
            }
            node.Copy (folder);
            return node;
        }

        // Add placeholder if not found
        Node FindParent (Dictionary<string, Node> nodeDictionary, McFolder folder)
        {
            Node parent;
            if (!nodeDictionary.TryGetValue (folder.ParentId, out parent)) {
                parent = new Node ();
                nodeDictionary.Add (folder.ParentId, parent);
            }
            return parent;
        }

        void AddToDisplayList (Node node, int level)
        {
            int n = 0;
            foreach (var dn in displayList) {
                if (!dn.allowDuplicate && (null != dn.node)) {
                    if (node == dn.node) {
                        return; // only dups in recents
                    }
                }
                n += 1;
            }
            var d = new DisplayElement (node, level, false);
            displayList.Add (d);
            if (node.opened) {
                foreach (var child in node.children) {
                    AddToDisplayList (child, level + 1);
                }
            }
        }

        private bool ShowInDefaults (McFolder folder)
        {
            switch (folder.Type) {
            case Xml.FolderHierarchy.TypeCode.DefaultInbox_2: 
                return true;
            case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                return false; // This is not our on-device drafts folder
            case Xml.FolderHierarchy.TypeCode.DefaultDeleted_4:
                return true;
            case Xml.FolderHierarchy.TypeCode.DefaultSent_5:
                return true;
            case Xml.FolderHierarchy.TypeCode.DefaultOutbox_6:
                return false; // This is not our on-device outbox folder
            }
            return (folder.IsClientOwnedDraftsFolder () || folder.IsClientOwnedOutboxFolder ());
        }

        public void SortDefaultList (List<Node> defaultList)
        {
            foreach (var n in defaultList) {
                var folder = n.folder;
                switch (folder.Type) {
                case Xml.FolderHierarchy.TypeCode.DefaultInbox_2: 
                    n.order = 1;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                    n.order = 2;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDeleted_4:
                    n.order = 3;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultSent_5:
                    n.order = 4;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultOutbox_6:
                    n.order = 5;
                    break;
                default:
                    n.order = 6;
                    break;
                }
            }
            defaultList.Sort (new DefaultNodeComparer ());
        }

        class DefaultNodeComparer : IComparer<Node>
        {
            public DefaultNodeComparer ()
            {
            }

            public int Compare (Node x, Node y)
            {
                if (x.order == y.order) {
                    return String.Compare (x.folder.DisplayName, y.folder.DisplayName, true);
                } else {
                    return x.order - y.order;
                }
            }
        }

        public bool IsOpen (Node node)
        {
            return openList.Contains (node.UniqueId);
        }

        public void Toggle (int position)
        {
            var uniqueId = displayList [position].node.UniqueId;
            if (openList.Contains (uniqueId)) {
                Close (position);
            } else {
                Open (position);
            }
        }

        public void ToggleById (int uniqueId)
        {
            int n = 0;
            foreach (var d in displayList) {
                if (!d.allowDuplicate && (null != d.node)) {
                    if (uniqueId == d.node.UniqueId) {
                        Toggle (n);
                        return;
                    }
                }
                n += 1;
            }
        }

        void Open (int position)
        {
            var item = displayList [position];

            if (0 == item.node.children.Count) {
                return;
            }

            openList.Add (item.node.UniqueId);

            if (null != item.node.account) {
                OpenAccount (position);
                return;
            }

            var children = new List<DisplayElement> ();
            foreach (var child in item.node.children) {
                children.Add (new DisplayElement (child, item.level + 1, false));
            }

            displayList.InsertRange (position + 1, children);
        }

        void OpenAccount (int position)
        {
            var list = new List<DisplayElement> ();

            var item = displayList [position];
            var rootNodeList = item.node.children [0].children;
            var defaultNodeList = item.node.children [1].children;

            foreach (var node in defaultNodeList) {
                list.Add (new DisplayElement (node, 1, false));
            }
            if (0 < list.Count) {
                list [0].header = Header.Default;
                list [list.Count - 1].lastInSection = true;
            }

            var rootListStart = list.Count;
            foreach (var node in rootNodeList) {
                list.Add (new DisplayElement (node, 1, false));
            }
            if (rootListStart < list.Count) {
                list [rootListStart].header = Header.Folders;
                list [list.Count - 1].lastInSection = true;
            }

            displayList.InsertRange (position + 1, list);
        }

        void Close (int position)
        {
            var item = displayList [position];
            var level = item.level;

            openList.Remove (item.node.UniqueId);

            int range = 0;
            int i = position + 1;
            while ((i < displayList.Count) && (level < displayList [i].level)) {
                openList.Remove (displayList [i].node.folder.Id);
                range += 1;
                i += 1;
            }

            if (0 < range) {
                displayList.RemoveRange (position + 1, range);
            }
        }

        public static void SetDefaultAccount (int accountId)
        {
            McMutables.SetInt (McAccount.GetDeviceAccount ().Id, "FolderList", "DefaultAccount", accountId);
        }

        public static int GetDefaultAccount ()
        {
            return McMutables.GetInt (McAccount.GetDeviceAccount ().Id, "FolderList", "DefaultAccount", 0);
        }

        int GetDisplayAccountId (int accountId)
        {
            // Use the current account if not unified
            if (McAccount.GetUnifiedAccount ().Id != accountId) {
                return accountId;
            }
            // Try the account last selected by the user
            var displayAccountId = FolderLists.GetDefaultAccount ();
            if (0 != displayAccountId) {
                return displayAccountId;
            }
            // Use the default email account, if there is one
            var displayAccount = McAccount.GetDefaultAccount (McAccount.AccountCapabilityEnum.EmailReaderWriter);
            if (null == displayAccount) {
                return 0;
            } else {
                return displayAccount.Id;
            }
        }

    }
}

