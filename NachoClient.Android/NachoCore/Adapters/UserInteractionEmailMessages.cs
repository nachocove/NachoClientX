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
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        // FIXME: Be smarter about getting Inbox
        protected static McFolder InboxFolder (int accountId)
        {
            NcAssert.True (0 != accountId);
            var emailFolders = new NachoFolders (accountId, NachoFolders.FilterForEmail);
            for (int i = 0; i < emailFolders.Count (); i++) {
                McFolder f = emailFolders.GetFolder (i);
                if (f.DisplayName.Equals ("Inbox")) {
                    return f;
                }
            }
            return null;
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
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
            var threads = NcMessageThreads.ThreadByMessage (list);
            if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                threadList = threads;
                return true;
            }
            return false;
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

        public void StartSync ()
        {
            if (null != folder) {
                BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            }
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return null;
        }

    }
}
