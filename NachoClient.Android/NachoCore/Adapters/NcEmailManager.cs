//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Utils;
using NachoCore.Model;
using NachoClient;

namespace NachoCore
{
    public class NcEmailManager
    {
        public NcEmailManager ()
        {
        }

        public static McFolder InboxFolder (int accountId)
        {
            var inboxFolder = McFolder.GetDefaultInboxFolder (accountId);
            return inboxFolder;
        }

        public static INachoEmailMessages Inbox (int accountId)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                return new NachoUnifiedInbox ();
            }
            var inboxFolder = InboxFolder (accountId);
            if (null == inboxFolder) {
                return new MissingFolder ("Inbox");
            } else {
                return new NachoEmailMessages (inboxFolder);
            }
        }

        public static INachoEmailMessages PriorityInbox (int accountId)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                return new NachoUnifiedHotList ();
            }
            var inboxFolder = InboxFolder (accountId);
            if (null == inboxFolder) {
                return new MissingFolder ("Hot List");
            } else {
                return new NachoPriorityEmailMessages (inboxFolder);
            }
        }

        public static INachoEmailMessages LikelyToReadInbox (int accountId)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                return new NachoUnifiedLikelyToRead ();
            }
            var inboxFolder = InboxFolder (accountId);
            if (null == inboxFolder) {
                return new MissingFolder ("Focused");
            } else {
                return new NachoLikelyToReadEmailMessages (inboxFolder);
            }
        }

        protected class MissingFolder : INachoEmailMessages
        {
            protected string displayName;

            public MissingFolder (string displayName) : base ()
            {
                this.displayName = displayName;
            }

            public int Count ()
            {
                return 0;
            }

            public bool Refresh (out List<int> adds, out List<int> deletes)
            {
                adds = null;
                deletes = null;
                return false;
            }

            public McEmailMessageThread GetEmailThread (int i)
            {
                NcAssert.CaseError ();
                return null;
            }

            public List<McEmailMessageThread> GetEmailThreadMessages (int id)
            {
                NcAssert.CaseError ();
                return null;
            }

            public string DisplayName ()
            {
                return displayName;
            }

            public bool HasOutboxSemantics ()
            {
                return false;
            }

            public bool HasDraftsSemantics ()
            {
                return false;
            }

            public NcResult StartSync ()
            {
                return NachoSyncResult.DoesNotSync ();
            }

            public INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
            {
                return null;
            }

            public bool IsCompatibleWithAccount (McAccount account)
            {
                return false;
            }

        }
    }
}

