//  Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoCore
{
    public class NachoDeferredEmailMessages : INachoEmailMessages
    {
        List<McEmailMessageThread> threadList;

        public NachoDeferredEmailMessages ()
        {
            List<int> adds;
            List <int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDeferredMessageItemsAllAccounts ();
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
            return "Deferred";
        }

        public void StartSync ()
        {
            // TODO: Send status in as if deferreds have changed
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return new NachoDeferredEmailThread (threadId);
        }
    }

    public class NachoDeferredEmailThread : INachoEmailMessages
    {
        string threadId;
        List<McEmailMessageThread> threadList;

        public NachoDeferredEmailThread (string threadId)
        {
            List<int> adds;
            List <int> deletes;
            this.threadId = threadId;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryDeferredMessageItemsAllAccountsByThreadId (threadId);
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            if (!NcMessageThreads.AreDifferent (threadList, list, out adds, out deletes)) {
                return false;
            }
            //            threadList = NcMessageThreads.ThreadByConversation (list);
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
            return "Thread";
        }

        public void StartSync ()
        {
            // TODO: Send status in as if deferreds have changed
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return null;
        }
    }
}
