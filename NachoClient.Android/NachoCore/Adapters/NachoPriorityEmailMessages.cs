//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore.Model;
using NachoCore.Brain;
using NachoCore.Utils;

namespace NachoCore
{
    public class NachoPriorityEmailMessages : NachoEmailMessages
    {
        List<McEmailMessageThread> threadList;
        McFolder folder;

        public NachoPriorityEmailMessages (McFolder folder)
        {
            this.folder = folder;
            List<int> adds;
            List<int> deletes;
            threadList = new List<McEmailMessageThread> ();
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
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

        // Add messages, not just hot ones
        public override List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var message = McEmailMessage.QueryById<McEmailMessage> (id);
            if (null == message) {
                return new List<McEmailMessageThread> ();
            } else {
                var thread = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, message.ConversationId);
                return thread;
            }
        }

        public override string DisplayName ()
        {
            return "Hot List";
        }

        public override NcResult StartSync ()
        {
            if (null != folder) {
                return BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public override NachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return new NachoThreadedEmailMessages (folder, thread.GetThreadId ());
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.Id == folder.AccountId;
        }
    }
}


