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

        public static NachoEmailMessages Inbox (int accountId)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                return new NachoUnifiedInbox ();
            }
            var inboxFolder = InboxFolder (accountId);
            if (null == inboxFolder) {
                return new MissingFolder ("Inbox");
            } else {
                return new NachoFolderMessages (inboxFolder);
            }
        }

        public static NachoEmailMessages PriorityInbox (int accountId, bool includeActions = true)
        {
            if (McAccount.GetUnifiedAccount ().Id == accountId) {
                var hotList = new NachoUnifiedHotList ();
                hotList.IncludeActions = includeActions;
                return hotList;
            }
            var inboxFolder = InboxFolder (accountId);
            if (null == inboxFolder) {
                return new MissingFolder ("Hot List");
            } else {
                var messages = new NachoPriorityEmailMessages (inboxFolder);
                messages.IncludeActions = includeActions;
                return messages;
            }
        }

        public static NachoEmailMessages LikelyToReadInbox (int accountId)
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

        protected class MissingFolder : NachoEmailMessages
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
