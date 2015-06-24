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
        public enum MethodEnum {
            UidSet,
            OpenOnly,
        };
        public MethodEnum Method;
        public McFolder Folder;

        // PendingSingle is null if Strategy decided to Sync.
        public McPending PendingSingle;

        // Flags to use when fetching
        public MessageSummaryItems Flags;
        // Headers to fetch when fetching
        public HashSet<HeaderId> Headers;

        // The set of UID's to fetch. These are new messages we want
        public UniqueIdSet FetchNewUidSet;

        // The set of UID's to Sync, i.e. existing messages to verify
        public UniqueIdSet SyncUidSet;

        // The search query to do to find changed messages. Without this, we only check for deleted messages.
        public SearchQuery SyncQuery;

        public override string ToString()
        {
            string me = string.Format ("Imap SyncKit({0}", Method.ToString ());
            if (MethodEnum.UidSet == Method) {
                if (null != FetchNewUidSet) {
                    me += string.Format (" fetch {{{0}}}", FetchNewUidSet.ToString ());
                }
                if (null != SyncUidSet) {
                    me += string.Format (" sync {{{0}}}", SyncUidSet.ToString ());
                }
                if (null != SyncQuery) {
                    me += string.Format (" query {0}", SyncQuery.ToString ());
                }
            }
            me += ")";
            return me;
        }

        public static string UidSetString(IList<UniqueId> uids)
        {
            return (uids is UniqueIdRange || uids is UniqueIdSet) ? uids.ToString () : UniqueIdSet.ToString (uids);
        }

        public static UniqueIdSet MustUniqueIdSet(IList<UniqueId> uids)
        {
            if (uids is UniqueIdSet) {
                return uids as UniqueIdSet;
            } else {
                if (0 == uids.Count) {
                    return new UniqueIdSet ();
                } else {
                    UniqueIdSet newUidSet;
                    string uidSetString = UidSetString (uids);
                    if (!UniqueIdSet.TryParse (uidSetString, out newUidSet)) {
                        throw new Exception (string.Format ("Could not parse uid set string {0}", uidSetString));
                    }
                    return newUidSet;
                }
            }
        }

    }

    public interface IImapStrategy
    {
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McPending pending);
        SyncKit GenSyncKit (int accountId, McProtocolState protocolState, McFolder folder);
    }
}
