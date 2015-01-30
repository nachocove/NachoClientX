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

        public static McFolder InboxFolder ()
        {
            var account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            var emailFolders = new NachoFolders (account.Id, NachoFolders.FilterForEmail);
            for (int i = 0; i < emailFolders.Count (); i++) {
                McFolder f = emailFolders.GetFolder (i);
                if (f.DisplayName.Equals ("Inbox")) {
                    return f;
                }
            }
            return null;
        }

        public static INachoEmailMessages Inbox ()
        {
            var inboxFolder = InboxFolder ();
            if (null == inboxFolder) {
                return new MissingFolder ("Inbox");
            } else {
                return new NachoEmailMessages (inboxFolder);
            }
        }

        public static INachoEmailMessages PriorityInbox ()
        {
            var inboxFolder = InboxFolder ();
            if (null == inboxFolder) {
                return new MissingFolder ("Hot List");
            } else {
                return new NachoPriorityEmailMessages (inboxFolder);
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

            public string DisplayName ()
            {
                return displayName;
            }

            public void StartSync ()
            {
                return;
            }

        }
    }
}

