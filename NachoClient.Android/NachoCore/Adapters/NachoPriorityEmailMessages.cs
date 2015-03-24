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
            double threshold = McEmailMessage.minHotScore;
            // Before statistics converge, there may be a period when there is no hot emails.
            // When that happens, lower the threshold until we found something
            var list = McEmailMessage.QueryActiveMessageItemsByScore (folder.AccountId, folder.Id, threshold);
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
            t.Source = this;
            return t;
        }

        // Add messages, not just hot ones
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


