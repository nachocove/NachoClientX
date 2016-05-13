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
        List<McEmailMessageThread> updatedThreadList;
        McFolder folder;
        public bool IncludeActions = true;
        int _CountIgnoringLimit;
        int UpdatedCountIgnoringLimit;

        public NachoPriorityEmailMessages (McFolder folder)
        {
            this.folder = folder;
            threadList = new List<McEmailMessageThread> ();
            _CountIgnoringLimit = 0;
        }

        public override bool BeginRefresh (out List<int> adds, out List<int> deletes)
        {
            double threshold = McEmailMessage.minHotScore;
            // Before statistics converge, there may be a period when there is no hot emails.
            // When that happens, lower the threshold until we found something
            var list = McEmailMessage.QueryActiveMessageItemsByScore (folder.AccountId, folder.Id, threshold, includeActions: IncludeActions);
            updatedThreadList = NcMessageThreads.ThreadByConversation (list);
            RemoveIgnoredMessages (updatedThreadList);
            UpdatedCountIgnoringLimit = updatedThreadList.Count;
            if (MessageLimit > 0 && updatedThreadList.Count > MessageLimit) {
                updatedThreadList = updatedThreadList.GetRange (0, MessageLimit);
            }
            return NcMessageThreads.AreDifferent (threadList, updatedThreadList, out adds, out deletes);
        }

        public override void CommitRefresh ()
        {
            ClearCache ();
            threadList = updatedThreadList;
            updatedThreadList = null;
            _CountIgnoringLimit = UpdatedCountIgnoringLimit;
            UpdatedCountIgnoringLimit = 0;
        }

        public override void RemoveIgnoredMessages ()
        {
            RemoveIgnoredMessages (threadList);
        }

        public override int Count ()
        {
            return threadList.Count;
        }

        public override int CountIgnoringLimit ()
        {
            return _CountIgnoringLimit;
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
    }
}


