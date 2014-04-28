//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore;
using NachoCore.Model;
using NachoClient;

namespace NachoCore
{
    public class NcEmailManager
    {
        public NcEmailManager ()
        {
        }

        protected static McFolder InboxFolder ()
        {
            var emailFolders = new NachoFolders (NachoFolders.FilterForEmail);
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
                return new MissingFolder ();
            } else {
                return new NachoEmailMessages (inboxFolder);
            }
        }

        public static INachoEmailMessages PriorityInbox ()
        {
            var inboxFolder = InboxFolder ();
            if (null == inboxFolder) {
                return new MissingFolder ();
            } else {
                return new NachoPriorityEmailMessages (inboxFolder);
            }
        }

        protected class MissingFolder : INachoEmailMessages
        {
            public int Count ()
            {
                return 0;
            }

            public void Refresh ()
            {
            }

            public McEmailMessageThread GetEmailThread (int i)
            {
                NachoAssert.CaseError ();
                return null;
            }
        }
    }
}

