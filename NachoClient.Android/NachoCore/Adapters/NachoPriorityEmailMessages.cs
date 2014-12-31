//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Brain;

namespace NachoCore
{
    public class NachoPriorityEmailMessages : INachoEmailMessages
    {
        List<McEmailMessageThread> threadList;
        McFolder folder;

        public NachoPriorityEmailMessages (McFolder folder)
        {
            this.folder = folder;
            List<int> deletes;
            Refresh (out deletes);
        }

        public bool Refresh (out List<int> deletes)
        {
            List<NcEmailMessageIndex> list = new List<NcEmailMessageIndex> ();
            double threshold = McEmailMessage.minHotScore;
            // Before statistics converge, there may be a period when there is no hot emails.
            // When that happens, lower the threshold until we found something
            list = McEmailMessage.QueryActiveMessageItemsByScore (folder.AccountId, folder.Id, threshold);
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
            return "Hot List";
        }

    }
}


