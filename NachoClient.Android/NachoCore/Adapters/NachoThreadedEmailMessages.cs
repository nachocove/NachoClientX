//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;

namespace NachoCore
{
    public class NachoThreadedEmailMessages : INachoEmailMessages
    {
        string threadId;
        McFolder folder;

        List<McEmailMessageThread> threadList;

        public NachoThreadedEmailMessages (McFolder folder, string threadId)
        {
            this.folder = folder;
            this.threadId = threadId;
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, threadId);
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
            return "Thread";
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

