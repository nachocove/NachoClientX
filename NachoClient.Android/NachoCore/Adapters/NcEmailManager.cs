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

        protected class MissingFolder : NachoEmailMessagesBase, INachoEmailMessages
        {
            protected string displayName;

            public MissingFolder (string displayName) : base ()
            {
                this.displayName = displayName;
            }

            public override string DisplayName ()
            {
                return displayName;
            }
        }
    }
}
