//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public class FolderLists
    {
        public class Node
        {
            public string id;
            public McFolder folder;
            public List<Node> children;
            public bool opened;

            public Node ()
            {
                this.children = new List<Node> ();
            }

            public Node Copy (McFolder folder)
            {
                this.id = folder.ServerId;
                this.folder = folder;
                this.opened = false;
                return this;
            }
        }

        public enum Header
        {
            None,
            Recents,
            Default,
            Folders,
        };

        public class DisplayElement
        {
            public Node node;
            public int order;
            public int level;
            public Header header;
            public bool lastInSection;

            public DisplayElement (Node node, int level)
            {
                this.node = node;
                this.level = level;
                this.header = Header.None;
                this.lastInSection = false;
            }
        }

        public List<DisplayElement> displayList;

        Dictionary<string, Node> nodeDictionary;

        HashSet<int> openList;

        public FolderLists (int accountId, bool hideFakeFolders)
        {
            Create (accountId, hideFakeFolders);
        }

        public void Create (int accountId, bool hideFakeFolders)
        {
            // sort list of folders
            var folders = new NachoFolders (accountId, NachoFolders.FilterForEmail);

            openList = new HashSet<int> ();
            displayList = new List<DisplayElement> ();
            nodeDictionary = new Dictionary<string, Node> ();

            var rootList = new List<Node> ();
            var defaultList = new List<DisplayElement> ();
            var recentsList = new List<DisplayElement> ();

            for (int i = 0; i < folders.Count (); i++) {
                var folder = folders.GetFolder (i);
                var me = AddNode (folder);
                if (McFolder.AsRootServerId == folder.ParentId) {
                    rootList.Add (me);
                } else {
                    var parent = FindParent (folder);
                    parent.children.Add (me);
                }
                if (openList.Contains (folder.Id)) {
                    me.opened = true;
                }
                if (ShowInDefaults (folder)) {
                    defaultList.Add (new DisplayElement (me, 0));
                }
            }

            // Well-known folders
            var inbox = McFolder.GetDefaultInboxFolder (accountId);
            if (null != inbox) {
                displayList.Add (new DisplayElement (new Node ().Copy (inbox), 0));
            }
            if (!hideFakeFolders) {
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetHotFakeFolder ()), 0));
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetLtrFakeFolder ()), 0));
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetDeferredFakeFolder ()), 0));
                displayList.Add (new DisplayElement (new Node ().Copy (McFolder.GetDeadlineFakeFolder ()), 0));
            }

            MarkLastInSection ();

            // Max 3 recents
            var recents = McFolder.QueryByMostRecentlyAccessedVisibleFolders (accountId);
            foreach (var folder in recents) {
                recentsList.Add (new DisplayElement (new Node ().Copy (folder), 0));
                if (3 == recentsList.Count) {
                    break;
                }
            }
            if (0 < recentsList.Count) {
                recentsList [0].header = Header.Recents;
                displayList.AddRange (recentsList);
            }
            MarkLastInSection ();

            // Defaults, like inbox
            SortDefaultList (defaultList);
            if (0 < defaultList.Count) {
                defaultList [0].header = Header.Default;
                displayList.AddRange (defaultList);
            }
            MarkLastInSection ();

            // All of the folders
            int firstFolder = displayList.Count;
            foreach (var node in rootList) {
                AddToDisplayList (node, 0);
            }
            if (firstFolder < displayList.Count) {
                displayList [firstFolder].header = Header.Folders;
            }
            MarkLastInSection ();
        }

        void MarkLastInSection()
        {
            if (0 < displayList.Count) {
                displayList [displayList.Count - 1].lastInSection = true;
            }
        }

        // Fill in placeholders too
        Node AddNode (McFolder folder)
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
        Node FindParent (McFolder folder)
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
            foreach (var dn in displayList) {
                if (node == dn.node) {
                    return; // no dups
                }
            }
            var d = new DisplayElement (node, level);
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

        public void SortDefaultList (List<DisplayElement> defaultList)
        {
            foreach (var d in defaultList) {
                var folder = d.node.folder;
                switch (folder.Type) {
                case Xml.FolderHierarchy.TypeCode.DefaultInbox_2: 
                    d.order = 1;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDrafts_3:
                    d.order = 2;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultDeleted_4:
                    d.order = 3;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultSent_5:
                    d.order = 4;
                    break;
                case Xml.FolderHierarchy.TypeCode.DefaultOutbox_6:
                    d.order = 5;
                    break;
                default:
                    d.order = 6;
                    break;
                }
            }
            defaultList.Sort (new DefaultNodeComparer ());
        }

        class DefaultNodeComparer : IComparer<DisplayElement>
        {
            public DefaultNodeComparer ()
            {
            }

            public int Compare (DisplayElement x, DisplayElement y)
            {
                if (x.order == y.order) {
                    return String.Compare (x.node.folder.DisplayName, y.node.folder.DisplayName, true);
                } else {
                    return x.order - y.order;
                }
            }
        }

        public bool IsOpen (Node node)
        {
            return openList.Contains (node.folder.Id);
        }

        public void Toggle (int position)
        {
            var folder = displayList [position].node.folder;
            if (openList.Contains (folder.Id)) {
                Close (position);
            } else {
                Open (position);
            }
        }

        void Open (int position)
        {
            var item = displayList [position];

            if (0 == item.node.children.Count) {
                return;
            }

            openList.Add (item.node.folder.Id);

            var children = new List<DisplayElement> ();
            foreach (var child in item.node.children) {
                children.Add (new DisplayElement (child, item.level + 1));
            }

            displayList.InsertRange (position + 1, children);
        }

        void Close (int position)
        {
            var item = displayList [position];
            var level = item.level;

            openList.Remove (item.node.folder.Id);

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

    }
}

