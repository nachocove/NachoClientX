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

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
            if (null == contact) {
                threadList = new List<McEmailMessageThread> ();
                return true;
            }
            var folder = McFolder.GetDefaultInboxFolder (contact.AccountId);
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
            t.Source = this;
            return t;
        }


        // Add messages make up the thread, just the user ones
        public List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var message = McEmailMessage.QueryById<McEmailMessage> (id);
            if (null == message) {
                return new List<McEmailMessageThread> ();
            } else {
                var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, message.ConversationId);
                return thread;
            }
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
