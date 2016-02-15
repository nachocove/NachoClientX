//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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
    public class NachoDeadlineEmailMessages : NachoEmailMessagesBase, INachoEmailMessages
    {
        int accountId;
        List<McEmailMessageThread> threadList;

        public NachoDeadlineEmailMessages (int accountId)
        {
            this.accountId = accountId;

            List<int> adds;
            List <int> deletes;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDueDateMessageItems (accountId);
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
                var thread = McEmailMessage.QueryDueDateMessageItemsByThreadId (accountId, message.ConversationId);
                return thread;
            }
        }

        public override string DisplayName ()
        {
            return "Deadlines";
        }

        public override INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoDeadlineEmailThread (accountId, thread.GetThreadId());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == accountId;
        }

    }

    public class NachoDeadlineEmailThread : NachoEmailMessagesBase, INachoEmailMessages
    {
        int accountId;
        string threadId;
        List<McEmailMessageThread> threadList;

        public NachoDeadlineEmailThread (int accountId, string threadId)
        {
            this.accountId = accountId;

            List<int> adds;
            List <int> deletes;
            this.threadId = threadId;
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDueDateMessageItemsByThreadId (accountId, threadId);
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
            return "Deadlines";
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == accountId;
        }
    }
}