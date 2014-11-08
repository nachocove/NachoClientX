//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class UserInteractionEmailMessages : INachoEmailMessages
    {
        List<McEmailMessageThread> threadList;
        McFolder folder;
        McContact contact;

        public UserInteractionEmailMessages (McContact contact)
        {
            this.folder = new McFolder ();
            this.contact = contact;
            Refresh ();
        }

        // FIXME: Be smarter about getting Inbox
        protected static McFolder InboxFolder (int accountId)
        {
            NcAssert.True (0 != accountId);
            var emailFolders = new NachoFolders (NachoFolders.FilterForEmail);
            for (int i = 0; i < emailFolders.Count (); i++) {
                McFolder f = emailFolders.GetFolder (i);
                if (f.DisplayName.Equals ("Inbox")) {
                    return f;
                }
            }
            return null;
        }

        public bool Refresh ()
        {
            if (null == contact) {
                threadList = new List<McEmailMessageThread> ();
                return true;
            }
            var folder = InboxFolder (contact.AccountId);
            if (null == folder) {
                threadList = new List<McEmailMessageThread> ();
                return true;
            }
            var list = McEmailMessage.QueryInteractions (contact.AccountId, contact);
            if (!NcMessageThreads.AreDifferent (threadList, list)) {
                return false;
            }
            threadList = NcMessageThreads.ThreadByConversation (list);
            return true;
        }

        public int Count ()
        {
            return threadList.Count;
        }

        public McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            return t;
        }

        public string DisplayName ()
        {
            return folder.DisplayName;
        }

    }
}
