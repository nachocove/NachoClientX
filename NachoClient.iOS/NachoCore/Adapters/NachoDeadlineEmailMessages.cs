//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoCore
{
    public class NachoDeadlineEmailMessages : INachoEmailMessages
    {
        List<McEmailMessageThread> threadList;

        public NachoDeadlineEmailMessages ()
        {
            List<int> adds;
            List <int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDueDateMessageItemsAllAccounts ();
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            if (!NcMessageThreads.AreDifferent (threadList, list, out adds, out deletes)) {
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
            return "Deadlines";
        }

        public void StartSync ()
        {
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return new NachoDeadlineEmailThread (threadId);
        }

    }

    public class NachoDeadlineEmailThread : INachoEmailMessages
    {
        string threadId;
        List<McEmailMessageThread> threadList;

        public NachoDeadlineEmailThread (string threadId)
        {
            List<int> adds;
            List <int> deletes;
            this.threadId = threadId;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDueDateMessageItemsAllAccountsByThreadId (threadId);
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            if (!NcMessageThreads.AreDifferent (threadList, list, out adds, out deletes)) {
                return false;
            }
            threadList = NcMessageThreads.ThreadByMessage (list);
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
            return "Deadlines";
        }

        public void StartSync ()
        {
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return null;
        }

    }
}