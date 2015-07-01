//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System.Collections.Generic;
using MailKit;
using NachoCore.Model;
using System;
using MailKit.Search;
using MimeKit;

namespace NachoCore.IMAP
{
    public class SyncKit
    {
        public enum MethodEnum
        {
            Sync,
            OpenOnly,
        };

        public MethodEnum Method;
        public McFolder Folder;
        public MessageSummaryItems Flags;
        // PendingSingle is null if Strategy decided to Sync.
        public McPending PendingSingle;
        public UniqueIdSet SyncSet;
        public HashSet<HeaderId> Headers;

        public SyncKit (McFolder folder)
        {
            Method = MethodEnum.OpenOnly;
            Folder = folder;
        }


        public SyncKit (McFolder folder, UniqueIdSet uidset, MessageSummaryItems flags, HashSet<HeaderId> headers)
        {
            Method = MethodEnum.Sync;
            Folder = folder;
            SyncSet = uidset;
            Flags = flags;
            Headers = headers;
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
