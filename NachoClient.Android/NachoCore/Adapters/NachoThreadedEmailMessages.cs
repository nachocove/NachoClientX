//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Linq;
using NachoCore;
using NachoCore.Brain;
using NachoCore.Model;
using NachoCore.Utils;

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
            var threads = NcMessageThreads.ThreadByMessage (list);
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

        public List<McEmailMessageThread> GetEmailThreadMessages (int id)
        {
            var thread = new List<McEmailMessageThread> ();
            var m = new McEmailMessageThread ();
            m.FirstMessageId = id;
            m.MessageCount = 1;
            thread.Add (m);
            return thread;
        }

        public string DisplayName ()
        {
            return "Thread";
        }

        public bool HasOutboxSemantics ()
        {
            return false;
        }

        public bool HasDraftsSemantics ()
        {
            return false;
        }

        public NcResult StartSync ()
        {
            if (null != folder) {
                return  BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public INachoEmailMessages GetAdapterForThread (McEmailMessageThread thread)
        {
            return null;
        }

        public bool IsCompatibleWithAccount (McAccount account)
        {
            return account.ContainsAccount (folder.AccountId);
        }
    }
}

