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
    public class UserInteractionEmailMessages : NachoEmailMessagesBase, INachoEmailMessages
    {
        McContact contact;
        List<McEmailMessageThread> threadList;

        public UserInteractionEmailMessages (McContact contact)
        {
            this.contact = contact;
            threadList = new List<McEmailMessageThread> ();
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            adds = null;
            deletes = null;
            if (null == contact) {
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

        public override int Count ()
        {
            return threadList.Count;
        }

        public override McEmailMessageThread GetEmailThread (int i)
        {
            var t = threadList.ElementAt (i);
            t.Source = this;
            return t;
        }

        // Add messages make up the thread, just the user ones

        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var thread = new List<McEmailMessageThread> ();
            var m = new McEmailMessageThread ();
            m.FirstMessageId = id;
            m.MessageCount = 1;
            thread.Add (m);
            return thread;
        }

        public override string DisplayName ()
        {
            return "Interactions";
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == contact.AccountId;
        }
    }
}
