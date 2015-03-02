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
            List<int> adds;
            List<int> deletes;
            Refresh (out adds, out deletes);
        }

        public bool Refresh (out List<int> adds, out List<int> deletes)
        {
            List<NcEmailMessageIndex> hotList = new List<NcEmailMessageIndex> ();
            double threshold = McEmailMessage.minHotScore;
            // Before statistics converge, there may be a period when there is no hot emails.
            // When that happens, lower the threshold until we found something
            hotList = McEmailMessage.QueryActiveMessageItemsByScore (folder.AccountId, folder.Id, threshold);
            if (null == hotList) {
                hotList = new List<NcEmailMessageIndex> ();
            }

            List<NcEmailMessageIndex> list = new List<NcEmailMessageIndex> ();
            foreach (var hotMessage in hotList) {
                var relatedList = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, hotMessage.ThreadId);
                list.AddRange (relatedList);
            }

            var threads = NcMessageThreads.ThreadByConversation (list);
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
            return "Hot List";
        }

        public void StartSync ()
        {
            if (null != folder) {
                BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            }
        }

        public INachoEmailMessages GetAdapterForThread (string threadId)
        {
            return new NachoThreadedEmailMessages (folder, threadId);
        }
            
    }
}


