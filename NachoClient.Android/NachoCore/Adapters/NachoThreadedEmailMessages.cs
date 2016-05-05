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
    public class NachoThreadedEmailMessages : NachoEmailMessages
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
            threadList = new List<McEmailMessageThread> ();
            Refresh (out adds, out deletes);
        }

        public override bool Refresh (out List<int> adds, out List<int> deletes)
        {
            var list = McEmailMessage.QueryActiveMessageItemsByThreadId (folder.AccountId, folder.Id, threadId);
            var threads = NcMessageThreads.ThreadByMessage (list);
            RemoveIgnoredMessages (threads);
            if (NcMessageThreads.AreDifferent (threadList, threads, out adds, out deletes)) {
                ClearCache ();
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
            return "Thread";
        }

        public override NcResult StartSync ()
        {
            if (null != folder) {
                return  BackEnd.Instance.SyncCmd (folder.AccountId, folder.Id);
            } else {
                return NachoSyncResult.DoesNotSync ();
            }
        }

        public override bool IsCompatibleWithAccount (McAccount account)
        {
            return account.ContainsAccount (folder.AccountId);
        }

        public override DateTime? LastSuccessfulSyncTime ()
        {
            if (folder == null) {
                return null;
            }
            if (folder.IsClientOwned) {
                return null;
            }
            return folder.LastSyncAttempt;
        }

        public override void RefetchSyncTime ()
        {
            folder = McFolder.QueryById<McFolder> (folder.Id);
        }

        public override bool HasSentSemantics ()
        {
            if (folder == null) {
                return false;
            }
            return folder.Type == NachoCore.ActiveSync.Xml.FolderHierarchy.TypeCode.DefaultSent_5;
        }
    }
}

