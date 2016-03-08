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
    public class NachoDeferredEmailMessages : NachoEmailMessagesBase, INachoEmailMessages
    {
        int accountId;
        List<McEmailMessageThread> threadList;

        public NachoDeferredEmailMessages (int accountId)
        {
            this.accountId = accountId;

            List<int> adds;
            List <int> deletes;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDeferredMessageItems (accountId);
            var threads = NcMessageThreads.ThreadByConversation (list);
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

        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var message = McEmailMessage.QueryById<McEmailMessage> (id);
            if (null == message) {
                return new List<McEmailMessageThread> ();
            } else {
                var thread = McEmailMessage.QueryDeferredMessageItemsByThreadId (accountId, message.ConversationId);
                return thread;
            }
        }

        public override string DisplayName ()
        {
            return "Deferred";
        }

        public override INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoDeferredEmailThread (accountId, thread.GetThreadId ());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == accountId;
        }
    }

    public class NachoDeferredEmailThread : NachoEmailMessagesBase, INachoEmailMessages
    {
        int accountId;
        string threadId;
        List<McEmailMessageThread> threadList;

        public NachoDeferredEmailThread (int accountId, string threadId)
        {
            this.accountId = accountId;

            List<int> adds;
            List <int> deletes;
            this.threadId = threadId;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDeferredMessageItemsByThreadId (accountId, threadId);
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
            return "Thread";
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == accountId;
        }
    }
}
