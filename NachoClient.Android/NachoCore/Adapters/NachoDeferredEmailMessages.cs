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
            List <int> deletes;
            Refresh (out deletes);
        }

        public bool Refresh (out List<int> deletes)
        {
            var list = McEmailMessage.QueryDeferredMessageItemsAllAccounts ();
            if (null == list) {
                list = new List<NcEmailMessageIndex> ();
            }
            if (!NcMessageThreads.AreDifferent (threadList, list, out deletes)) {
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

    }
}
