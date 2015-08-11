//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System.Collections.Generic;
using MailKit;
using NachoCore.Model;
using System;
using MailKit.Search;
using MimeKit;
using NachoCore.Utils;
using System.Linq;

namespace NachoCore.IMAP
{
    public class SyncKit
    {

        // TODO Strategy Optimizations:
        // 1) Instead of passing a simple list (or set) of uniqueId's, send a set, where each element is UID+Flags+headers.
        //    Reason: We only need the full NewMessageFlags for NEW messages. When we're trying to determine Deleted or Flags-change,
        //    we only need MessageSummaryItems.Flags and MessageSummaryItems.UniqueId. This will give us a much faster
        //    server-response.
        //    Perhaps instead UID+Flags_headers, have a set of "check for new" and a set for "check for changes"?
        // 2) strategy should remember which folder it's been working on and give it some preference. Otherwise we always
        //    do a depth-first sync, always checking the front of the list of folders first, making the tail-end of the list
        //    very slow to update.

        public enum MethodEnum
        {
            Sync,
            OpenOnly,
            QuickSync,
        };

        public MethodEnum Method;
        public McFolder Folder;
        public MessageSummaryItems Flags;
        // PendingSingle is null if Strategy decided to Sync.
        public McPending PendingSingle;
        public IList<UniqueId> SyncSet;
        public HashSet<HeaderId> Headers;
        public bool GetPreviews;
        public bool GetHeaders;
        public uint QSpan; // Quick Sync Span

        public SyncKit (McFolder folder)
        {
            Method = MethodEnum.OpenOnly;
            Folder = folder;
        }

        public SyncKit (McFolder folder, uint span, McPending pending, MessageSummaryItems flags, HashSet<HeaderId> headers)
        {
            Method = MethodEnum.QuickSync;
            Folder = folder;
            QSpan = span;
            Flags = flags;
            Headers = headers;
            GetPreviews = false;
            GetHeaders = true;
            PendingSingle = pending;
        }

        public SyncKit (McFolder folder, UniqueIdSet uidset, MessageSummaryItems flags, HashSet<HeaderId> headers)
        {
            Method = MethodEnum.Sync;
            Folder = folder;
            SyncSet = uidset;
            Flags = flags;
            Headers = headers;
            GetPreviews = false;
            GetHeaders = true;
        }

        public static UniqueIdSet MustUniqueIdSet (IList<UniqueId> uids)
        {
            if (uids is UniqueIdSet) {
                return uids as UniqueIdSet;
            } else {
                return new UniqueIdSet (uids);
            }
        }

        public override string ToString ()
        {
            string me = string.Format ("SyncKit {0} (Type {{{1}}}", Folder.ImapFolderNameRedacted (), Method.ToString ());
            switch (Method) {
            case MethodEnum.Sync:
                me += string.Format (" Flags {{{0}}}", Flags);
                me += string.Format (" SyncSet {{{0}}}", SyncSet.ToString ());
                break;

            default:
                break;
            }
            if (null != PendingSingle) {
                me += " UserRequested";
            }
            me += ")";
            return me;
        }
    }

    public interface IImapStrategy
    {
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending);

        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder);
    }
}
